using ArticleService.Data;
using ArticleService.Models;
using Microsoft.Extensions.Options;

namespace ArticleService.Infrastructure;

public class ArticleDatabaseInitializer
{
    private readonly ArticleDbContextFactory _factory;
    private readonly DatabaseInitializationOptions _options;
    private readonly ILogger<ArticleDatabaseInitializer> _logger;

    public ArticleDatabaseInitializer(
        ArticleDbContextFactory factory,
        IOptions<DatabaseInitializationOptions> options,
        ILogger<ArticleDatabaseInitializer> logger)
    {
        _factory = factory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        foreach (var continent in Enum.GetValues<Continent>())
        {
            await EnsureDatabaseCreatedAsync(continent, cancellationToken);
        }
    }

    private async Task EnsureDatabaseCreatedAsync(Continent continent, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            try
            {
                await using var db = _factory.Create(continent);
                await db.Database.EnsureCreatedAsync(cancellationToken);
                _logger.LogInformation("Database ready for {Continent}.", continent);
                return;
            }
            catch (Exception ex)
            {
                if (attempt < _options.MaxAttempts)
                {
                    _logger.LogWarning(
                        ex,
                        "Database for {Continent} is not ready yet (attempt {Attempt}/{MaxAttempts}). Retrying in {DelaySeconds}s.",
                        continent,
                        attempt,
                        _options.MaxAttempts,
                        _options.DelaySeconds);

                    await Task.Delay(TimeSpan.FromSeconds(_options.DelaySeconds), cancellationToken);
                    continue;
                }

                _logger.LogError(
                    ex,
                    "Could not create database for {Continent} after {MaxAttempts} attempts.",
                    continent,
                    _options.MaxAttempts);

                throw new InvalidOperationException(
                    $"Could not create database for {continent} after {_options.MaxAttempts} attempts.",
                    ex);
            }
    }
}
