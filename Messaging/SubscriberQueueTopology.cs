using RabbitMQ.Client;

namespace Messaging;

public static class SubscriberQueueTopology
{
    public const string ExchangeName = "subscribers.registered";
    public const string NewsletterServiceQueue = "subscribers.newsletter-service";

    public static async Task DeclareAsync(
        IChannel channel,
        CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            ExchangeName,
            ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            NewsletterServiceQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            NewsletterServiceQueue,
            ExchangeName,
            routingKey: string.Empty,
            arguments: null,
            cancellationToken: cancellationToken);
    }
}
