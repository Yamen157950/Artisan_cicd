using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ArtisanApi.Services;

/// <summary>Sends password-reset OTP via SendGrid HTTP API when configured, otherwise SMTP (<see cref="SmtpEmailSender"/>).</summary>
public sealed class PasswordResetMailer
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly SendGridEmailOptions _sendGrid;
    private readonly SmtpEmailSender _smtp;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<PasswordResetMailer> _log;

    public PasswordResetMailer(
        IOptions<SendGridEmailOptions> sendGridOptions,
        SmtpEmailSender smtp,
        IHttpClientFactory httpFactory,
        ILogger<PasswordResetMailer> log
    )
    {
        _sendGrid = sendGridOptions.Value;
        _smtp = smtp;
        _httpFactory = httpFactory;
        _log = log;
    }

    public bool CanSend =>
        (!string.IsNullOrWhiteSpace(_sendGrid.ApiKey) && !string.IsNullOrWhiteSpace(_sendGrid.FromEmail)) || _smtp.IsConfigured;

    public async Task<bool> TrySendPasswordResetOtpAsync(string toEmail, string otp, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_sendGrid.ApiKey) && !string.IsNullOrWhiteSpace(_sendGrid.FromEmail))
        {
            try
            {
                var ok = await TrySendViaSendGridAsync(toEmail, otp, cancellationToken).ConfigureAwait(false);
                if (ok)
                    return true;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "SendGrid send failed for {To}; falling back to SMTP if configured.", toEmail);
            }
        }

        return await _smtp.TrySendPasswordResetOtpAsync(toEmail, otp, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> TrySendViaSendGridAsync(string toEmail, string otp, CancellationToken cancellationToken)
    {
        var bodyText =
            $"Your Artisan password reset code is: {otp}\n\n"
            + "It expires in 15 minutes. If you did not request this, you can ignore this email.\n";

        var payload = new
        {
            personalizations = new[] { new { to = new[] { new { email = toEmail } } } },
            from = new { email = _sendGrid.FromEmail!.Trim(), name = _sendGrid.FromName.Trim() },
            subject = "Your Artisan verification code",
            content = new[] { new { type = "text/plain", value = bodyText } },
        };

        var json = JsonSerializer.Serialize(payload, JsonOpts);
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _sendGrid.ApiKey!.Trim());

        var client = _httpFactory.CreateClient();
        using var resp = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        if (resp.IsSuccessStatusCode)
            return true;

        var err = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        _log.LogWarning("SendGrid returned {Status}: {Body}", (int)resp.StatusCode, err);
        return false;
    }
}
