using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ArtisanApi.Models;
using Microsoft.IdentityModel.Tokens;

namespace ArtisanApi.Services;

public sealed class JwtTokenService(IConfiguration config)
{
    public string CreateAccessToken(ApplicationUser user, IEnumerable<string> roles, string? providerProfileId)
    {
        var jwtSection = config.GetSection("Jwt");
        var key = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var issuer = jwtSection["Issuer"] ?? "ArtisanApi";
        var audience = jwtSection["Audience"] ?? "ArtisanFrontend";
        var minutes = int.TryParse(jwtSection["ExpireMinutes"], out var m) ? m : 10080; // 7 days default

        var roleList = roles.Distinct().ToList();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.FullName ?? user.UserName ?? ""),
        };
        foreach (var r in roleList)
            claims.Add(new Claim(ClaimTypes.Role, r));

        if (!string.IsNullOrEmpty(providerProfileId))
            claims.Add(new Claim("provider_profile_id", providerProfileId));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(minutes),
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public int GetExpireSeconds()
    {
        var jwtSection = config.GetSection("Jwt");
        var minutes = int.TryParse(jwtSection["ExpireMinutes"], out var m) ? m : 10080;
        return minutes * 60;
    }
}
