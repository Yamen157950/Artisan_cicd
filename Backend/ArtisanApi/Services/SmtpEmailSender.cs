using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ArtisanApi.Services;

/// <summary>Sends transactional mail when <see cref="SmtpEmailOptions"/> is fully configured (e.g. Gmail + app password).</summary>
public sealed class SmtpEmailSender
{
    private readonly SmtpEmailOptions _opts;
    private readonly ILogger<SmtpEmailSender> _log;

    public SmtpEmailSender(IOptions<SmtpEmailOptions> options, ILogger<SmtpEmailSender> log)
    {
        _opts = options.Value;
        _log = log;
    }

    public bool IsConfigured
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_opts.Host))
                return false;
            if (_opts.AllowAnonymous)
                return !string.IsNullOrWhiteSpace(_opts.FromEmail) || !string.IsNullOrWhiteSpace(_opts.User);
            return !string.IsNullOrWhiteSpace(_opts.User) && !string.IsNullOrWhiteSpace(_opts.Password);
        }
    }

    private static SecureSocketOptions ParseSecureSocket(string? raw)
    {
        var v = (raw ?? "Auto").Trim();
        if (v.Equals("None", StringComparison.OrdinalIgnoreCase))
            return SecureSocketOptions.None;
        if (v.Equals("StartTls", StringComparison.OrdinalIgnoreCase))
            return SecureSocketOptions.StartTls;
        if (v.Equals("SslOnConnect", StringComparison.OrdinalIgnoreCase))
            return SecureSocketOptions.SslOnConnect;
        return SecureSocketOptions.Auto;
    }

    /// <summary>Plain-text OTP email. Returns false if not configured or send failed.</summary>
    public async Task<bool> TrySendPasswordResetOtpAsync(string toEmail, string otp, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return false;

        var from = string.IsNullOrWhiteSpace(_opts.FromEmail)
            ? (_opts.User ?? "noreply@localhost").Trim()
            : _opts.FromEmail.Trim();

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_opts.FromName, from));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "Your Artisan verification code";
        message.Body = new TextPart("plain")
        {
            Text =
                $"Your Artisan password reset code is: {otp}\n\n"
                + "It expires in 15 minutes. If you did not request this, you can ignore this email.\n",
        };

        try
        {
            using var client = new SmtpClient();
            var secure = ParseSecureSocket(_opts.SecureSocket);
            await client.ConnectAsync(_opts.Host!, _opts.Port, secure, cancellationToken).ConfigureAwait(false);
            if (!_opts.AllowAnonymous)
                await client
                    .AuthenticateAsync(_opts.User!, _opts.Password!, cancellationToken)
                    .ConfigureAwait(false);
            await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
            await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "SMTP send failed for password reset to {To}", toEmail);
            return false;
        }
    }
}
