using FeatureHubSDK;
using Microsoft.Extensions.Options;

namespace SubscriberService.Features;

public sealed class FeatureHubSubscriberReleaseToggle(
    IOptions<FeatureHubOptions> options,
    ILogger<FeatureHubSubscriberReleaseToggle> logger)
    : BackgroundService, ISubscriberReleaseToggle
{
    private readonly FeatureHubOptions _options = options.Value;
    private IClientContext? _context;
    private IFeatureHubConfig? _config;

    public bool IsEnabled
    {
        get
        {
            var context = Volatile.Read(ref _context);
            return context is not null && context[_options.FeatureKey].IsEnabled;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.SdkKey) ||
            _options.SdkKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "FeatureHub SDK key is not configured. SubscriberService remains disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _config = new EdgeFeatureHubConfig(_options.EdgeUrl, _options.SdkKey);
                var context = await _config
                    .NewContext()
                    .UserKey($"subscriber-service-{Environment.MachineName}")
                    .Build();

                Volatile.Write(ref _context, context);
                logger.LogInformation(
                    "Connected to FeatureHub. Release toggle {FeatureKey} is {State}.",
                    _options.FeatureKey,
                    IsEnabled ? "On" : "Off");

                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                Volatile.Write(ref _context, null);
                logger.LogWarning(
                    ex,
                    "Could not connect to FeatureHub. SubscriberService remains disabled; retrying in {RetrySeconds}s.",
                    _options.RetrySeconds);

                await Task.Delay(
                    TimeSpan.FromSeconds(_options.RetrySeconds),
                    stoppingToken);
            }
        }
    }
}
