using Google.Apis.Auth;

namespace ArtisanApi.Services;

public sealed class GoogleSignInService(IConfiguration configuration)
{
    private const string ConfigKey = "Google:ClientId";

    public string? GetClientId() => configuration[ConfigKey]?.Trim();

    public async Task<(GoogleJsonWebSignature.Payload? Payload, string? Error)> ValidateIdTokenAsync(string idToken)
    {
        var clientId = GetClientId();
        if (string.IsNullOrEmpty(clientId))
            return (null, "Google sign-in is not configured on the server (missing Google:ClientId).");

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] };
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            return (payload, null);
        }
        catch (InvalidJwtException)
        {
            return (null, "Invalid or expired Google credential.");
        }
    }
}
