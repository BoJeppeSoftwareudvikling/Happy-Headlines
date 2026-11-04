using System.Text.RegularExpressions;

namespace ProfanityService.Services;

public sealed record ProfanityFilterResult(
    string FilteredText,
    IReadOnlyList<string> MatchedWords);

public static class ProfanityTextFilter
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    public static ProfanityFilterResult Filter(string text, IEnumerable<string> words)
    {
        var normalizedText = text.Trim().ToLowerInvariant();
        var normalizedWords = words
            .Select(NormalizeWord)
            .Where(word => word.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedWords.Length == 0)
        {
            return new ProfanityFilterResult(normalizedText, []);
        }

        var alternatives = normalizedWords
            .OrderByDescending(word => word.Length)
            .Select(Regex.Escape);
        var pattern = $@"\b(?:{string.Join('|', alternatives)})\b";
        var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var filteredText = Regex.Replace(
            normalizedText,
            pattern,
            match =>
            {
                matches.Add(match.Value);
                return new string('*', match.Length);
            },
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            MatchTimeout);

        var matchedWords = normalizedWords
            .Where(matches.Contains)
            .ToArray();

        return new ProfanityFilterResult(filteredText, matchedWords);
    }

    private static string NormalizeWord(string? word) =>
        word?.Trim().ToLowerInvariant() ?? string.Empty;
}
