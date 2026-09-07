using CommentService.Data;
using Microsoft.Extensions.Options;

namespace CommentService.Infrastructure;

public class CommentDatabaseInitializer
{
    private readonly CommentDbContextFactory _factory;
    private readonly DatabaseInitializationOptions _options;
    private readonly ILogger<CommentDatabaseInitializer> _logger;

    public CommentDatabaseInitializer(
        CommentDbContextFactory factory,
        IOptions<DatabaseInitializationOptions> options,
        ILogger<CommentDatabaseInitializer> logger)
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

                _logger.LogInformation("CommentDatabase is ready.");
                return;
            }
            catch (Exception ex)
            {
                if (attempt < _options.MaxAttempts)
                {
                    _logger.LogWarning(
                        ex,
                        "CommentDatabase is not ready yet (attempt {Attempt}/{MaxAttempts}). Retrying in {DelaySeconds}s.",
                        attempt,
                        _options.MaxAttempts,
                        _options.DelaySeconds);

                    await Task.Delay(TimeSpan.FromSeconds(_options.DelaySeconds), cancellationToken);
                    continue;
                }

                _logger.LogError(
                    ex,
                    "Could not create CommentDatabase after {MaxAttempts} attempts.",
                    _options.MaxAttempts);

                throw new InvalidOperationException(
                    $"Could not create CommentDatabase after {_options.MaxAttempts} attempts.",
                    ex);
            }
        }
    }
}
