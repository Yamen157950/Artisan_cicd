using ArtisanApi.Data.Entities;
using ArtisanApi.Models;

namespace ArtisanApi.Services;

public static class ProviderMapper
{
    public static string FormatPriceLabel(decimal? amount, string? unit)
    {
        if (amount is null || amount < 0)
            return "";
        var u = unit?.ToLowerInvariant() switch
        {
            "day" => "day",
            "week" => "week",
            _ => "hour",
        };
        var n = (decimal)amount;
        var num = n % 1 == 0 ? ((int)n).ToString() : n.ToString("0.##");
        return $"{num} JOD / {u}";
    }

    public static string FormatExperienceLabel(int? years)
    {
        if (years is null or < 0)
            return "";
        var y = years.Value;
        return y == 1 ? "Experience: +1 year" : $"Experience: +{y} years";
    }

    public static ProviderListItemDto ToListItem(ProviderProfile p, double? avgRating)
    {
        return new ProviderListItemDto
        {
            Id = p.Id,
            DisplayName = p.DisplayName,
            Trade = p.Trade,
            City = p.City,
            Bio = p.Bio,
            Rating = avgRating,
            PhotoUrl = p.PhotoUrl ?? "",
            ProfileUrl = null,
            JoinedAt = p.JoinedAt.ToString("yyyy-MM-dd"),
            PriceLabel = string.IsNullOrEmpty(FormatPriceLabel(p.PriceAmount, p.PriceUnit)) ? null : FormatPriceLabel(p.PriceAmount, p.PriceUnit),
            ExperienceLabel = string.IsNullOrEmpty(FormatExperienceLabel(p.ExperienceYears)) ? null : FormatExperienceLabel(p.ExperienceYears),
            Searchable = p.VisibleInSearch,
        };
    }

    public static ProviderDetailDto ToDetail(ProviderProfile p, double? avgRating)
    {
        var list = ToListItem(p, avgRating);
        return new ProviderDetailDto
        {
            Id = list.Id,
            DisplayName = list.DisplayName,
            Trade = list.Trade,
            City = list.City,
            Bio = list.Bio,
            Rating = list.Rating,
            PhotoUrl = list.PhotoUrl,
            ProfileUrl = list.ProfileUrl,
            JoinedAt = list.JoinedAt,
            PriceLabel = list.PriceLabel,
            ExperienceLabel = list.ExperienceLabel,
            Searchable = list.Searchable,
            WorkPhotosJson = p.WorkPhotosJson,
            PriceAmount = p.PriceAmount,
            PriceUnit = p.PriceUnit,
            ExperienceYears = p.ExperienceYears,
        };
    }
}
