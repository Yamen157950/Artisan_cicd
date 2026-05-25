using ArtisanApi.Data.Entities;
using ArtisanApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArtisanApi.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var db = sp.GetRequiredService<AppDbContext>();

        await EnsureRole(roleManager, "Customer");
        await EnsureRole(roleManager, "Provider");
        await EnsureRole(roleManager, "Admin");
        await EnsureRole(roleManager, "Moderator");

        await SeedAdminAccountsAsync(userManager);

        if (!await db.ProviderProfiles.AnyAsync())
        {
            var coreDemos = CoreDemoProviders();
            db.ProviderProfiles.AddRange(coreDemos);
            await db.SaveChangesAsync();
            await SeedRatingsAsync(db, CoreDemoRatingSeeds());
        }

        await SeedExtendedDemoProvidersAsync(db);
    }

    private static ProviderProfile[] CoreDemoProviders() =>
        new[]
        {
            new ProviderProfile
            {
                Id = "demo-plumber-amman",
                DisplayName = "Khalid — Master Plumber",
                Trade = "Plumbing",
                City = "Amman",
                Bio = "Leak repairs, water heaters, and full bathroom installs.",
                PhotoUrl =
                    "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&w=240&q=80",
                JoinedAt = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero),
                PriceAmount = 25,
                PriceUnit = "hour",
                ExperienceYears = 12,
                VisibleInSearch = true,
                IsSeededDemo = true,
            },
            new ProviderProfile
            {
                Id = "demo-carpenter-zarqa",
                DisplayName = "Omar Woodworks",
                Trade = "Carpentry",
                City = "Zarqa",
                Bio = "Custom shelves, doors, and fitted kitchen units.",
                PhotoUrl =
                    "https://images.unsplash.com/photo-1472099645785-5658abf4ff4e?auto=format&fit=crop&w=240&q=80",
                JoinedAt = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
                PriceAmount = 40,
                PriceUnit = "day",
                ExperienceYears = 8,
                VisibleInSearch = true,
                IsSeededDemo = true,
            },
            new ProviderProfile
            {
                Id = "demo-clean-irbid",
                DisplayName = "Noor Home Cleaning",
                Trade = "Cleaning",
                City = "Irbid",
                Bio = "Deep cleaning and move-in/out packages.",
                PhotoUrl =
                    "https://images.unsplash.com/photo-1438761681033-6461ffad8d80?auto=format&fit=crop&w=240&q=80",
                JoinedAt = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero),
                PriceAmount = 15,
                PriceUnit = "hour",
                ExperienceYears = 5,
                VisibleInSearch = true,
                IsSeededDemo = true,
            },
            new ProviderProfile
            {
                Id = "demo-electric-amman",
                DisplayName = "Rami Electrical Services",
                Trade = "Electrical",
                City = "Amman",
                Bio = "Wiring, panels, and smart home installs.",
                PhotoUrl =
                    "https://images.unsplash.com/photo-1560250097-0b93528c311a?auto=format&fit=crop&w=240&q=80",
                JoinedAt = new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero),
                PriceAmount = 20,
                PriceUnit = "hour",
                ExperienceYears = 10,
                VisibleInSearch = true,
                IsSeededDemo = true,
            },
        };

    private static (string ProfileId, int[] Scores)[] CoreDemoRatingSeeds() =>
        new[]
        {
            ("demo-plumber-amman", new[] { 5, 5, 4 }),
            ("demo-carpenter-zarqa", new[] { 5, 4, 5 }),
            ("demo-clean-irbid", new[] { 5, 5, 5 }),
            ("demo-electric-amman", new[] { 4, 5, 5 }),
        };

    /// <summary>Painting, barber, and other trades — added even when core demos already exist.</summary>
    private static async Task SeedExtendedDemoProvidersAsync(AppDbContext db)
    {
        var extended = new[]
        {
            new ProviderProfile
            {
                Id = "demo-paint-amman",
                DisplayName = "Layla Interior Painting",
                Trade = "Painting",
                City = "Amman",
                Bio = "Interior walls, feature colours, and tidy finish for homes and offices.",
                PhotoUrl =
                    "https://images.unsplash.com/photo-1544005313-94ddf0286df2?auto=format&fit=crop&w=240&q=80",
                JoinedAt = new DateTimeOffset(2026, 3, 5, 0, 0, 0, TimeSpan.Zero),
                PriceAmount = 18,
                PriceUnit = "hour",
                ExperienceYears = 7,
                VisibleInSearch = true,
                IsSeededDemo = true,
            },
            new ProviderProfile
            {
                Id = "demo-paint-aqaba",
                DisplayName = "Hassan Pro Painters",
                Trade = "Painting",
                City = "Aqaba",
                Bio = "Exterior facades, weather-resistant coatings, and rental refresh packages.",
                PhotoUrl =
                    "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&w=240&q=80",
                JoinedAt = new DateTimeOffset(2026, 4, 2, 0, 0, 0, TimeSpan.Zero),
                PriceAmount = 22,
                PriceUnit = "hour",
                ExperienceYears = 9,
                VisibleInSearch = true,
                IsSeededDemo = true,
            },
            new ProviderProfile
            {
                Id = "demo-barber-amman",
                DisplayName = "Zaid Classic Cuts",
                Trade = "Barber",
                City = "Amman",
                Bio = "Fade cuts, beard shaping, and hot towel finish — home visits available.",
                PhotoUrl =
                    "https://images.unsplash.com/photo-1506794778202-cad84cf45f1d?auto=format&fit=crop&w=240&q=80",
                JoinedAt = new DateTimeOffset(2026, 2, 14, 0, 0, 0, TimeSpan.Zero),
                PriceAmount = 12,
                PriceUnit = "hour",
                ExperienceYears = 6,
                VisibleInSearch = true,
                IsSeededDemo = true,
            },
            new ProviderProfile
            {
                Id = "demo-barber-irbid",
                DisplayName = "Tareq Style Studio",
                Trade = "Barber",
                City = "Irbid",
                Bio = "Modern styles for men and teens; walk-ins and booked appointments.",
                PhotoUrl =
                    "https://images.unsplash.com/photo-1519085360753-af0119f7cbe7?auto=format&fit=crop&w=240&q=80",
                JoinedAt = new DateTimeOffset(2026, 3, 22, 0, 0, 0, TimeSpan.Zero),
                PriceAmount = 10,
                PriceUnit = "hour",
                ExperienceYears = 4,
                VisibleInSearch = true,
                IsSeededDemo = true,
            },
            new ProviderProfile
            {
                Id = "demo-garden-zarqa",
                DisplayName = "Salma Garden Care",
                Trade = "Gardening",
                City = "Zarqa",
                Bio = "Lawn trimming, planting, and seasonal garden maintenance.",
                PhotoUrl =
                    "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=240&q=80",
                JoinedAt = new DateTimeOffset(2026, 4, 8, 0, 0, 0, TimeSpan.Zero),
                PriceAmount = 16,
                PriceUnit = "hour",
                ExperienceYears = 5,
                VisibleInSearch = true,
                IsSeededDemo = true,
            },
            new ProviderProfile
            {
                Id = "demo-hvac-amman",
                DisplayName = "Fadi Cool Air HVAC",
                Trade = "HVAC",
                City = "Amman",
                Bio = "AC installation, servicing, and duct cleaning for apartments and shops.",
                PhotoUrl =
                    "https://images.unsplash.com/photo-1560250097-0b93528c311a?auto=format&fit=crop&w=240&q=80",
                JoinedAt = new DateTimeOffset(2026, 1, 28, 0, 0, 0, TimeSpan.Zero),
                PriceAmount = 28,
                PriceUnit = "hour",
                ExperienceYears = 11,
                VisibleInSearch = true,
                IsSeededDemo = true,
            },
        };

        var existingIds = await db.ProviderProfiles.Select(p => p.Id).ToListAsync();
        var toAdd = extended.Where(p => !existingIds.Contains(p.Id)).ToArray();
        if (toAdd.Length == 0)
            return;

        db.ProviderProfiles.AddRange(toAdd);
        await db.SaveChangesAsync();

        var ratingSeeds = new (string ProfileId, int[] Scores)[]
        {
            ("demo-paint-amman", new[] { 5, 4, 5 }),
            ("demo-paint-aqaba", new[] { 5, 5, 4 }),
            ("demo-barber-amman", new[] { 5, 5, 5 }),
            ("demo-barber-irbid", new[] { 4, 5, 4 }),
            ("demo-garden-zarqa", new[] { 5, 4, 4 }),
            ("demo-hvac-amman", new[] { 5, 5, 5 }),
        };

        await SeedRatingsAsync(
            db,
            ratingSeeds.Where(r => toAdd.Any(p => p.Id == r.ProfileId)).ToArray()
        );
    }

    private static async Task SeedRatingsAsync(
        AppDbContext db,
        (string ProfileId, int[] Scores)[] ratingSeeds
    )
    {
        foreach (var (profileId, scores) in ratingSeeds)
        {
            if (await db.ProviderRatings.AnyAsync(r => r.ProviderProfileId == profileId))
                continue;

            foreach (var s in scores)
            {
                db.ProviderRatings.Add(
                    new ProviderRating
                    {
                        Id = Guid.NewGuid(),
                        ProviderProfileId = profileId,
                        CustomerUserId = null,
                        Score = s,
                        CreatedAt = DateTimeOffset.UtcNow,
                    }
                );
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedAdminAccountsAsync(UserManager<ApplicationUser> users)
    {
        const string password = "ArtisanAdmin2026!";
        var admins = new (string Email, string FullName)[]
        {
            ("admin1@artisan.local", "Seeded Admin One"),
            ("admin2@artisan.local", "Seeded Admin Two"),
            ("admin3@artisan.local", "Seeded Admin Three"),
        };

        foreach (var (email, fullName) in admins)
        {
            var existing = await users.FindByEmailAsync(email);
            if (existing is not null)
            {
                if (!await users.IsInRoleAsync(existing, "Admin"))
                    await users.AddToRoleAsync(existing, "Admin");
                continue;
            }

            var u = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                LockoutEnabled = true,
            };

            var create = await users.CreateAsync(u, password);
            if (!create.Succeeded)
                continue;

            await users.AddToRoleAsync(u, "Admin");
        }
    }

    private static async Task EnsureRole(RoleManager<IdentityRole> roleManager, string name)
    {
        if (!await roleManager.RoleExistsAsync(name))
            await roleManager.CreateAsync(new IdentityRole(name));
    }
}
