using System.Text.RegularExpressions;

namespace ArtisanApi.Services;

/// <summary>Passwords must not use Arabic script (letters, digits, or presentation forms).</summary>
public static partial class PasswordInputRules
{
    public const string ArabicNotAllowedMessage =
        "Password cannot contain Arabic characters. Use Latin letters, numbers, and symbols only.";

    [GeneratedRegex(@"[\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\uFB50-\uFDFF\uFE70-\uFEFF]")]
    private static partial Regex ArabicScript();

    public static bool ContainsArabicScript(string? value) =>
        !string.IsNullOrEmpty(value) && ArabicScript().IsMatch(value);
}
