using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ArtisanApi.Admin;
using ArtisanApi.Data;
using ArtisanApi.Data.Entities;
using ArtisanApi.Hubs;
using ArtisanApi.Models;
using ArtisanApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Provider profile PUT sends workPhotosJson as base64 data URLs — allow a generous body limit.
builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = 100 * 1024 * 1024;
});

var dataDir = Path.Combine(builder.Environment.ContentRootPath, "Data");
Directory.CreateDirectory(dataDir);
var chatAttachmentsDir = Path.Combine(dataDir, "chat-attachments");
Directory.CreateDirectory(chatAttachmentsDir);
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));
builder.Services.AddSingleton<HerafiTrainingMatcher>();
builder.Services.AddScoped<HerafiChatService>();
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = ChatAttachmentRules.MaxBytes + 4 * 1024 * 1024;
});

builder
    .Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? "ArtisanDevJwtKey_ChangeInProduction_Min32Chars!!";
var jwtIssuer = jwtSection["Issuer"] ?? "ArtisanApi";
var jwtAudience = jwtSection["Audience"] ?? "ArtisanFrontend";

builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<GoogleSignInService>();
builder.Services.Configure<SmtpEmailOptions>(builder.Configuration.GetSection(SmtpEmailOptions.SectionName));
builder.Services.Configure<SendGridEmailOptions>(builder.Configuration.GetSection(SendGridEmailOptions.SectionName));
builder.Services.AddSingleton<SmtpEmailSender>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<PasswordResetMailer>();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            RoleClaimType = ClaimTypes.Role,
        };
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

builder.Services.AddCors(o =>
    o.AddPolicy(
        "frontend",
        p =>
            p.SetIsOriginAllowed(origin =>
                {
                    if (string.IsNullOrEmpty(origin))
                        return false;
                    return origin.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase)
                        || origin.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase)
                        || origin.StartsWith("https://localhost:", StringComparison.OrdinalIgnoreCase);
                })
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
    )
);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var mailProbe = scope.ServiceProvider.GetRequiredService<PasswordResetMailer>();
        var exposeOtpInDev = app.Configuration.GetValue("ForgotPassword:ExposeOtpInDevelopment", false);
        if (!mailProbe.CanSend && !exposeOtpInDev)
        {
            app.Logger.LogInformation(
                "Outbound email is not configured; password reset codes will not be emailed. "
                    + "Set SendGrid:ApiKey + SendGrid:FromEmail, or Smtp:User + Smtp:Password (+ Smtp:Host). "
                    + "Use dotnet user-secrets or environment variables (SendGrid__ApiKey, Smtp__Password, …)."
            );
        }
    }
}

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbInitializer.SeedAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("frontend");

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

// SignalR hub (JWT via Authorization header on negotiate; access_token query on WebSocket upgrade).
// If /hubs/chat returns 404, restart the API so the running process picks up this mapping.
app.MapHub<ChatHub>("/hubs/chat").RequireAuthorization();
app.MapHub<BrowseHub>("/hubs/browse");

app.MapGet("/api/health", () => Results.Ok(new { ok = true, service = "ArtisanApi" })).WithName("Health");

app.MapPost(
        "/api/herafi/chat",
        async (HerafiChatRequest req, HerafiChatService chat, CancellationToken ct) =>
        {
            var message = (req.Message ?? "").Trim();
            if (string.IsNullOrEmpty(message))
                return Results.Ok(new HerafiChatResponse("Please type a message.", null, 0));

            var result = await chat.GetResponseAsync(message, ct);
            return Results.Ok(new HerafiChatResponse(result.Reply, result.Navigate, result.RedirectDelayMs, result.TopPick, result.SimilarResults));
        }
    )
    .AllowAnonymous()
    .WithName("HerafiChat");

app.MapGet(
        "/api/auth/public-config",
        (GoogleSignInService google) =>
        {
            var id = google.GetClientId();
            return Results.Ok(new { googleClientId = id ?? "" });
        }
    )
    .AllowAnonymous()
    .WithName("AuthPublicConfig");

app.MapPost(
        "/api/auth/register",
        async (RegisterRequest req, UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles, JwtTokenService jwt, AppDbContext db) =>
        {
            var roleName = req.Role.Trim().ToLowerInvariant() switch
            {
                "customer" => "Customer",
                "provider" => "Provider",
                _ => (string?)null,
            };
            if (roleName is null)
                return Results.BadRequest(new { error = "Role must be customer or provider." });

            if (PasswordInputRules.ContainsArabicScript(req.Password))
                return Results.BadRequest(new { error = PasswordInputRules.ArabicNotAllowedMessage });

            var email = req.Email.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(email))
                return Results.BadRequest(new { error = "Email is required." });

            if (await users.FindByEmailAsync(email) is not null)
                return Results.Conflict(new { error = "An account with this email already exists. Please log in instead." });

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = req.FullName.Trim(),
                Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim(),
            };

            var create = await users.CreateAsync(user, req.Password);
            if (!create.Succeeded)
            {
                if (create.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
                    return Results.Conflict(new { error = "An account with this email already exists. Please log in instead." });

                return Results.BadRequest(new { errors = create.Errors.Select(e => e.Description).ToArray() });
            }

            await users.AddToRoleAsync(user, roleName);

            string? profileId = null;
            if (roleName == "Provider")
            {
                var profile = new ProviderProfile
                {
                    Id = Guid.NewGuid().ToString("N"),
                    UserId = user.Id,
                    DisplayName = user.FullName ?? user.Email!,
                    Trade = "",
                    City = "Jordan",
                    Bio = "",
                    JoinedAt = DateTimeOffset.UtcNow,
                    VisibleInSearch = true,
                    IsSeededDemo = false,
                };
                db.ProviderProfiles.Add(profile);
                await db.SaveChangesAsync();
                profileId = profile.Id;
            }

            var rolesList = await users.GetRolesAsync(user);
            var token = jwt.CreateAccessToken(user, rolesList, profileId);
            return Results.Ok(
                new AuthResponse
                {
                    AccessToken = token,
                    ExpiresInSeconds = jwt.GetExpireSeconds(),
                    Email = user.Email ?? "",
                    FullName = user.FullName ?? "",
                    Role = roleName,
                    ProviderProfileId = profileId,
                }
            );
        }
    )
    .WithName("Register");

app.MapPost(
        "/api/auth/login",
        async (LoginRequest req, UserManager<ApplicationUser> users, JwtTokenService jwt, AppDbContext db) =>
        {
            if (PasswordInputRules.ContainsArabicScript(req.Password))
                return Results.BadRequest(new { error = PasswordInputRules.ArabicNotAllowedMessage });

            var user = await users.FindByEmailAsync(req.Email.Trim().ToLowerInvariant());
            if (user is null || !await users.CheckPasswordAsync(user, req.Password))
                return Results.Unauthorized();

            if (await users.IsLockedOutAsync(user))
                return Results.Json(new { error = "Your account has been suspended. Contact support." }, statusCode: StatusCodes.Status403Forbidden);

            var rolesList = await users.GetRolesAsync(user);
            var primaryRole = AuthRoleHelper.PublicPrimaryRole(rolesList);
            var profile = await db.ProviderProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == user.Id);
            var token = jwt.CreateAccessToken(user, rolesList, profile?.Id);
            return Results.Ok(
                new AuthResponse
                {
                    AccessToken = token,
                    ExpiresInSeconds = jwt.GetExpireSeconds(),
                    Email = user.Email ?? "",
                    FullName = user.FullName ?? "",
                    Role = primaryRole,
                    ProviderProfileId = profile?.Id,
                }
            );
        }
    )
    .WithName("Login");

app.MapPost(
        "/api/auth/forgot-password/request",
        async (
            ForgotPasswordStartRequest req,
            UserManager<ApplicationUser> users,
            AppDbContext db,
            IConfiguration configuration,
            IHostEnvironment env,
            ILoggerFactory loggerFactory,
            PasswordResetMailer mailer
        ) =>
        {
            var log = loggerFactory.CreateLogger("ForgotPassword");
            var emailTrim = (req.Email ?? "").Trim();
            var norm = users.NormalizeEmail(emailTrim);
            if (string.IsNullOrEmpty(norm))
                return Results.BadRequest(new { error = "Invalid email." });

            var user = await users.FindByEmailAsync(emailTrim);
            string? otpPlain = null;
            if (user is not null)
            {
                var now = DateTimeOffset.UtcNow;
                await db.ForgotPasswordChallenges.Where(c => c.EmailNormalized == norm).ExecuteDeleteAsync();
                otpPlain = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString(System.Globalization.CultureInfo.InvariantCulture);
                db.ForgotPasswordChallenges.Add(
                    new ForgotPasswordChallenge
                    {
                        Id = Guid.NewGuid(),
                        EmailNormalized = norm,
                        OtpCode = otpPlain,
                        ExpiresAtUtc = now.AddMinutes(15),
                        ConsumedAtUtc = null,
                    }
                );
                await db.SaveChangesAsync();
                log.LogInformation("Password reset OTP for {Email} expires {Expires}", user.Email, now.AddMinutes(15));

                var toAddr = user.Email ?? emailTrim;
                if (mailer.CanSend)
                {
                    var sent = await mailer.TrySendPasswordResetOtpAsync(toAddr, otpPlain);
                    if (!sent)
                        log.LogWarning("Password reset OTP email failed for {Email}. Check SendGrid/SMTP logs.", toAddr);
                }
                else
                {
                    if (configuration.GetValue("ForgotPassword:ExposeOtpInDevelopment", false))
                        log.LogDebug(
                            "Password reset OTP not emailed for {Email} (development mode returns devOtp in response).",
                            toAddr
                        );
                    else
                        log.LogInformation(
                            "Password reset OTP not emailed for {Email}: configure SendGrid or SMTP (see appsettings / user-secrets).",
                            toAddr
                        );
                }
            }

            var expose = env.IsDevelopment() && configuration.GetValue("ForgotPassword:ExposeOtpInDevelopment", false);
            if (expose && otpPlain is not null)
                return Results.Ok(
                    new
                    {
                        ok = true,
                        message = "Development only: use the code below. In production this would be emailed.",
                        devOtp = otpPlain,
                    }
                );

            return Results.Ok(new { ok = true, message = "If an account exists for this email, a verification code was sent." });
        }
    )
    .AllowAnonymous()
    .WithName("ForgotPasswordRequest");

app.MapPost(
        "/api/auth/forgot-password/verify-otp",
        async (
            ForgotPasswordVerifyOtpRequest req,
            UserManager<ApplicationUser> users,
            AppDbContext db,
            ILoggerFactory loggerFactory
        ) =>
        {
            var log = loggerFactory.CreateLogger("ForgotPassword");
            var emailTrim = (req.Email ?? "").Trim();
            var norm = users.NormalizeEmail(emailTrim);
            if (string.IsNullOrEmpty(norm))
                return Results.BadRequest(new { error = "Invalid email." });

            var digits = new string((req.Otp ?? "").Where(char.IsDigit).ToArray());
            if (digits.Length != 6)
                return Results.BadRequest(new { error = "Enter the 6-digit code." });

            var user = await users.FindByEmailAsync(emailTrim);
            if (user is null)
                return Results.BadRequest(new { error = "Invalid or expired code." });

            var now = DateTimeOffset.UtcNow;
            var ch = await db.ForgotPasswordChallenges.FirstOrDefaultAsync(c =>
                c.EmailNormalized == norm && c.ConsumedAtUtc == null && c.ExpiresAtUtc > now
            );
            if (ch is null || !string.Equals(ch.OtpCode, digits, StringComparison.Ordinal))
                return Results.BadRequest(new { error = "Invalid or expired code." });

            ch.ConsumedAtUtc = now;
            await db.SaveChangesAsync();

            string resetToken;
            try
            {
                resetToken = await users.GeneratePasswordResetTokenAsync(user);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "GeneratePasswordResetTokenAsync failed for {Email}", emailTrim);
                return Results.BadRequest(
                    new
                    {
                        error = "Password reset could not be started for this account. If you use Google sign-in, use that to log in.",
                    }
                );
            }

            return Results.Ok(new { resetToken });
        }
    )
    .AllowAnonymous()
    .WithName("ForgotPasswordVerifyOtp");

app.MapPost(
        "/api/auth/forgot-password/reset",
        async (ForgotPasswordResetRequest req, UserManager<ApplicationUser> users) =>
        {
            var emailTrim = (req.Email ?? "").Trim();
            var user = await users.FindByEmailAsync(emailTrim);
            if (user is null)
                return Results.BadRequest(new { error = "Invalid request." });

            var token = (req.Token ?? "").Trim();
            if (string.IsNullOrEmpty(token))
                return Results.BadRequest(new { error = "Invalid or expired reset link. Start again from the email step." });

            if (PasswordInputRules.ContainsArabicScript(req.NewPassword))
                return Results.BadRequest(new { error = PasswordInputRules.ArabicNotAllowedMessage });

            var result = await users.ResetPasswordAsync(user, token, req.NewPassword);
            if (!result.Succeeded)
                return Results.BadRequest(new { errors = result.Errors.Select(e => e.Description).ToArray() });

            return Results.Ok(new { ok = true });
        }
    )
    .AllowAnonymous()
    .WithName("ForgotPasswordReset");

const string GoogleLoginProvider = "Google";

app.MapPost(
        "/api/auth/google",
        async (
            GoogleSignInRequest req,
            GoogleSignInService googleSvc,
            UserManager<ApplicationUser> users,
            RoleManager<IdentityRole> roles,
            JwtTokenService jwt,
            AppDbContext db
        ) =>
        {
            var (payload, err) = await googleSvc.ValidateIdTokenAsync(req.IdToken);
            if (payload is null)
                return Results.BadRequest(new { error = err ?? "invalid_token" });

            if (string.IsNullOrWhiteSpace(payload.Email))
                return Results.BadRequest(new { error = "Google account has no email." });

            if (payload.EmailVerified is false)
                return Results.BadRequest(new { error = "Google email is not verified." });

            var email = payload.Email.Trim().ToLowerInvariant();
            var sub = payload.Subject;
            if (string.IsNullOrWhiteSpace(sub))
                return Results.BadRequest(new { error = "Invalid Google token (no sub)." });

            var loginInfo = new UserLoginInfo(GoogleLoginProvider, sub, GoogleLoginProvider);

            var user = await users.FindByLoginAsync(GoogleLoginProvider, sub);
            if (user is null)
            {
                user = await users.FindByEmailAsync(email);
                if (user is not null)
                {
                    var existingLogins = await users.GetLoginsAsync(user);
                    if (existingLogins.Any(l => l.LoginProvider == GoogleLoginProvider && !string.Equals(l.ProviderKey, sub, StringComparison.Ordinal)))
                        return Results.Conflict(new { error = "This email is linked to a different Google account." });

                    if (existingLogins.All(l => l.LoginProvider != GoogleLoginProvider))
                    {
                        var addLogin = await users.AddLoginAsync(user, loginInfo);
                        if (!addLogin.Succeeded)
                            return Results.BadRequest(new { errors = addLogin.Errors.Select(e => e.Description).ToArray() });
                    }
                }
            }

            if (user is null)
            {
                var roleName = (req.Role ?? "").Trim().ToLowerInvariant() switch
                {
                    "provider" => "Provider",
                    _ => "Customer",
                };
                if (!await roles.RoleExistsAsync(roleName))
                    await roles.CreateAsync(new IdentityRole(roleName));

                var fullName = string.IsNullOrWhiteSpace(payload.Name) ? email.Split('@')[0] : payload.Name.Trim();
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = fullName,
                };

                var randomPwd = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "Aa1!";
                var create = await users.CreateAsync(user, randomPwd);
                if (!create.Succeeded)
                    return Results.BadRequest(new { errors = create.Errors.Select(e => e.Description).ToArray() });

                var addGoogle = await users.AddLoginAsync(user, loginInfo);
                if (!addGoogle.Succeeded)
                    return Results.BadRequest(new { errors = addGoogle.Errors.Select(e => e.Description).ToArray() });

                await users.AddToRoleAsync(user, roleName);

                string? profileId = null;
                if (roleName == "Provider")
                {
                    var profile = new ProviderProfile
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        UserId = user.Id,
                        DisplayName = user.FullName ?? user.Email!,
                        Trade = "",
                        City = "Jordan",
                        Bio = "",
                        JoinedAt = DateTimeOffset.UtcNow,
                        VisibleInSearch = true,
                        IsSeededDemo = false,
                    };
                    db.ProviderProfiles.Add(profile);
                    await db.SaveChangesAsync();
                    profileId = profile.Id;
                }

                var rolesListNew = await users.GetRolesAsync(user);
                var tokenNew = jwt.CreateAccessToken(user, rolesListNew, profileId);
                return Results.Ok(
                    new AuthResponse
                    {
                        AccessToken = tokenNew,
                        ExpiresInSeconds = jwt.GetExpireSeconds(),
                        Email = user.Email ?? "",
                        FullName = user.FullName ?? "",
                        Role = roleName,
                        ProviderProfileId = profileId,
                    }
                );
            }

            if (await users.IsLockedOutAsync(user))
                return Results.Json(new { error = "Your account has been suspended. Contact support." }, statusCode: StatusCodes.Status403Forbidden);

            var rolesList = await users.GetRolesAsync(user);
            var primaryRole = AuthRoleHelper.PublicPrimaryRole(rolesList);
            var profileExisting = await db.ProviderProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == user.Id);
            var token = jwt.CreateAccessToken(user, rolesList, profileExisting?.Id);
            return Results.Ok(
                new AuthResponse
                {
                    AccessToken = token,
                    ExpiresInSeconds = jwt.GetExpireSeconds(),
                    Email = user.Email ?? "",
                    FullName = user.FullName ?? "",
                    Role = primaryRole,
                    ProviderProfileId = profileExisting?.Id,
                }
            );
        }
    )
    .AllowAnonymous()
    .WithName("GoogleSignIn");

app.MapGet(
        "/api/auth/me",
        async (ClaimsPrincipal principal, UserManager<ApplicationUser> users, AppDbContext db) =>
        {
            var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(id))
                return Results.Unauthorized();
            var user = await users.FindByIdAsync(id);
            if (user is null)
                return Results.Unauthorized();
            var roles = await users.GetRolesAsync(user);
            var profile = await db.ProviderProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == id);
            var logins = await users.GetLoginsAsync(user);
            var linkedGoogle = logins.Any(l => string.Equals(l.LoginProvider, GoogleLoginProvider, StringComparison.Ordinal));
            return Results.Ok(
                new MeResponse
                {
                    Id = user.Id,
                    Email = user.Email ?? "",
                    FullName = user.FullName ?? "",
                    Phone = user.Phone,
                    ProfilePhotoUrl = user.ProfilePhotoUrl,
                    LinkedGoogle = linkedGoogle,
                    Role = AuthRoleHelper.PublicPrimaryRole(roles),
                    ProviderProfileId = profile?.Id,
                }
            );
        }
    )
    .RequireAuthorization()
    .WithName("Me");

app.MapPut(
        "/api/me/account",
        async (UpdateAccountRequest req, ClaimsPrincipal principal, UserManager<ApplicationUser> users, AppDbContext db) =>
        {
            var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(id))
                return Results.Unauthorized();
            var user = await users.FindByIdAsync(id);
            if (user is null)
                return Results.Unauthorized();

            user.FullName = req.FullName.Trim();
            user.Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();
            user.ProfilePhotoUrl = string.IsNullOrWhiteSpace(req.ProfilePhotoUrl) ? null : req.ProfilePhotoUrl.Trim();
            var update = await users.UpdateAsync(user);
            if (!update.Succeeded)
                return Results.BadRequest(new { errors = update.Errors.Select(e => e.Description).ToArray() });

            var roles = await users.GetRolesAsync(user);
            var profile = await db.ProviderProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == id);
            var logins = await users.GetLoginsAsync(user);
            var linkedGoogle = logins.Any(l => string.Equals(l.LoginProvider, GoogleLoginProvider, StringComparison.Ordinal));
            return Results.Ok(
                new MeResponse
                {
                    Id = user.Id,
                    Email = user.Email ?? "",
                    FullName = user.FullName ?? "",
                    Phone = user.Phone,
                    ProfilePhotoUrl = user.ProfilePhotoUrl,
                    LinkedGoogle = linkedGoogle,
                    Role = AuthRoleHelper.PublicPrimaryRole(roles),
                    ProviderProfileId = profile?.Id,
                }
            );
        }
    )
    .RequireAuthorization()
    .WithName("UpdateMyAccount");

async Task<Dictionary<string, double>> LoadRatingAverages(AppDbContext db)
{
    return await db.ProviderRatings
        .AsNoTracking()
        .GroupBy(r => r.ProviderProfileId)
        .Select(g => new { g.Key, Avg = g.Average(x => (double)x.Score) })
        .ToDictionaryAsync(x => x.Key, x => x.Avg);
}

app.MapGet(
        "/api/providers",
        async (string? q, string? search, AppDbContext db) =>
        {
            var term = !string.IsNullOrWhiteSpace(q) ? q : search;
            var providerUserIds = await ProviderBrowseRules.GetProviderRoleUserIdsAsync(db);
            var candidates = await db.ProviderProfiles.AsNoTracking().Where(p => p.VisibleInSearch).ToListAsync();
            var all = candidates.Where(p => ProviderBrowseRules.IsPublicBrowseCard(p, providerUserIds)).ToList();
            if (!string.IsNullOrWhiteSpace(term))
            {
                var t = term.Trim();
                all = all
                    .Where(p =>
                    {
                        var blob = $"{p.DisplayName} {p.Trade} {p.City} {p.Bio} {ProviderMapper.FormatPriceLabel(p.PriceAmount, p.PriceUnit)} {ProviderMapper.FormatExperienceLabel(p.ExperienceYears)}";
                        return blob.Contains(t, StringComparison.OrdinalIgnoreCase);
                    })
                    .ToList();
            }

            all.Sort((a, b) => b.JoinedAt.CompareTo(a.JoinedAt));
            var avgs = await LoadRatingAverages(db);
            var dtos = all.Select(p => ProviderMapper.ToListItem(p, avgs.GetValueOrDefault(p.Id))).ToList();
            return Results.Ok(dtos);
        }
    )
    .WithName("ListProviders");

app.MapGet(
        "/api/providers/{id}",
        async (string id, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var p = await db.ProviderProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (p is null)
                return Results.NotFound();

            var requestUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var isOwner = !string.IsNullOrEmpty(requestUserId) && p.UserId == requestUserId;

            if (!p.VisibleInSearch)
            {
                if (!isOwner)
                    return Results.NotFound();
            }
            else if (!isOwner)
            {
                var providerUserIds = await ProviderBrowseRules.GetProviderRoleUserIdsAsync(db);
                if (!ProviderBrowseRules.IsPublicBrowseCard(p, providerUserIds))
                    return Results.NotFound();
            }

            var avgs = await LoadRatingAverages(db);
            var avg = avgs.GetValueOrDefault(p.Id);
            return Results.Ok(ProviderMapper.ToDetail(p, avg));
        }
    )
    .AllowAnonymous()
    .WithName("GetProvider");

app.MapGet(
        "/api/me/provider-profile",
        async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();
            var p = await db.ProviderProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
            if (p is null)
                return Results.NotFound();
            var avgs = await LoadRatingAverages(db);
            return Results.Ok(ProviderMapper.ToDetail(p, avgs.GetValueOrDefault(p.Id)));
        }
    )
    .RequireAuthorization()
    .WithName("GetMyProviderProfile");

app.MapPut(
        "/api/me/provider-profile",
        async (
            ProviderProfileSaveDto req,
            ClaimsPrincipal principal,
            AppDbContext db,
            IHubContext<BrowseHub> browseHub
        ) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();
            var p = await db.ProviderProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
            if (p is null)
                return Results.NotFound();

            const int maxWorkPhotosJsonChars = 12_000_000;
            if (req.WorkPhotosJson is { Length: > maxWorkPhotosJsonChars })
                return Results.BadRequest(
                    new { error = "Work samples payload is too large. Remove some images or use smaller files." }
                );

            if (!string.IsNullOrWhiteSpace(req.WorkPhotosJson))
            {
                try
                {
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<string[]>(req.WorkPhotosJson);
                    if (parsed is null || parsed.Length > 16)
                        return Results.BadRequest(new { error = "Work samples: invalid format or too many images (max 16)." });
                    foreach (var u in parsed)
                    {
                        if (u is null || u.Length == 0)
                            return Results.BadRequest(new { error = "Work samples: empty image entry." });
                        if (u.Length > 4_000_000)
                            return Results.BadRequest(new { error = "One work sample image is too large. Use smaller images." });
                        if (!u.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
                            && !u.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                            && !u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                            return Results.BadRequest(new { error = "Work samples must be image data URLs or http(s) links." });
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    return Results.BadRequest(new { error = "Work samples must be a JSON array of image strings." });
                }
            }

            p.DisplayName = req.DisplayName.Trim();
            p.Trade = req.Trade.Trim();
            p.City = string.IsNullOrWhiteSpace(req.City) ? "Jordan" : req.City.Trim();
            p.Bio = req.Bio.Trim();
            p.PhotoUrl = req.PhotoUrl;
            p.WorkPhotosJson = req.WorkPhotosJson;
            p.PriceAmount = req.PriceAmount is < 0 ? null : req.PriceAmount;
            p.PriceUnit = req.PriceUnit is "hour" or "day" or "week" ? req.PriceUnit : "hour";
            p.ExperienceYears = req.ExperienceYears is < 0 or > 80 ? null : req.ExperienceYears;
            var prevVisible = p.VisibleInSearch;
            p.VisibleInSearch = req.VisibleInSearch;

            await db.SaveChangesAsync();

            if (prevVisible != p.VisibleInSearch)
            {
                await browseHub.Clients
                    .Group(BrowseHub.FeedGroup)
                    .SendAsync(
                        "ProviderVisibilityChanged",
                        new { providerId = p.Id, searchable = p.VisibleInSearch }
                    );
            }

            var avgs = await LoadRatingAverages(db);
            return Results.Ok(ProviderMapper.ToDetail(p, avgs.GetValueOrDefault(p.Id)));
        }
    )
    .RequireAuthorization()
    .WithName("UpdateMyProviderProfile");

app.MapPost(
        "/api/providers/{id}/ratings",
        async (string id, PostRatingRequest req, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var profile = await db.ProviderProfiles.FirstOrDefaultAsync(p => p.Id == id);
            if (profile is null)
                return Results.NotFound();

            var providerUserIds = await ProviderBrowseRules.GetProviderRoleUserIdsAsync(db);
            if (!ProviderBrowseRules.IsPublicBrowseCard(profile, providerUserIds))
                return Results.NotFound();

            var customerId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            db.ProviderRatings.Add(
                new ProviderRating
                {
                    Id = Guid.NewGuid(),
                    ProviderProfileId = id,
                    CustomerUserId = customerId,
                    Score = req.Score,
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
            await db.SaveChangesAsync();
            var avg = await db.ProviderRatings.Where(r => r.ProviderProfileId == id).AverageAsync(r => (double)r.Score);
            return Results.Ok(new { averageRating = avg });
        }
    )
    .RequireAuthorization()
    .WithName("RateProvider");

app.MapGet(
        "/api/me/chats",
        async (ClaimsPrincipal principal, UserManager<ApplicationUser> users, AppDbContext db) =>
        {
            var me = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(me))
                return Results.Unauthorized();

            // SQLite: cannot ORDER BY DateTimeOffset in SQL — sort in memory.
            var msgs = (await db.DirectMessages.AsNoTracking()
                    .Where(m => m.SenderUserId == me || m.RecipientUserId == me)
                    .ToListAsync())
                .Where(m => !ChatMessageFilters.IsHiddenAutoChatMessage(m.Body))
                .OrderByDescending(m => m.SentAt)
                .ToList();

            var partnerIds = new HashSet<string>();
            foreach (var m in msgs)
            {
                if (m.SenderUserId != me)
                    partnerIds.Add(m.SenderUserId);
                if (m.RecipientUserId != me)
                    partnerIds.Add(m.RecipientUserId);
            }

            var readStates = await db.ChatThreadReadStates.AsNoTracking()
                .Where(r => r.ReaderUserId == me)
                .ToDictionaryAsync(r => r.PartnerUserId, r => r.LastReadAt);

            var result = new List<ChatPartnerDto>();
            foreach (var pid in partnerIds)
            {
                var thread = msgs.Where(m =>
                        (m.SenderUserId == me && m.RecipientUserId == pid) || (m.SenderUserId == pid && m.RecipientUserId == me)
                    )
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefault();
                if (thread is null)
                    continue;

                var lastRead = readStates.TryGetValue(pid, out var lr)
                    ? lr
                    : msgs
                            .Where(m => m.SenderUserId == me && m.RecipientUserId == pid)
                            .Select(m => m.SentAt)
                            .DefaultIfEmpty(DateTimeOffset.MinValue)
                            .Max();
                var unreadCount = msgs.Count(m =>
                    m.SenderUserId == pid && m.RecipientUserId == me && m.SentAt > lastRead
                );

                var u = await users.FindByIdAsync(pid);
                var prof = await db.ProviderProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == pid);
                result.Add(
                    new ChatPartnerDto
                    {
                        UserId = pid,
                        DisplayName = u?.FullName ?? u?.Email ?? pid,
                        ProviderProfileId = prof?.Id,
                        LastMessagePreview = thread.Body.Length > 120 ? thread.Body[..120] + "…" : thread.Body,
                        LastMessageAt = thread.SentAt,
                        UnreadCount = unreadCount,
                    }
                );
            }

            result.Sort((a, b) => b.LastMessageAt.CompareTo(a.LastMessageAt));
            return Results.Ok(result);
        }
    )
    .RequireAuthorization()
    .WithName("ChatInbox");

app.MapGet(
        "/api/me/chats/{partnerUserId}/messages",
        async (string partnerUserId, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var me = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(me))
                return Results.Unauthorized();

            partnerUserId = partnerUserId.Trim();
            if (string.IsNullOrEmpty(partnerUserId))
                return Results.BadRequest();

            var rows = (await db.DirectMessages.AsNoTracking()
                    .Where(m =>
                        (m.SenderUserId == me && m.RecipientUserId == partnerUserId)
                        || (m.SenderUserId == partnerUserId && m.RecipientUserId == me)
                    )
                    .ToListAsync())
                .Where(m => !ChatMessageFilters.IsHiddenAutoChatMessage(m.Body))
                .OrderBy(m => m.SentAt)
                .ToList();

            var dtos = rows
                .Select(m => new MessageItemDto
                {
                    Id = m.Id,
                    SenderUserId = m.SenderUserId,
                    Body = m.Body,
                    SentAt = m.SentAt,
                    IsMine = m.SenderUserId == me,
                    Attachment = string.IsNullOrEmpty(m.AttachmentStoredName)
                        ? null
                        : new MessageAttachmentItemDto
                        {
                            FileName = m.AttachmentOriginalName ?? "file",
                            ContentType = m.AttachmentContentType ?? "application/octet-stream",
                            SizeBytes = m.AttachmentSizeBytes ?? 0,
                        },
                })
                .ToList();

            if (rows.Count > 0)
            {
                var maxAt = rows.Max(m => m.SentAt);
                var state = await db.ChatThreadReadStates.FindAsync(me, partnerUserId);
                if (state is null)
                {
                    db.ChatThreadReadStates.Add(
                        new ChatThreadReadState
                        {
                            ReaderUserId = me,
                            PartnerUserId = partnerUserId,
                            LastReadAt = maxAt,
                        }
                    );
                }
                else if (maxAt > state.LastReadAt)
                {
                    state.LastReadAt = maxAt;
                }

                await db.SaveChangesAsync();
            }

            return Results.Ok(dtos);
        }
    )
    .RequireAuthorization()
    .WithName("ChatThread");

app.MapGet(
        "/api/me/chats/attachments/{messageId:guid}",
        async (Guid messageId, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var me = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(me))
                return Results.Unauthorized();

            var m = await db.DirectMessages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == messageId);
            if (m is null || string.IsNullOrEmpty(m.AttachmentStoredName))
                return Results.NotFound();

            if (m.SenderUserId != me && m.RecipientUserId != me)
                return Results.Forbid();

            var path = Path.Combine(dataDir, "chat-attachments", m.Id.ToString("D"), m.AttachmentStoredName);
            if (!File.Exists(path))
                return Results.NotFound();

            var fileName = m.AttachmentOriginalName ?? "download";
            var contentType = m.AttachmentContentType ?? "application/octet-stream";
            return Results.File(path, contentType, fileDownloadName: fileName);
        }
    )
    .RequireAuthorization()
    .WithName("ChatAttachmentDownload");

app.MapPost(
        "/api/me/chats/{partnerUserId}/attachments",
        async (
            string partnerUserId,
            HttpRequest request,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> users,
            AppDbContext db,
            IHubContext<ChatHub> hubContext
        ) =>
        {
            var me = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(me))
                return Results.Unauthorized();

            partnerUserId = partnerUserId.Trim();
            if (string.IsNullOrEmpty(partnerUserId) || partnerUserId == me)
                return Results.BadRequest(new { error = "Invalid recipient." });

            if (await users.FindByIdAsync(partnerUserId) is null)
                return Results.BadRequest(new { error = "User not found." });

            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "Use multipart/form-data with a file field." });

            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "Missing file." });

            var err = ChatAttachmentRules.ValidateFile(file, out var ext);
            if (err is not null)
                return Results.BadRequest(new { error = err });

            var original = ChatAttachmentRules.SanitizeOriginalName(file.FileName);
            var caption = (form["caption"].ToString() ?? "").Trim();
            string body;
            if (caption.Length > 0)
            {
                if (caption.Length > 4000)
                    return Results.BadRequest(new { error = "Caption must be at most 4000 characters." });
                if (ChatMessageFilters.IsHiddenAutoChatMessage(caption))
                    return Results.BadRequest(new { error = "Caption not allowed." });
                body = caption;
            }
            else
            {
                var prefix = "📎 ";
                var maxName = 4000 - prefix.Length;
                var namePart = original.Length > maxName ? original[..maxName] : original;
                body = prefix + namePart;
            }

            var storedName = Guid.NewGuid().ToString("N") + ext;
            var msg = new DirectMessage
            {
                Id = Guid.NewGuid(),
                SenderUserId = me,
                RecipientUserId = partnerUserId,
                Body = body,
                SentAt = DateTimeOffset.UtcNow,
                AttachmentStoredName = storedName,
                AttachmentOriginalName = original,
                AttachmentContentType = ChatAttachmentRules.GuessContentType(ext, file.ContentType),
                AttachmentSizeBytes = file.Length,
            };

            db.DirectMessages.Add(msg);
            await db.SaveChangesAsync();

            var dir = Path.Combine(dataDir, "chat-attachments", msg.Id.ToString("D"));
            try
            {
                Directory.CreateDirectory(dir);
                var fullPath = Path.Combine(dir, storedName);
                await using (var fs = File.Create(fullPath))
                {
                    await file.CopyToAsync(fs);
                }
            }
            catch
            {
                db.DirectMessages.Remove(msg);
                await db.SaveChangesAsync();
                return Results.Problem("Could not save the file.");
            }

            var payload = new
            {
                id = msg.Id.ToString(),
                senderUserId = me,
                recipientUserId = partnerUserId,
                body = msg.Body,
                sentAt = msg.SentAt,
                attachment = new
                {
                    fileName = msg.AttachmentOriginalName,
                    contentType = msg.AttachmentContentType,
                    sizeBytes = msg.AttachmentSizeBytes ?? 0,
                },
            };
            await hubContext.Clients.Users(me, partnerUserId).SendAsync("ReceiveMessage", payload);

            return Results.Ok(new { id = msg.Id });
        }
    )
    .DisableAntiforgery()
    .RequireAuthorization()
    .WithName("ChatAttachmentUpload");

app.MapPost(
        "/api/me/messages",
        async (SendMessageRequest req, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var me = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(me))
                return Results.Unauthorized();

            string? recipientId = req.RecipientUserId;
            if (!string.IsNullOrWhiteSpace(req.ProviderProfileId))
            {
                var prof = await db.ProviderProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == req.ProviderProfileId);
                if (prof?.UserId is null)
                    return Results.BadRequest(new { error = "Provider is not a registered user or profile not found." });
                var providerUserIds = await ProviderBrowseRules.GetProviderRoleUserIdsAsync(db);
                if (!providerUserIds.Contains(prof.UserId))
                    return Results.BadRequest(new { error = "That profile is not a registered service provider." });
                recipientId = prof.UserId;
            }

            if (string.IsNullOrWhiteSpace(recipientId) || recipientId == me)
                return Results.BadRequest(new { error = "Specify RecipientUserId or ProviderProfileId." });

            var msg = new DirectMessage
            {
                Id = Guid.NewGuid(),
                SenderUserId = me,
                RecipientUserId = recipientId,
                Body = req.Body.Trim(),
                SentAt = DateTimeOffset.UtcNow,
            };
            db.DirectMessages.Add(msg);
            await db.SaveChangesAsync();
            return Results.Ok(new { id = msg.Id });
        }
    )
    .RequireAuthorization()
    .WithName("SendMessage");

app.MapPost(
        "/api/me/service-requests",
        async (CreateServiceRequestDto req, ClaimsPrincipal principal, UserManager<ApplicationUser> users, AppDbContext db, IHubContext<ChatHub> hubContext) =>
        {
            var me = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(me))
                return Results.Unauthorized();

            var customer = await users.FindByIdAsync(me);
            if (customer is null)
                return Results.Unauthorized();
            if (!await users.IsInRoleAsync(customer, "Customer"))
                return Results.Json(
                    new { error = "Only customer accounts can book services. Provider accounts can browse but not book." },
                    statusCode: StatusCodes.Status403Forbidden
                );

            var body = req.Body.Trim();
            if (string.IsNullOrWhiteSpace(body))
                return Results.BadRequest(new { error = "Message body is required." });

            if (ChatMessageFilters.IsHiddenAutoChatMessage(body))
                return Results.BadRequest(new { error = "Message text is not allowed for chat." });

            var prof = await db.ProviderProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == req.ProviderProfileId);
            if (prof?.UserId is null)
                return Results.BadRequest(new { error = "Provider profile not found." });
            var providerUserIds = await ProviderBrowseRules.GetProviderRoleUserIdsAsync(db);
            if (!providerUserIds.Contains(prof.UserId))
                return Results.BadRequest(new { error = "That profile is not a registered service provider." });
            if (prof.UserId == me)
                return Results.BadRequest(new { error = "You cannot send a booking request to your own profile." });

            var sr = new ServiceRequest
            {
                Id = Guid.NewGuid(),
                CustomerUserId = me,
                ProviderUserId = prof.UserId,
                ProviderProfileId = prof.Id,
                Status = "pending",
                Body = body,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.ServiceRequests.Add(sr);

            var chatMsg = new DirectMessage
            {
                Id = Guid.NewGuid(),
                SenderUserId = me,
                RecipientUserId = prof.UserId,
                Body = body,
                SentAt = DateTimeOffset.UtcNow,
            };
            db.DirectMessages.Add(chatMsg);

            await db.SaveChangesAsync();

            var customerUser = await users.FindByIdAsync(me);
            var bookingRequest = new ServiceRequestDto
            {
                Id = sr.Id.ToString(),
                CustomerUserId = me,
                CustomerName = customerUser?.FullName ?? customerUser?.Email ?? "Customer",
                Body = sr.Body,
                Status = sr.Status,
                CreatedAt = sr.CreatedAt,
                RespondedAt = sr.RespondedAt,
            };

            var payload = new
            {
                id = chatMsg.Id.ToString(),
                senderUserId = me,
                recipientUserId = prof.UserId,
                body = chatMsg.Body,
                sentAt = chatMsg.SentAt,
            };
            try
            {
                await hubContext.Clients.User(prof.UserId).SendAsync("NewBookingRequest", bookingRequest);
                await hubContext.Clients.Users(me, prof.UserId).SendAsync("ReceiveMessage", payload);
            }
            catch (Exception)
            {
                // Booking + chat row are already saved; hub notify is best-effort only.
            }

            return Results.Ok(new { id = sr.Id.ToString(), providerUserId = prof.UserId });
        }
    )
    .RequireAuthorization()
    .WithName("CreateServiceRequest");

app.MapGet(
        "/api/me/customer/booking-notifications",
        async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            var me = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(me))
                return Results.Unauthorized();

            // SQLite cannot ORDER BY DateTimeOffset; fetch then sort in memory (cap via Take after sort).
            var raw = await (
                from r in db.ServiceRequests.AsNoTracking()
                join p in db.ProviderProfiles.AsNoTracking() on r.ProviderProfileId equals p.Id
                where r.CustomerUserId == me
                    && (r.Status == "accepted" || r.Status == "rejected")
                select new { r, p }
            ).ToListAsync();

            var rows = raw
                .OrderByDescending(x => x.r.RespondedAt ?? x.r.CreatedAt)
                .Take(50)
                .Select(x => new CustomerBookingNotificationDto
                {
                    Id = x.r.Id.ToString(),
                    Status = x.r.Status,
                    ProviderDisplayName = x.p.DisplayName ?? "Provider",
                    ProviderProfileId = x.r.ProviderProfileId,
                    ProviderUserId = x.p.UserId ?? "",
                    Body = x.r.Body,
                    CreatedAt = x.r.CreatedAt,
                    RespondedAt = x.r.RespondedAt,
                })
                .ToList();

            return Results.Ok(rows);
        }
    )
    .RequireAuthorization()
    .WithName("ListCustomerBookingNotifications");

app.MapGet(
        "/api/me/provider/service-requests",
        async (ClaimsPrincipal principal, UserManager<ApplicationUser> users, AppDbContext db) =>
        {
            var me = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(me))
                return Results.Unauthorized();

            // Tie history to the provider account (UserId), not only the current profile row id.
            var rows = (await db.ServiceRequests.AsNoTracking()
                    .Where(r => r.ProviderUserId == me)
                    .ToListAsync())
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            var list = new List<ServiceRequestDto>(rows.Count);
            foreach (var r in rows)
            {
                var cu = await users.FindByIdAsync(r.CustomerUserId);
                list.Add(
                    new ServiceRequestDto
                    {
                        Id = r.Id.ToString(),
                        CustomerUserId = r.CustomerUserId,
                        CustomerName = cu?.FullName ?? cu?.Email ?? "Customer",
                        Body = r.Body,
                        Status = r.Status,
                        CreatedAt = r.CreatedAt,
                        RespondedAt = r.RespondedAt,
                    }
                );
            }

            return Results.Ok(list);
        }
    )
    .RequireAuthorization(policy => policy.RequireRole("Provider"))
    .WithName("ListProviderServiceRequests");

app.MapPatch(
        "/api/me/provider/service-requests/{id:guid}",
        async (
            Guid id,
            ServiceRequestStatusDto req,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> users,
            AppDbContext db,
            IHubContext<ChatHub> hubContext
        ) =>
        {
            var me = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(me))
                return Results.Unauthorized();

            var sr = await db.ServiceRequests.FirstOrDefaultAsync(r => r.Id == id && r.ProviderUserId == me);
            if (sr is null)
                return Results.NotFound();

            var profile = await db.ProviderProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == me);

            if (!string.Equals(sr.Status, "pending", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = "This request is no longer pending." });

            var st = (req.Status ?? "").Trim().ToLowerInvariant();
            if (st is not ("accepted" or "rejected"))
                return Results.BadRequest(new { error = "Status must be accepted or rejected." });

            sr.Status = st;
            sr.RespondedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync();

            var providerDisplayName = profile?.DisplayName ?? "Provider";
            var bookingPayload = new
            {
                id = sr.Id.ToString(),
                status = sr.Status,
                providerDisplayName,
                providerProfileId = sr.ProviderProfileId,
                providerUserId = sr.ProviderUserId,
                body = sr.Body,
                createdAt = sr.CreatedAt,
                respondedAt = sr.RespondedAt,
            };
            try
            {
                await hubContext.Clients.User(sr.CustomerUserId).SendAsync("BookingResponse", bookingPayload);
            }
            catch (Exception)
            {
                // Status is already persisted; real-time notify is best-effort.
            }

            var customer = await users.FindByIdAsync(sr.CustomerUserId);
            return Results.Ok(
                new ServiceRequestDto
                {
                    Id = sr.Id.ToString(),
                    CustomerUserId = sr.CustomerUserId,
                    CustomerName = customer?.FullName ?? customer?.Email ?? "Customer",
                    Body = sr.Body,
                    Status = sr.Status,
                    CreatedAt = sr.CreatedAt,
                    RespondedAt = sr.RespondedAt,
                }
            );
        }
    )
    .RequireAuthorization(policy => policy.RequireRole("Provider"))
    .WithName("UpdateServiceRequestStatus");

app.MapAdminApi();

app.Run();
