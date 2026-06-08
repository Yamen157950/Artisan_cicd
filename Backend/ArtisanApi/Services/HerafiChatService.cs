using System.Text.RegularExpressions;
using ArtisanApi.Data;
using ArtisanApi.Data.Entities;
using ArtisanApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ArtisanApi.Services;

public sealed class HerafiChatService(AppDbContext db, HerafiTrainingMatcher training)
{
    private static readonly Dictionary<string, string[]> ServiceKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Electrical"] = ["electrician", "electric", "electricity", "wiring", "wire", "power", "outlet", "كهربائي", "كهرباء", "كهربية"],
        ["Plumbing"] = ["plumber", "plumbing", "pipe", "water leak", "leak", "drain", "سباك", "سباكة", "مياه", "تسريب"],
        ["Painting"] = ["painter", "paint", "painting", "wall", "color", "colour", "دهان", "دهانة", "طلاء", "صبغ"],
        ["Carpentry"] = ["carpenter", "carpentry", "wood", "furniture", "door", "نجار", "نجارة", "خشب", "أثاث"],
        ["Cleaning"] = ["cleaner", "cleaning", "clean", "mop", "sweep", "عامل نظافة", "تنظيف", "نظافة"],
        ["HVAC"] = ["ac", "air condition", "hvac", "cooling", "cool", "فني تكييف", "تكييف", "مكيف"],
        ["Barber"] = ["barber", "haircut", "salon", "حلاق", "حلاقة"],
        ["Gardening"] = ["gardener", "garden", "landscap", "بستان", "حديقة", "زراعة"],
    };

    private static readonly Dictionary<string, string[]> CityKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Amman"] = ["amman", "عمان"],
        ["Irbid"] = ["irbid", "إربد", "اربد"],
        ["Zarqa"] = ["zarqa", "الزرقاء", "زرقاء"],
        ["Aqaba"] = ["aqaba", "العقبة", "عقبة"],
        ["Jerash"] = ["jerash", "جرش"],
        ["Ajloun"] = ["ajloun", "عجلون"],
        ["Salt"] = ["salt", "السلط", "سلط"],
        ["Madaba"] = ["madaba", "مادبا"],
        ["Mafraq"] = ["mafraq", "المفرق", "مفرق"],
        ["Karak"] = ["karak", "الكرk"],
        ["Tafilah"] = ["tafilah", "الطفيلة", "طفيلة"],
        ["Maan"] = ["maan", "ma'an", "معan"],
    };

    private static readonly Dictionary<string, string[]> SortKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["rating"] =
        [
            "best", "top", "rated", "rating", "ratings", "highest rated", "highly rated", "top rated",
            "recommended", "recommend", "popular", "trusted", "quality", "review", "reviews",
            "star", "stars", "5 star", "4 star",
            "أفضل", "تقييم", "تقييمات", "الأعلى", "موصى", "نجوم", "نجمة", "مراجعة", "مراجعات", "مميز",
        ],
        ["price"] = ["cheap", "cheapest", "lowest price", "affordable", "budget", "أرخص", "رخيص", "سعر", "اقتصادي"],
        ["experience"] =
        [
            "experienced", "most experienced", "senior", "veteran", "expert",
            "years experience", "years of experience", "years exp",
            "خبرة", "خبرات", "سنوات خبرة", "ذو خبرة", "محترف",
        ],
    };

    private static readonly Dictionary<string, string> TradeLabelEn = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Electrical"] = "Electricians",
        ["Plumbing"] = "Plumbers",
        ["Painting"] = "Painters",
        ["Carpentry"] = "Carpenters",
        ["Cleaning"] = "Cleaners",
        ["HVAC"] = "HVAC technicians",
        ["Barber"] = "Barbers",
        ["Gardening"] = "Gardeners",
    };

    private static readonly Dictionary<string, string> TradeLabelAr = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Electrical"] = "كهربائيين",
        ["Plumbing"] = "سباكين",
        ["Painting"] = "دهانين",
        ["Carpentry"] = "نجارين",
        ["Cleaning"] = "عمال تنظيف",
        ["HVAC"] = "فنيي تكييف",
        ["Barber"] = "حلاقين",
        ["Gardening"] = "بستانيين",
    };

    private static readonly Dictionary<string, string> CityLabelAr = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Amman"] = "عمان",
        ["Irbid"] = "إربد",
        ["Zarqa"] = "الزرقاء",
        ["Aqaba"] = "العقبة",
    };

    private sealed class ParsedIntent
    {
        public string? Trade { get; set; }
        public string? City { get; set; }
        public string? Sort { get; set; }
        public string? DetailId { get; set; }
        public double? MinRating { get; set; }
        public int? MinExperienceYears { get; set; }
    }

    private sealed record ProviderRow(ProviderProfile Profile, double? Rating, int RatingCount);

    public async Task<HerafiChatResult> GetResponseAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        var arabic = IsArabic(userMessage);
        var low = userMessage.ToLowerInvariant().Trim();

        if (IsGreeting(low))
            return new HerafiChatResult(HelpMessage(arabic), NoneNavigate(), 0);

        var intent = ResolveIntent(userMessage, low);
        if (!HasSearchIntent(intent))
        {
            var reply = arabic
                ? "لم أفهم طلبك. جرّب مثلاً: *دهان في عمان* أو *أرخص سباك*"
                : "I didn't understand that. Try something like: *painter in Amman* or *cheapest plumber*";
            return new HerafiChatResult(reply, NoneNavigate(), 0);
        }

        var navigate = BuildNavigateAction(intent, userMessage);
        var rows = await QueryProvidersAsync(intent, cancellationToken);
        var topPick = ToTopPick(rows.FirstOrDefault());

        if (!string.IsNullOrEmpty(intent.DetailId))
        {
            if (rows.Count == 0)
            {
                var missing = arabic
                    ? "لم أجد مزوداً بهذا المعرف. جرّب البحث بالمدينة أو نوع الخدمة."
                    : "No provider found with that ID. Try searching by city or trade.";
                return new HerafiChatResult(missing, NoneNavigate(), 0);
            }

            return new HerafiChatResult(
                FormatDetail(rows[0], arabic),
                navigate with { Type = "browse", Q = rows[0].Profile.Id },
                2000,
                ToTopPick(rows[0])
            );
        }

        if (rows.Count == 0)
        {
            var similar = await TrySimilarResultsAsync(intent, cancellationToken);
            if (similar is not null)
            {
                var (simRows, relaxedIntent) = similar.Value;
                var simTopPick = ToTopPick(simRows.FirstOrDefault());
                var simNavigate = BuildNavigateAction(relaxedIntent, userMessage, similar: true);
                var intro = SimilarResultsIntro(arabic);
                var preview = FormatListPreview(simRows, relaxedIntent, arabic, similar: true);
                var redirect = RedirectReply(relaxedIntent, simTopPick, arabic, similar: true);
                return new HerafiChatResult($"{intro}\n\n{preview}\n\n{redirect}", simNavigate, 1800, simTopPick, SimilarResults: true);
            }

            var empty = arabic
                ? "لم أجد أي نتائج أو نتائج مشابهة. جرّب خدمة أو مدينة أخرى."
                : "No exact or similar providers found. Try another trade or city.";
            return new HerafiChatResult(empty, NoneNavigate(), 0);
        }

        if (navigate.Type == "browse" && rows.Count > 0)
        {
            var preview = FormatListPreview(rows, intent, arabic);
            var redirect = RedirectReply(intent, topPick, arabic);
            return new HerafiChatResult($"{preview}\n\n{redirect}", navigate, 1600, topPick);
        }

        return new HerafiChatResult(FormatListPreview(rows, intent, arabic), navigate, 0, topPick);
    }

    private async Task<(List<ProviderRow> Rows, ParsedIntent RelaxedIntent)?> TrySimilarResultsAsync(
        ParsedIntent original,
        CancellationToken cancellationToken)
    {
        var attempts = BuildSimilarAttempts(original);
        foreach (var relaxed in attempts)
        {
            var rows = await QueryProvidersAsync(relaxed, cancellationToken);
            if (rows.Count > 0)
                return (rows, relaxed);
        }

        return null;
    }

    private static List<ParsedIntent> BuildSimilarAttempts(ParsedIntent original)
    {
        var attempts = new List<ParsedIntent>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(ParsedIntent intent)
        {
            var key = $"{intent.Trade}|{intent.City}|{intent.Sort}|{intent.MinRating}|{intent.MinExperienceYears}";
            if (seen.Add(key))
                attempts.Add(intent);
        }

        if (original.MinRating is > 0 || original.MinExperienceYears is > 0)
        {
            Add(new ParsedIntent
            {
                Trade = original.Trade,
                City = original.City,
                Sort = original.Sort ?? "rating",
            });
        }

        if (!string.IsNullOrEmpty(original.City))
        {
            Add(new ParsedIntent
            {
                Trade = original.Trade,
                Sort = original.Sort ?? "rating",
            });
        }

        if (!string.IsNullOrEmpty(original.Trade))
        {
            Add(new ParsedIntent { Trade = original.Trade, Sort = "rating" });
        }

        if (!string.IsNullOrEmpty(original.City))
        {
            Add(new ParsedIntent { City = original.City, Sort = "rating" });
        }

        Add(new ParsedIntent { Sort = "rating" });
        return attempts;
    }

    private static string SimilarResultsIntro(bool arabic) =>
        arabic
            ? "⚠️ **لم أجد نتائج مطابقة تماماً، لكن إليك نتائج مشابهة:**"
            : "⚠️ **No exact matches found, but here are similar results:**";

    private async Task<List<ProviderRow>> QueryProvidersAsync(ParsedIntent intent, CancellationToken cancellationToken)
    {
        var providerUserIds = await ProviderBrowseRules.GetProviderRoleUserIdsAsync(db, cancellationToken);
        var candidates = await db.ProviderProfiles.AsNoTracking().Where(p => p.VisibleInSearch).ToListAsync(cancellationToken);
        var list = candidates.Where(p => ProviderBrowseRules.IsPublicBrowseCard(p, providerUserIds)).ToList();

        if (!string.IsNullOrEmpty(intent.DetailId))
        {
            var id = intent.DetailId;
            list = list
                .Where(p =>
                    string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)
                    || p.Id.Contains(id, StringComparison.OrdinalIgnoreCase)
                    || p.DisplayName.Contains(id, StringComparison.OrdinalIgnoreCase)
                )
                .Take(1)
                .ToList();
        }
        else
        {
            if (!string.IsNullOrEmpty(intent.Trade))
                list = list.Where(p => string.Equals(p.Trade, intent.Trade, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!string.IsNullOrEmpty(intent.City))
                list = list.Where(p => string.Equals(p.City, intent.City, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var ratingStats = await db.ProviderRatings
            .AsNoTracking()
            .GroupBy(r => r.ProviderProfileId)
            .Select(g => new { g.Key, Avg = g.Average(x => (double)x.Score), Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => (Avg: x.Avg, Count: x.Count), cancellationToken);

        IEnumerable<ProviderRow> rows = list.Select(p =>
        {
            var stats = ratingStats.GetValueOrDefault(p.Id);
            return new ProviderRow(p, stats.Avg, stats.Count);
        });

        if (intent.MinRating is double minRating && minRating > 0)
        {
            rows = rows.Where(r => r.Rating.HasValue && r.Rating.Value >= minRating);
        }

        if (intent.MinExperienceYears is int minExp && minExp > 0)
        {
            rows = rows.Where(r => r.Profile.ExperienceYears.HasValue && r.Profile.ExperienceYears.Value >= minExp);
        }

        rows = intent.Sort switch
        {
            "price" => rows.OrderBy(r => r.Profile.PriceAmount ?? decimal.MaxValue)
                .ThenByDescending(r => r.Rating ?? 0),
            "experience" => rows
                .OrderByDescending(r => r.Profile.ExperienceYears ?? 0)
                .ThenByDescending(r => r.Rating ?? 0)
                .ThenByDescending(r => r.RatingCount),
            _ => rows
                .OrderByDescending(r => r.Rating.HasValue && r.Rating > 0 ? 1 : 0)
                .ThenByDescending(r => r.Rating ?? 0)
                .ThenByDescending(r => r.RatingCount)
                .ThenByDescending(r => r.Profile.ExperienceYears ?? 0)
                .ThenByDescending(r => r.Profile.JoinedAt),
        };

        return rows.Take(string.IsNullOrEmpty(intent.DetailId) ? 5 : 1).ToList();
    }

    private static string FormatListPreview(IReadOnlyList<ProviderRow> rows, ParsedIntent intent, bool arabic, bool similar = false)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(intent.Trade))
            parts.Add(arabic ? TradeLabelAr.GetValueOrDefault(intent.Trade, intent.Trade) : TradeLabelEn.GetValueOrDefault(intent.Trade, intent.Trade));
        if (!string.IsNullOrEmpty(intent.City))
            parts.Add(arabic ? CityLabelAr.GetValueOrDefault(intent.City, intent.City) : intent.City);

        var header = similar
            ? (arabic
                ? $"📋 نتائج مشابهة ({string.Join(" · ", parts)})"
                : $"📋 Similar results ({string.Join(" · ", parts)})")
            : intent.Sort == "experience" || intent.MinExperienceYears is > 0
                ? (arabic
                    ? $"🔍 نتائج حسب الخبرة ({string.Join(" · ", parts)})"
                    : $"🔍 Matches by experience ({string.Join(" · ", parts)})")
                : (arabic
                    ? $"🔍 أفضل النتائج حسب التقييم ({string.Join(" · ", parts)})"
                    : $"🔍 Top rated matches ({string.Join(" · ", parts)})");

        var lines = new List<string> { header, "" };
        var rank = 1;
        foreach (var row in rows)
        {
            var p = row.Profile;
            var price = ProviderMapper.FormatPriceLabel(p.PriceAmount, p.PriceUnit);
            var exp = p.ExperienceYears is > 0
                ? (arabic ? $"{p.ExperienceYears} سنوات" : $"{p.ExperienceYears} years exp")
                : (arabic ? "—" : "—");
            var rating = row.Rating is > 0 ? $"{row.Rating:0.#}/5" : "—";
            var reviews = row.RatingCount > 0
                ? (arabic ? $"{row.RatingCount} تقييم" : $"{row.RatingCount} reviews")
                : (arabic ? "بدون تقييمات" : "no reviews yet");

            lines.Add(
                $"━━━━━━━━━━━━\n#{rank} 👤 **{p.DisplayName}**\n🔧 {p.Trade}  |  📍 {p.City}\n📅 {exp}  ⭐ {rating} ({reviews})  💰 {price}\n🆔 {p.Id}"
            );
            rank++;
        }

        lines.Add(arabic ? "\n💡 للتفاصيل اكتب: تفاصيل demo-plumber-amman" : "\n💡 For details type: details demo-plumber-amman");
        return string.Join("\n", lines);
    }

    private static string FormatDetail(ProviderRow row, bool arabic)
    {
        var p = row.Profile;
        var price = ProviderMapper.FormatPriceLabel(p.PriceAmount, p.PriceUnit);
        var exp = ProviderMapper.FormatExperienceLabel(p.ExperienceYears);
        var rating = row.Rating is > 0 ? $"{row.Rating:0.#}/5" : "—";
        var reviews = row.RatingCount > 0
            ? (arabic ? $"{row.RatingCount} تقييم" : $"{row.RatingCount} reviews")
            : (arabic ? "بدون تقييمات" : "no reviews yet");

        if (arabic)
        {
            return $"""
                📋 *تفاصيل {p.DisplayName}*

                🔧 الخدمة: {p.Trade}
                📍 المدينة: {p.City}
                ⭐ التقييم: {rating} ({reviews})
                💰 السعر: {price}
                📅 {exp}
                📝 {p.Bio}
                🆔 {p.Id}
                """;
        }

        return $"""
            📋 *Details for {p.DisplayName}*

            🔧 Trade: {p.Trade}
            📍 City: {p.City}
            ⭐ Rating: {rating} ({reviews})
            💰 Price: {price}
            📅 {exp}
            📝 {p.Bio}
            🆔 ID: {p.Id}
            """;
    }

    private static HerafiChatTopPick? ToTopPick(ProviderRow? row)
    {
        if (row is null)
            return null;
        return new HerafiChatTopPick(
            row.Profile.Id,
            row.Profile.DisplayName,
            row.Rating is > 0 ? row.Rating : null,
            row.RatingCount,
            row.Profile.ExperienceYears
        );
    }

    private static string RedirectReply(ParsedIntent intent, HerafiChatTopPick? topPick, bool arabic, bool similar = false)
    {
        var label = !string.IsNullOrEmpty(intent.Trade)
            ? (arabic ? TradeLabelAr.GetValueOrDefault(intent.Trade, intent.Trade) : TradeLabelEn.GetValueOrDefault(intent.Trade, intent.Trade))
            : (arabic ? "الخدمات" : "providers");

        var place = !string.IsNullOrEmpty(intent.City)
            ? (arabic ? CityLabelAr.GetValueOrDefault(intent.City, intent.City) : intent.City)
            : (arabic ? "الأردن" : "Jordan");

        var sortHint = intent.Sort switch
        {
            "price" => arabic ? " (أقل سعر)" : " (lowest price first)",
            "experience" => arabic ? " (حسب الخبرة)" : " (sorted by experience)",
            _ => arabic ? " (حسب التقييم)" : " (sorted by rating)",
        };

        if (intent.MinExperienceYears is int expHint && expHint > 0 && intent.Sort != "price")
            sortHint = arabic ? $" (خبرة {expHint}+ سنوات)" : $" ({expHint}+ years experience)";

        var topLine = "";
        if (topPick is not null && intent.Sort != "price")
        {
            if (intent.Sort == "experience" || intent.MinExperienceYears is > 0)
            {
                var expPart = topPick.ExperienceYears is > 0
                    ? (arabic ? $"{topPick.ExperienceYears} سنوات خبرة" : $"{topPick.ExperienceYears} years experience")
                    : "";
                var ratingPart = topPick.Rating is > 0
                    ? (arabic ? $" · {topPick.Rating:0.#}/5" : $" · {topPick.Rating:0.#}/5 rating")
                    : "";
                topLine = arabic
                    ? $"📅 **{topPick.DisplayName}** — {expPart}{ratingPart}\n\n"
                    : $"📅 Top match: **{topPick.DisplayName}** — {expPart}{ratingPart}\n\n";
            }
            else if (topPick.Rating is > 0)
            {
                var reviewHint = topPick.RatingCount > 0
                    ? (arabic ? $" · {topPick.RatingCount} تقييم" : $" · {topPick.RatingCount} reviews")
                    : "";
                topLine = arabic
                    ? $"⭐ **{topPick.DisplayName}** — {topPick.Rating:0.#}/5{reviewHint}\n\n"
                    : $"⭐ Top pick: **{topPick.DisplayName}** — {topPick.Rating:0.#}/5{reviewHint}\n\n";
            }
        }

        return topLine + (arabic
            ? similar
                ? $"🔍 جاري فتح **{label}**{sortHint} — نتائج مشابهة على Artisan…"
                : $"🔍 جاري فتح **{label}** في **{place}**{sortHint} على Artisan…"
            : similar
                ? $"🔍 Opening **{label}**{sortHint} — similar results on Artisan…"
                : $"🔍 Opening **{label}** in **{place}**{sortHint} on Artisan…");
    }

    private static HerafiChatNavigate BuildNavigateAction(ParsedIntent intent, string userMessage, bool similar = false)
    {
        if (!string.IsNullOrEmpty(intent.DetailId))
        {
            return new HerafiChatNavigate("browse", "", "", intent.DetailId, "rating");
        }

        var sort = intent.Sort switch
        {
            "price" => "price_asc",
            "experience" => "experience",
            _ => "rating",
        };
        var hasIntent = !string.IsNullOrEmpty(intent.Trade)
            || !string.IsNullOrEmpty(intent.City)
            || !string.IsNullOrEmpty(intent.Sort)
            || intent.MinExperienceYears is > 0;

        if (!hasIntent)
            return NoneNavigate();

        var q = intent.City ?? "";
        return new HerafiChatNavigate(
            "browse",
            intent.Trade ?? "",
            intent.City ?? "",
            q,
            sort,
            similar ? null : intent.MinExperienceYears,
            similar
        );
    }

    private ParsedIntent ResolveIntent(string text, string low)
    {
        if (training.TryMatch(text, out var trained))
        {
            return new ParsedIntent
            {
                Trade = trained.Trade,
                City = NormalizeCity(trained.City),
                Sort = trained.Sort,
                MinRating = trained.MinRating,
                MinExperienceYears = trained.MinExperienceYears,
            };
        }

        return ParseIntent(text, low);
    }

    private static string? NormalizeCity(string? city)
    {
        if (string.IsNullOrWhiteSpace(city))
            return null;

        var c = city.Trim();
        if (c.EndsWith(" (default sort)", StringComparison.OrdinalIgnoreCase))
            c = c[..c.IndexOf(" (default sort)", StringComparison.OrdinalIgnoreCase)].Trim();

        var key = c.ToLowerInvariant();
        return key switch
        {
            "amman" or "عمان" => "Amman",
            "irbid" or "إربد" or "اربد" => "Irbid",
            "zarqa" or "الزرقاء" or "زرقاء" => "Zarqa",
            "aqaba" or "العقبة" or "عقبة" => "Aqaba",
            "jerash" or "جرش" => "Jerash",
            "ajloun" or "عجلون" => "Ajloun",
            "salt" or "السلط" or "سلط" => "Salt",
            "madaba" or "مادبا" => "Madaba",
            "mafraq" or "المفرق" or "مفرق" => "Mafraq",
            "karak" or "الكرk" => "Karak",
            "tafilah" or "الطفيلة" or "طفيلة" => "Tafilah",
            "maan" or "ma'an" or "معan" => "Maan",
            _ => c,
        };
    }

    private static ParsedIntent ParseIntent(string text, string low)
    {
        var intent = new ParsedIntent();

        var detailMatch = Regex.Match(
            text,
            @"\b(?:details?|profile|تفاصيل|معرف)\s*#?\s*([\w-]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );
        if (detailMatch.Success)
        {
            intent.DetailId = detailMatch.Groups[1].Value;
            return intent;
        }

        foreach (var (trade, keywords) in ServiceKeywords)
        {
            if (keywords.Any(k => low.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                intent.Trade = trade;
                break;
            }
        }

        foreach (var (city, keywords) in CityKeywords)
        {
            if (keywords.Any(k => low.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                intent.City = city;
                break;
            }
        }

        foreach (var (sort, keywords) in SortKeywords)
        {
            if (keywords.Any(k => low.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                intent.Sort = sort;
                break;
            }
        }

        intent.MinRating = ParseMinRating(text, low);
        intent.MinExperienceYears = ParseMinExperienceYears(text, low);

        if (intent.MinExperienceYears is > 0 && intent.Sort != "price" && intent.Sort != "rating")
            intent.Sort = "experience";
        else if (intent.MinRating is > 0 && intent.Sort != "price")
            intent.Sort = "rating";
        else if (string.IsNullOrEmpty(intent.Sort))
            intent.Sort = "rating";

        return intent;
    }

    private static int? ParseMinExperienceYears(string text, string low)
    {
        var patterns = new[]
        {
            @"(\d{1,2})\s*(?:\+?\s*)?(?:years?|yrs?\.?)\s*(?:of\s+)?(?:experience|exp|خبرة)?",
            @"(?:experience|exp|خبرة|خبرات)\s*(?:of|at least|minimum|min|\+|of at least)?\s*(\d{1,2})\s*(?:\+?\s*)?(?:years?|yrs?|سنوات|سنة)?",
            @"(?:with|at least|minimum|min|\+|مع|على الأقل)\s*(\d{1,2})\s*(?:\+?\s*)?(?:years?|yrs?|سنوات|سنة)\s*(?:of\s+)?(?:experience|exp|خبرة)?",
            @"(\d{1,2})\s*(?:سنوات|سنة)\s*(?:خبرة|خبرات)?",
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var years) && years is >= 1 and <= 50)
                return years;
        }

        return null;
    }

    private static double? ParseMinRating(string text, string low)
    {
        var starMatch = Regex.Match(
            text,
            @"(\d(?:\.\d)?)\s*(?:\+?\s*)?(?:star|stars|⭐|نجوم|نجمة|/5)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );
        if (starMatch.Success && double.TryParse(starMatch.Groups[1].Value, out var starVal) && starVal is >= 1 and <= 5)
            return starVal;

        var thresholdMatch = Regex.Match(
            low,
            @"(?:above|over|at least|minimum|min|\+|أكثر من|فوق|على الأقل)\s*(\d(?:\.\d)?)\s*(?:star|stars|rating|تقييم|نجوم)?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );
        if (thresholdMatch.Success && double.TryParse(thresholdMatch.Groups[1].Value, out var threshold) && threshold is >= 1 and <= 5)
            return threshold;

        var ratingFirstMatch = Regex.Match(
            low,
            @"(?:rating|rated|تقييم)\s*(?:of|at|>=|≥|أكبر من)?\s*(\d(?:\.\d)?)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );
        if (ratingFirstMatch.Success && double.TryParse(ratingFirstMatch.Groups[1].Value, out var ratingVal) && ratingVal is >= 1 and <= 5)
            return ratingVal;

        return null;
    }

    private static bool HasSearchIntent(ParsedIntent intent) =>
        !string.IsNullOrEmpty(intent.Trade)
        || !string.IsNullOrEmpty(intent.City)
        || !string.IsNullOrEmpty(intent.Sort)
        || !string.IsNullOrEmpty(intent.DetailId)
        || intent.MinRating is > 0
        || intent.MinExperienceYears is > 0;

    private static bool IsGreeting(string low)
    {
        string[] greetings = ["hi", "hello", "hey", "start", "help", "مرحبا", "أهلا", "هلا", "مساعدة", "ابدأ"];
        return string.IsNullOrWhiteSpace(low) || low == "?" || greetings.Any(g => low.Contains(g, StringComparison.Ordinal));
    }

    private static bool IsArabic(string text) => Regex.IsMatch(text, @"[\u0600-\u06FF]");

    private static HerafiChatNavigate NoneNavigate() => new("none", "", "", "", "rating");

    private static string HelpMessage(bool arabic)
    {
        if (arabic)
        {
            return """
                👋 أهلاً! أنا مساعد *Artisan AI* — أبحث مباشرة في قاعدة بيانات المزودين.

                جرّب:
                • سباك بخبرة 8 سنوات في عمان
                • كهربائي 10+ سنوات خبرة
                • أعلى تقييم — دهان
                • تفاصيل demo-plumber-amman

                English or Arabic 👍
                """;
        }

        return """
            👋 Hi! I'm *Artisan AI* — I search live provider data from the database.

            Try:
            • plumber with 8 years experience
            • electrician 10+ years in Amman
            • top rated painter
            • details demo-plumber-amman

            English or Arabic 👍
            """;
    }
}
