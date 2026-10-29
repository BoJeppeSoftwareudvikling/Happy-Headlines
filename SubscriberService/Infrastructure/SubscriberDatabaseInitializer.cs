using Microsoft.Extensions.Options;
using SubscriberService.Data;

namespace SubscriberService.Infrastructure;

public class SubscriberDatabaseInitializer(
    SubscriberDbContextFactory factory,
    ILogger<SubscriberDatabaseInitializer> logger,
    IOptions<DatabaseInitializationOptions> options)
{
    private readonly DatabaseInitializationOptions _options = options.Value;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            try
            {
                await using var db = factory.Create();
                await db.Database.EnsureCreatedAsync(cancellationToken);
                logger.LogInformation("Database ready for Subscribers.");
                return;
            }
            catch (Exception ex) when (attempt < _options.MaxAttempts)
            {
                logger.LogWarning(
                    ex,
                    "Subscriber database is not ready (attempt {Attempt}/{MaxAttempts}). Retrying in {DelaySeconds}s.",
                    attempt,
                    _options.MaxAttempts,
                    _options.DelaySeconds);

                await Task.Delay(
                    TimeSpan.FromSeconds(_options.DelaySeconds),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Could not create the Subscriber database after {_options.MaxAttempts} attempts.",
                    ex);
            }
        }
    }
}
