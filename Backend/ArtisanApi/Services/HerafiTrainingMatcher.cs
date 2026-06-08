using System.Text.Json;
using System.Text.RegularExpressions;
using ArtisanApi.Models;

namespace ArtisanApi.Services;

/// <summary>
/// Matches user messages against the Excel training dataset (Arabic + English sheets).
/// </summary>
public sealed class HerafiTrainingMatcher
{
    private const double FuzzyThreshold = 0.72;
    private readonly List<HerafiTrainingRecord> _entries;
    private readonly Dictionary<string, HerafiTrainingRecord> _exact;

    public HerafiTrainingMatcher(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "herafi-training.json");
        if (!File.Exists(path))
        {
            _entries = [];
            _exact = new Dictionary<string, HerafiTrainingRecord>(StringComparer.Ordinal);
            return;
        }

        var json = File.ReadAllText(path);
        _entries =
            JsonSerializer.Deserialize<List<HerafiTrainingRecord>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            ) ?? [];

        _exact = _entries
            .GroupBy(e => e.SentenceNorm, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    }

    public int Count => _entries.Count;

    public bool TryMatch(string userMessage, out HerafiTrainingIntent intent)
    {
        intent = null!;
        var norm = Normalize(userMessage);
        if (string.IsNullOrWhiteSpace(norm))
            return false;

        if (_exact.TryGetValue(norm, out var exact))
        {
            intent = ToIntent(exact, 1.0);
            return true;
        }

        var userTokens = Tokenize(norm);
        HerafiTrainingRecord? best = null;
        var bestScore = 0.0;

        foreach (var entry in _entries)
        {
            var score = Jaccard(userTokens, Tokenize(entry.SentenceNorm));
            if (score > bestScore)
            {
                bestScore = score;
                best = entry;
            }
        }

        if (best is null || bestScore < FuzzyThreshold)
            return false;

        intent = ToIntent(best, bestScore);
        return true;
    }

    private static HerafiTrainingIntent ToIntent(HerafiTrainingRecord record, double score) =>
        new()
        {
            Trade = record.Trade,
            City = record.City,
            Sort = string.IsNullOrWhiteSpace(record.Sort) ? "rating" : record.Sort,
            MinRating = record.MinRating,
            MinExperienceYears = record.MinExperienceYears,
            MatchedSentence = record.Sentence,
            MatchScore = score,
        };

    private static string Normalize(string text)
    {
        var s = text.ToLowerInvariant().Trim();
        s = Regex.Replace(s, @"[^\w\s\u0600-\u06FF]", " ");
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    private static HashSet<string> Tokenize(string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);

    private static double Jaccard(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0)
            return 0;
        var inter = a.Intersect(b).Count();
        var union = a.Union(b).Count();
        return union == 0 ? 0 : (double)inter / union;
    }
}
