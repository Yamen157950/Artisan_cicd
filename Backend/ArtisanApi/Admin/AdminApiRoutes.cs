using System.Security.Claims;
using ArtisanApi.Data;
using ArtisanApi.Models;
using ArtisanApi.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArtisanApi.Admin;

public static class AdminApiRoutes
{
    public static void MapAdminApi(this WebApplication app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization(policy => policy.RequireRole("Admin"));

        admin.MapGet(
                "/users",
                async (ClaimsPrincipal principal, UserManager<ApplicationUser> users, string? q, int page, int pageSize) =>
                {
                    page = Math.Max(1, page);
                    pageSize = pageSize <= 0 ? 20 : Math.Clamp(pageSize, 1, 100);
                    IQueryable<ApplicationUser> query = users.Users.AsNoTracking();
                    if (!string.IsNullOrWhiteSpace(q))
                    {
                        var term = q.Trim();
                        query = query.Where(u => u.Email != null && u.Email.Contains(term));
                    }

                    query = query.OrderBy(u => u.Email);

                    var total = await query.CountAsync();
                    var slice = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

                    var items = new List<object>(slice.Count);
                    foreach (var u in slice)
                    {
                        var r = await users.GetRolesAsync(u);
                        var locked =
                            u.LockoutEnabled
                            && u.LockoutEnd.HasValue
                            && u.LockoutEnd.Value.UtcDateTime > DateTime.UtcNow;
                        items.Add(
                            new
                            {
                                id = u.Id,
                                email = u.Email,
                                fullName = u.FullName,
                                roles = r,
                                isLockedOut = locked,
                                lockoutEnd = u.LockoutEnd,
                            }
                        );
                    }

                    return Results.Ok(new { total, page, pageSize, items });
                }
            )
            .WithName("AdminListUsers");

        admin.MapGet(
                "/users/{userId}/profile",
                async (string userId, UserManager<ApplicationUser> users, AppDbContext db) =>
                {
                    var target = await users.FindByIdAsync(userId);
                    if (target is null)
                        return Results.NotFound();

                    var roles = await users.GetRolesAsync(target);
                    var locked =
                        target.LockoutEnabled
                        && target.LockoutEnd.HasValue
                        && target.LockoutEnd.Value.UtcDateTime > DateTime.UtcNow;

                    var messageCount = await db.DirectMessages.CountAsync(m =>
                        m.SenderUserId == userId || m.RecipientUserId == userId
                    );

                    object? providerProfile = null;
                    var profile = await db.ProviderProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);
                    if (profile is not null)
                    {
                        var ratingRow = await db.ProviderRatings
                            .AsNoTracking()
                            .Where(r => r.ProviderProfileId == profile.Id)
                            .GroupBy(r => r.ProviderProfileId)
                            .Select(g => new { Avg = g.Average(x => (double)x.Score), Count = g.Count() })
                            .FirstOrDefaultAsync();

                        providerProfile = new
                        {
                            id = profile.Id,
                            displayName = profile.DisplayName,
                            trade = profile.Trade,
                            city = profile.City,
                            bio = profile.Bio,
                            photoUrl = profile.PhotoUrl ?? "",
                            workPhotosJson = profile.WorkPhotosJson,
                            priceLabel = ProviderMapper.FormatPriceLabel(profile.PriceAmount, profile.PriceUnit),
                            experienceLabel = ProviderMapper.FormatExperienceLabel(profile.ExperienceYears),
                            experienceYears = profile.ExperienceYears,
                            visibleInSearch = profile.VisibleInSearch,
                            joinedAt = profile.JoinedAt,
                            isSeededDemo = profile.IsSeededDemo,
                            averageRating = ratingRow?.Avg,
                            ratingCount = ratingRow?.Count ?? 0,
                        };
                    }

                    return Results.Ok(
                        new
                        {
                            id = target.Id,
                            email = target.Email ?? "",
                            fullName = target.FullName ?? "",
                            phone = target.PhoneNumber ?? target.Phone,
                            profilePhotoUrl = target.ProfilePhotoUrl ?? "",
                            roles,
                            isLockedOut = locked,
                            emailConfirmed = target.EmailConfirmed,
                            messageCount,
                            providerProfile,
                        }
                    );
                }
            )
            .WithName("AdminGetUserProfile");

        admin.MapPost(
                "/users/{userId}/block",
                async (ClaimsPrincipal principal, string userId, UserManager<ApplicationUser> users) =>
                {
                    var callerId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (string.Equals(callerId, userId, StringComparison.Ordinal))
                        return Results.BadRequest(new { error = "You cannot block your own account." });

                    var target = await users.FindByIdAsync(userId);
                    if (target is null)
                        return Results.NotFound();

                    if (await users.IsInRoleAsync(target, "Admin"))
                        return Results.BadRequest(new { error = "Admin accounts cannot be blocked." });

                    await users.SetLockoutEnabledAsync(target, true);
                    await users.SetLockoutEndDateAsync(target, DateTimeOffset.UtcNow.AddYears(100));
                    var res = await users.UpdateAsync(target);
                    if (!res.Succeeded)
                        return Results.BadRequest(new { errors = res.Errors.Select(e => e.Description).ToArray() });

                    return Results.Ok(new { ok = true });
                }
            )
            .WithName("AdminBlockUser");

        admin.MapPost(
                "/users/{userId}/unblock",
                async (string userId, UserManager<ApplicationUser> users) =>
                {
                    var target = await users.FindByIdAsync(userId);
                    if (target is null)
                        return Results.NotFound();

                    await users.SetLockoutEndDateAsync(target, null);
                    var res = await users.UpdateAsync(target);
                    if (!res.Succeeded)
                        return Results.BadRequest(new { errors = res.Errors.Select(e => e.Description).ToArray() });

                    return Results.Ok(new { ok = true });
                }
            )
            .WithName("AdminUnblockUser");

        admin.MapPost(
                "/users/{userId}/roles/moderator",
                async (string userId, UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles) =>
                {
                    if (!await roles.RoleExistsAsync("Moderator"))
                        await roles.CreateAsync(new IdentityRole("Moderator"));

                    var target = await users.FindByIdAsync(userId);
                    if (target is null)
                        return Results.NotFound();

                    if (await users.IsInRoleAsync(target, "Admin"))
                        return Results.BadRequest(new { error = "Admins already have full access; moderator role is not applied." });

                    if (await users.IsInRoleAsync(target, "Moderator"))
                        return Results.Ok(new { ok = true, message = "User is already a moderator." });

                    var add = await users.AddToRoleAsync(target, "Moderator");
                    if (!add.Succeeded)
                        return Results.BadRequest(new { errors = add.Errors.Select(e => e.Description).ToArray() });

                    return Results.Ok(new { ok = true });
                }
            )
            .WithName("AdminAddModerator");

        admin.MapDelete(
                "/users/{userId}/roles/moderator",
                async (ClaimsPrincipal principal, string userId, UserManager<ApplicationUser> users) =>
                {
                    var target = await users.FindByIdAsync(userId);
                    if (target is null)
                        return Results.NotFound();

                    if (!await users.IsInRoleAsync(target, "Moderator"))
                        return Results.Ok(new { ok = true, message = "User is not a moderator." });

                    var rem = await users.RemoveFromRoleAsync(target, "Moderator");
                    if (!rem.Succeeded)
                        return Results.BadRequest(new { errors = rem.Errors.Select(e => e.Description).ToArray() });

                    return Results.Ok(new { ok = true });
                }
            )
            .WithName("AdminRemoveModerator");
    }
}
