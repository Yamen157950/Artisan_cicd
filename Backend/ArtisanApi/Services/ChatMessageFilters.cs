namespace ArtisanApi.Services;

public static class ChatMessageFilters
{
    public static bool IsHiddenAutoChatMessage(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return false;
        var b = body.Trim();
        return b.StartsWith("[Artisan]", StringComparison.OrdinalIgnoreCase)
            || b.StartsWith("Booking request", StringComparison.OrdinalIgnoreCase)
            || b.Contains("Preferred date:", StringComparison.OrdinalIgnoreCase);
    }
}
