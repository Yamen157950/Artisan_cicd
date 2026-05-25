namespace ArtisanApi.Data.Entities;

public sealed class ProviderProfile
{
    public string Id { get; set; } = "";
    public string? UserId { get; set; }
    public Models.ApplicationUser? User { get; set; }

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
    public bool IsSeededDemo { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
}
