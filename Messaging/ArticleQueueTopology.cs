using RabbitMQ.Client;

namespace Messaging;

public static class ArticleQueueTopology
{
    public const string ExchangeName = "articles.published";
    public const string ArticleServiceQueue = "articles.article-service";
    public const string NewsletterServiceQueue = "articles.newsletter-service";

    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            ExchangeName,
            ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await DeclareQueueAsync(channel, ArticleServiceQueue, cancellationToken);
        await DeclareQueueAsync(channel, NewsletterServiceQueue, cancellationToken);
    }

    private static async Task DeclareQueueAsync(
        IChannel channel,
        string queueName,
        CancellationToken cancellationToken)
    {
        await channel.QueueDeclareAsync(
            queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queueName,
            ExchangeName,
            routingKey: string.Empty,
            arguments: null,
            cancellationToken: cancellationToken);
    }
}
