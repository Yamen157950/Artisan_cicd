namespace ArtisanApi.Data.Entities;

public sealed class ProviderRating
{
    public Guid Id { get; set; }
    public string ProviderProfileId { get; set; } = "";
    public ProviderProfile ProviderProfile { get; set; } = null!;
    public string? CustomerUserId { get; set; }
    public int Score { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
