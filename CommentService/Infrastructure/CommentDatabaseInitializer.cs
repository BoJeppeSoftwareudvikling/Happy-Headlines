using CommentService.Data;
using Microsoft.Extensions.Options;

namespace CommentService.Infrastructure;

    public class CommentDatabaseInitializer
{
    private readonly CommentDbContextFactory _factory;
    private readonly ILogger<CommentDatabaseInitializer> _logger;
    private readonly DatabaseInitializationOptions _options;

    public CommentDatabaseInitializer(
        CommentDbContextFactory factory,
        ILogger<CommentDatabaseInitializer> logger,
        IOptions<DatabaseInitializationOptions> options)
    {
        _factory = factory;
        _logger = logger;
        _options = options.Value;
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
                _logger.LogInformation("Database ready for Comments.");
                return;
            }
            catch (Exception ex)
            {
                if (attempt < _options.MaxAttempts)
                {
                    _logger.LogWarning(
                        ex,
                        "Database for Comments is not ready yet (attempt {Attempt}/{MaxAttempts}). Retrying in {DelaySeconds}s.",
                        attempt,
                        _options.MaxAttempts,
                        _options.DelaySeconds);

                    await Task.Delay(TimeSpan.FromSeconds(_options.DelaySeconds), cancellationToken);
                    continue;
                }

                _logger.LogError(
                    ex,
                    "Could not create database for Comments after {MaxAttempts} attempts.",
                    _options.MaxAttempts);

                throw new InvalidOperationException(
                    $"Could not create database for Comments after {_options.MaxAttempts} attempts.",
                    ex);
            }
        }
    }
}
