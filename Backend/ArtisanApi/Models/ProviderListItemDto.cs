namespace ArtisanApi.Models;

/// <summary>Matches frontend provider cards (camelCase in JSON).</summary>
public class ProviderListItemDto
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Trade { get; init; } = "";
    public string City { get; init; } = "";
    public string Bio { get; init; } = "";
    public double? Rating { get; init; }
    public string PhotoUrl { get; init; } = "";
    public string? ProfileUrl { get; init; }
    public string? JoinedAt { get; init; }
    public string? PriceLabel { get; init; }
    public string? ExperienceLabel { get; init; }
    public bool Searchable { get; init; } = true;
}
