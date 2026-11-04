using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArticleService.Dtos;
using ArticleService.Models;
using ProfanityService.Services;
using Xunit.Abstractions;

namespace Sustainability.Tests;

public class GafMeasurementTests(ITestOutputHelper output)
{
    [Fact]
    public void Print_representative_payload_and_filter_measurements()
    {
        var articles = Enumerable.Range(1, 100)
            .Select(id => new Article
            {
                Id = id,
                Title = $"Positive headline {id}",
                Content = new string('x', 2_000),
                Continent = Continent.Europe
            })
            .ToArray();
        var summaries = articles
            .Select(article => new ArticleSummaryDto(
                article.Id,
                article.Title,
                article.Continent))
            .ToArray();

        var fullPayloadBytes = JsonSerializer.SerializeToUtf8Bytes(articles).Length;
        var summaryPayloadBytes = JsonSerializer.SerializeToUtf8Bytes(summaries).Length;

        Assert.True(summaryPayloadBytes < fullPayloadBytes);
        output.WriteLine(
            "Article payload: full={0} bytes, summary={1} bytes, reduction={2:F1}%",
            fullPayloadBytes,
            summaryPayloadBytes,
            100d * (fullPayloadBytes - summaryPayloadBytes) / fullPayloadBytes);

        var words = Enumerable.Range(0, 500)
            .Select(index => $"blocked{index}")
            .Append("damn")
            .ToArray();
        var text = string.Join(' ', Enumerable.Repeat("safe words and damn", 40));

        var expected = LegacyFilter(text, words);
        var actual = ProfanityTextFilter.Filter(text, words).FilteredText;
        Assert.Equal(expected, actual);

        const int iterations = 10;
        var legacyDuration = Measure(() => LegacyFilter(text, words), iterations);
        var singlePassDuration = Measure(
            () => ProfanityTextFilter.Filter(text, words),
            iterations);

        output.WriteLine(
            "Profanity filter ({0} iterations): legacy={1:F1} ms, single-pass={2:F1} ms",
            iterations,
            legacyDuration.TotalMilliseconds,
            singlePassDuration.TotalMilliseconds);
    }

    private static TimeSpan Measure(Action action, int iterations)
    {
        action();
        var stopwatch = Stopwatch.StartNew();

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            action();
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static string LegacyFilter(string text, IEnumerable<string> words)
    {
        var normalizedText = text.Trim().ToLowerInvariant();
        var matchedWords = words
            .Select(word => word.Trim().ToLowerInvariant())
            .Where(word => word.Length > 0)
            .Where(word => Regex.IsMatch(
                normalizedText,
                WordPattern(word),
                RegexOptions.IgnoreCase))
            .Distinct()
            .ToList();

        foreach (var word in matchedWords)
        {
            normalizedText = Regex.Replace(
                normalizedText,
                WordPattern(word),
                new string('*', word.Length),
                RegexOptions.IgnoreCase);
        }

        return normalizedText;
    }

    private static string WordPattern(string word) =>
        $@"\b{Regex.Escape(word)}\b";
}
