using System.ComponentModel.DataAnnotations;

namespace ArtisanApi.Models;

public sealed class RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, MinLength(8)]
    public string Password { get; set; } = "";

    [Required, MinLength(2)]
    public string FullName { get; set; } = "";

    public string? Phone { get; set; }

    /// <summary>customer or provider</summary>
    [Required]
    public string Role { get; set; } = "";
}

public sealed class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}

public sealed class ForgotPasswordStartRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";
}

public sealed class ForgotPasswordVerifyOtpRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, MinLength(4), MaxLength(32)]
    public string Otp { get; set; } = "";
}

public sealed class ForgotPasswordResetRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    public string Token { get; set; } = "";

    [Required, MinLength(8)]
    public string NewPassword { get; set; } = "";
}

/// <summary>GIS credential JWT from the browser; optional role only for brand-new accounts.</summary>
public sealed class GoogleSignInRequest
{
    [Required, MinLength(10)]
    public string IdToken { get; set; } = "";

    /// <summary>customer or provider — used only when creating a new user. Existing users keep their roles.</summary>
    public string? Role { get; set; }
}

public sealed class AuthResponse
{
    public string AccessToken { get; set; } = "";
    public int ExpiresInSeconds { get; set; }
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "";
    public string? ProviderProfileId { get; set; }
}

public sealed class MeResponse
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string? ProfilePhotoUrl { get; set; }
    public bool LinkedGoogle { get; set; }
    public string Role { get; set; } = "";
    public string? ProviderProfileId { get; set; }
}

/// <summary>Customer (or any user) identity fields stored on ApplicationUser.</summary>
public sealed class UpdateAccountRequest
{
    [Required, MinLength(2)]
    public string FullName { get; set; } = "";

    public string? Phone { get; set; }

    public string? ProfilePhotoUrl { get; set; }
}

public sealed class UpdateProviderProfileRequest
{
    [MinLength(2)]
    public string? DisplayName { get; set; }
    public string? Trade { get; set; }
    public string? City { get; set; }
    public string? Bio { get; set; }
    public string? PhotoUrl { get; set; }
    public string? WorkPhotosJson { get; set; }
    public decimal? PriceAmount { get; set; }
    public string? PriceUnit { get; set; }
    public int? ExperienceYears { get; set; }
    public bool? VisibleInSearch { get; set; }
}

/// <summary>Full save from client (PUT) — replaces profile fields.</summary>
public sealed class ProviderProfileSaveDto
{
    [Required, MinLength(2)]
    public string DisplayName { get; set; } = "";

    public string Trade { get; set; } = "";
    public string City { get; set; } = "";
    public string Bio { get; set; } = "";
    public string? PhotoUrl { get; set; }
    public string? WorkPhotosJson { get; set; }
    public decimal? PriceAmount { get; set; }
    public string? PriceUnit { get; set; }
    public int? ExperienceYears { get; set; }
    public bool VisibleInSearch { get; set; } = true;
}

public sealed class PostRatingRequest
{
    [Range(1, 5)]
    public int Score { get; set; }
}

public sealed class SendMessageRequest
{
    /// <summary>Send to user by id (either this or ProviderProfileId).</summary>
    public string? RecipientUserId { get; set; }

    /// <summary>Send to registered provider by public profile id.</summary>
    public string? ProviderProfileId { get; set; }

    [Required, MinLength(1), MaxLength(4000)]
    public string Body { get; set; } = "";
}
