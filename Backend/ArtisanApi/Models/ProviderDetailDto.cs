namespace ArtisanApi.Models;

/// <summary>Public provider detail — includes portfolio JSON for profile page.</summary>
public sealed class ProviderDetailDto : ProviderListItemDto
{
    public string? WorkPhotosJson { get; set; }
    public decimal? PriceAmount { get; set; }
    public string? PriceUnit { get; set; }
    public int? ExperienceYears { get; set; }
}
