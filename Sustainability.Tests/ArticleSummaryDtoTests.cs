using System.Text.Json;
using ArticleService.Dtos;
using ArticleService.Models;

namespace Sustainability.Tests;

public class ArticleSummaryDtoTests
{
    [Fact]
    public void Summary_payload_does_not_contain_article_content()
    {
        var summary = new ArticleSummaryDto(42, "A positive headline", Continent.Europe);

        var json = JsonSerializer.Serialize(summary);

        Assert.Contains("A positive headline", json);
        Assert.DoesNotContain("content", json, StringComparison.OrdinalIgnoreCase);
    }
}
