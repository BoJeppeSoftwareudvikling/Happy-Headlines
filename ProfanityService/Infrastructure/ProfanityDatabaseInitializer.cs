using Microsoft.Extensions.Options;
using ProfanityService.Data;
using ProfanityService.Models;
using Microsoft.EntityFrameworkCore;

namespace ProfanityService.Infrastructure;

public class ProfanityDatabaseInitializer
{
    private readonly ProfanityDbContextFactory _factory;
    private readonly DatabaseInitializationOptions _options;
    private readonly ILogger<ProfanityDatabaseInitializer> _logger;

    public ProfanityDatabaseInitializer(
        ProfanityDbContextFactory factory,
        IOptions<DatabaseInitializationOptions> options,
        ILogger<ProfanityDatabaseInitializer> logger)
    {
        _factory = factory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await EnsureDatabaseCreatedAsync(cancellationToken);
    }

    private async Task EnsureDatabaseCreatedAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            try
            {
                await using var db = _factory.Create();
                await db.Database.EnsureCreatedAsync(cancellationToken);
                await SeedWordsAsync(db, cancellationToken);
                _logger.LogInformation("Database ready for Profanity.");
                return;
            }
            catch (Exception ex)
            {
                if (attempt < _options.MaxAttempts)
                {
                    _logger.LogWarning(
                        ex,
                        "Database for Profanity is not ready yet (attempt {Attempt}/{MaxAttempts}). Retrying in {DelaySeconds}s.",
                        attempt,
                        _options.MaxAttempts,
                        _options.DelaySeconds);

                    await Task.Delay(TimeSpan.FromSeconds(_options.DelaySeconds), cancellationToken);
                    continue;
                }

                _logger.LogError(
                    ex,
                    "Could not create database for Profanity after {MaxAttempts} attempts.",
                    _options.MaxAttempts);

                throw new InvalidOperationException(
                    $"Could not create database for Profanity after {_options.MaxAttempts} attempts.",
                    ex);
            }
        }
    }
    private static async Task SeedWordsAsync(
        ProfanityDbContext db,
        CancellationToken cancellationToken)
    {
        if (await db.Profanity.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Profanity.AddRange(
            new Profanity { Word = "damn" },
            new Profanity { Word = "hell" },
            new Profanity { Word = "crap" });

        await db.SaveChangesAsync(cancellationToken);
    }
}
