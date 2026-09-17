using DraftService.Data;
using Microsoft.Extensions.Options;

namespace DraftService.Infrastructure;

public class DraftDatabaseInitializer(
    DraftDbContextFactory factory,
    IOptions<DatabaseInitializationOptions> options,
    ILogger<DraftDatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= options.Value.MaxAttempts; attempt++)
        {
            try
            {
                await using var db = factory.Create();
                await db.Database.EnsureCreatedAsync(cancellationToken);
                logger.LogInformation("Database ready for Drafts.");
                return;
            }
            catch (Exception ex) when (attempt < options.Value.MaxAttempts)
            {
                logger.LogWarning(
                    ex,
                    "Database for Drafts is not ready yet (attempt {Attempt}/{MaxAttempts}). Retrying in {DelaySeconds}s.",
                    attempt,
                    options.Value.MaxAttempts,
                    options.Value.DelaySeconds);

                await Task.Delay(TimeSpan.FromSeconds(options.Value.DelaySeconds), cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Could not create database for Drafts after {MaxAttempts} attempts.",
                    options.Value.MaxAttempts);

                throw new InvalidOperationException(
                    $"Could not create database for Drafts after {options.Value.MaxAttempts} attempts.",
                    ex);
            }
        }
    }
}
