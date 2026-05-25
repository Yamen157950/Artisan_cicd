namespace ArtisanApi.Services;



public sealed class SmtpEmailOptions
{
    public const string SectionName = "Smtp";

    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? User { get; set; }
    public string? Password { get; set; }
    public string? FromEmail { get; set; }
    public string FromName { get; set; } = "Artisan";

    /// <summary>Local tools (smtp4dev, Papercut): no SMTP auth; requires <see cref="FromEmail"/> for envelope.</summary>
    public bool AllowAnonymous { get; set; }

    /// <summary>Auto (default), StartTls, SslOnConnect, None — passed to MailKit.</summary>
    public string SecureSocket { get; set; } = "Auto";
}
