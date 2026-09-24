using System.Diagnostics;
using System.Text.Json;
using Messaging;
using Messaging.Contracts;
using Microsoft.Extensions.Options;
using Monitoring;
using RabbitMQ.Client;

namespace PublisherService.Messaging;

public sealed class ArticlePublisher : IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly SemaphoreSlim _channelLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public ArticlePublisher(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    public async Task PublishAsync(ArticlePublished article, CancellationToken cancellationToken)
    {
        using var activity = MonitorService.ActivitySource.StartActivity(
            "articles publish",
            ActivityKind.Producer);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", ArticleQueueTopology.ExchangeName);
        activity?.SetTag("messaging.operation.name", "publish");
        activity?.SetTag("messaging.message.id", article.MessageId);

        await _channelLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureChannelAsync(cancellationToken);

            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = article.MessageId.ToString(),
                Type = nameof(ArticlePublished),
                Headers = new Dictionary<string, object?>()
            };
            RabbitMqTraceContext.Inject(properties.Headers);

            var body = JsonSerializer.SerializeToUtf8Bytes(article);
            await _channel!.BasicPublishAsync(
                ArticleQueueTopology.ExchangeName,
                routingKey: string.Empty,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            _channelLock.Release();
        }
    }

    private async Task EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return;
        }

        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }

        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            ClientProvidedName = $"PublisherService-{Environment.MachineName}",
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true
        };

        _connection = await factory.CreateConnectionAsync(cancellationToken);
        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);
        _channel = await _connection.CreateChannelAsync(channelOptions, cancellationToken);
        await ArticleQueueTopology.DeclareAsync(_channel, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _channelLock.Dispose();
    }
}
