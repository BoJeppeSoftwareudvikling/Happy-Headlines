namespace NewsletterService.Services;

public class WelcomeEmailSender(ILogger<WelcomeEmailSender> logger)
{
    public Task SendAsync(string email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Welcome email sent to {SubscriberEmail}.", email);
        return Task.CompletedTask;
    }
}
