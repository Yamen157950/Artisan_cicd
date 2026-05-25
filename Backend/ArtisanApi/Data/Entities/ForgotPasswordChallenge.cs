namespace ArtisanApi.Data.Entities;



/// <summary>Short-lived email OTP before issuing an Identity password reset token.</summary>

public sealed class ForgotPasswordChallenge

{

    public Guid Id { get; set; }

    public string EmailNormalized { get; set; } = "";

    public string OtpCode { get; set; } = "";

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? ConsumedAtUtc { get; set; }

}


