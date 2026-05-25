using Microsoft.AspNetCore.Identity;

namespace ArtisanApi.Models;

public sealed class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public string? Phone { get; set; }

    /// <summary>Optional profile photo (URL or data URL) for customer account UI.</summary>
    public string? ProfilePhotoUrl { get; set; }
}
