namespace ArtisanApi.Models;

public sealed record HerafiChatRequest(string? Message);

public sealed record HerafiChatNavigate(string? Type, string? Trade, string? City, string? Q, string? Sort, int? MinExperience = null, bool Similar = false);

public sealed record HerafiChatTopPick(string Id, string DisplayName, double? Rating, int RatingCount, int? ExperienceYears = null);

public sealed record HerafiChatResponse(
    string Reply,
    HerafiChatNavigate? Navigate,
    int? RedirectDelayMs,
    HerafiChatTopPick? TopPick = null,
    bool SimilarResults = false
);

public sealed record HerafiChatResult(
    string Reply,
    HerafiChatNavigate Navigate,
    int RedirectDelayMs,
    HerafiChatTopPick? TopPick = null,
    bool SimilarResults = false
);