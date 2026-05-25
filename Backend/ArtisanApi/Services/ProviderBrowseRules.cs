using ArtisanApi.Data;
using ArtisanApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArtisanApi.Services;

/// <summary>
/// Public browse/search should only list real providers (Provider role). Customer accounts never appear,
/// even if a ProviderProfile row exists. Seeded demo cards (no UserId) stay listable for academics.
/// </summary>
public static class ProviderBrowseRules
{
    public const string ProviderRoleName = "Provider";

    public static bool IsSeededAnonymousDemo(ProviderProfile p) =>
        p.IsSeededDemo && string.IsNullOrEmpty(p.UserId);

    public static async Task<HashSet<string>> GetProviderRoleUserIdsAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var ids = await (
            from ur in db.UserRoles.AsNoTracking()
            join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where r.Name == ProviderRoleName
            select ur.UserId
        ).ToListAsync(cancellationToken);
        return ids.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Profile may appear on GET /api/providers when VisibleInSearch is already true.</summary>
    public static bool IsPublicBrowseCard(ProviderProfile p, HashSet<string> providerRoleUserIds) =>
        IsSeededAnonymousDemo(p)
        || (!string.IsNullOrEmpty(p.UserId) && providerRoleUserIds.Contains(p.UserId));
}
