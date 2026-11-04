using ArticleService.Models;

namespace ArticleService.Dtos;

public sealed record ArticleSummaryDto(
    int Id,
    string Title,
    Continent Continent);
