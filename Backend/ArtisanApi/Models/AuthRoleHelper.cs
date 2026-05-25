namespace ArtisanApi.Models;

/// <summary>Single role for auth UI: Admin, then Moderator, then Provider, then Customer.</summary>
public static class AuthRoleHelper
{
    public static string PublicPrimaryRole(IEnumerable<string> roles)
    {
        var set = new HashSet<string>(roles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        if (set.Contains("Admin"))
            return "Admin";
        if (set.Contains("Moderator"))
            return "Moderator";
        if (set.Contains("Provider"))
            return "Provider";
        return "Customer";
    }
}
