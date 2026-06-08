namespace ArtisanApi.Models;

public sealed class HerafiTrainingRecord
{
    public string Sentence { get; set; } = "";
    public string SentenceNorm { get; set; } = "";
    public string Language { get; set; } = "";
    public string? Trade { get; set; }
    public string? City { get; set; }
    public string Sort { get; set; } = "rating";
    public double? MinRating { get; set; }
    public int? MinExperienceYears { get; set; }
    public string IntentLabel { get; set; } = "";
}

public sealed class HerafiTrainingIntent
{
    public string? Trade { get; init; }
    public string? City { get; init; }
    public string Sort { get; init; } = "rating";
    public double? MinRating { get; init; }
    public int? MinExperienceYears { get; init; }
    public string? MatchedSentence { get; init; }
    public double MatchScore { get; init; }
}
