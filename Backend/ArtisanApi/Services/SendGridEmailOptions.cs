namespace ArtisanApi.Services;

/// <summary>Optional SendGrid Web API — set ApiKey + FromEmail (verified sender) to deliver OTP without SMTP.</summary>
public sealed class SendGridEmailOptions
{
    public const string SectionName = "SendGrid";

    public string? ApiKey { get; set; }
    public string? FromEmail { get; set; }
    public string FromName { get; set; } = "Artisan";
}
