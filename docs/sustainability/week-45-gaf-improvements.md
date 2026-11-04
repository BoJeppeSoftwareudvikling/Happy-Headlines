# Week 45: Green Architecture Framework improvements

Three GAF tactics that had not previously been an active focus were applied to separate services:

| Tactic | Service | Improvement |
| --- | --- | --- |
| Reduce Network Package Size | ArticleService | Article lists return summaries without article content. |
| Use Efficient Databases | CommentService | The article-comment query has a matching composite index. |
| Use Efficient Algorithms | ProfanityService | Profanity filtering scans the text once with one combined regex. |

## 1. Reduce Network Package Size

`GET /Article` returned every complete article even though a list view only needs summary data. Full content remains available from `GET /Article/{id}`.

### Before

```csharp
public ActionResult<IEnumerable<Article>> GetAll([FromQuery] Continent continent)
{
    using var db = factory.Create(continent);
    return Ok(db.Articles.ToList());
}
```

### After

```csharp
public ActionResult<IReadOnlyList<ArticleSummaryDto>> GetAll(
    [FromQuery] Continent continent)
{
    using var db = factory.Create(continent);
    var articles = db.Articles
        .AsNoTracking()
        .Select(article => new ArticleSummaryDto(
            article.Id,
            article.Title,
            article.Continent))
        .ToList();

    return Ok(articles);
}
```

`ArticleSummaryDto` contains only `Id`, `Title`, and `Continent`.

### Verification

The repeatable test fixture serializes 100 articles with 2,000-character content:

| Representation | JSON size |
| --- | ---: |
| Full articles | 206,785 bytes |
| Summaries | 5,485 bytes |
| Reduction | 97.3% |

The result measures representative serialized data, not production traffic or energy. Clients that previously expected `Content` from the collection endpoint must fetch article details by ID.

## 2. Use Efficient Databases

`GET /Comment/article/{articleId}` filters by `ArticleId` and orders by `CreatedAtUtc`. The model previously had no matching index.

### Before

```csharp
public DbSet<Comment> Comments => Set<Comment>();
```

### After

```csharp
public DbSet<Comment> Comments => Set<Comment>();

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Comment>()
        .HasIndex(comment => new
        {
            comment.ArticleId,
            comment.CreatedAtUtc
        });
}
```

### Verification

`CommentIndexTests` verifies that the EF model contains the composite index in query order. Runtime query duration and SQL logical reads still require a populated SQL Server baseline. Because the project uses `EnsureCreated`, an existing Comment database must be recreated before this model change appears in SQL Server.

The tradeoff is additional index storage and write work whenever a comment is inserted.

## 3. Use Efficient Algorithms

The old profanity filter scanned the complete text once per blocked word and then scanned it again for every matched word.

### Before

```csharp
var matchedWords = words
    .Where(word => Regex.IsMatch(text, WordPattern(word), RegexOptions.IgnoreCase))
    .ToList();

foreach (var word in matchedWords)
{
    filteredText = Regex.Replace(
        filteredText,
        WordPattern(word),
        new string('*', word.Length),
        RegexOptions.IgnoreCase);
}
```

### After

```csharp
var pattern = $@"\b(?:{string.Join('|', alternatives)})\b";

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
```

The combined pattern is made from escaped database values, supports multi-word entries, and uses a one-second timeout. Filtering is isolated in `ProfanityTextFilter` and the controller remains responsible for database access and the HTTP response.

The pattern is rebuilt from the current database values for every request. This avoids a second cached source of truth, but a very large word list would increase pattern-construction cost. Pathological input can produce a timeout error instead of consuming CPU without a bound.

### Verification

A local repeatable micro-measurement used 501 blocked words, a repeated input text, and ten iterations:

| Implementation | Duration |
| --- | ---: |
| Previous per-word regex loops | 24.0 ms |
| Combined single-pass regex | 1.3 ms |

This is a local micro-measurement, not a production or energy measurement. Tests also cover whole-word boundaries, repeated matches, and multi-word entries.

## Run the verification

```bash
dotnet test Sustainability.Tests/Sustainability.Tests.csproj \
  --logger "console;verbosity=detailed"
```

The verified run completed with 6 passed tests.
