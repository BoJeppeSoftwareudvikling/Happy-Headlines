namespace SubscriberService.Features;

public class FeatureHubOptions
{
    public const string SectionName = "FeatureHub";

    public string EdgeUrl { get; set; } = "http://localhost:8085";
    public string SdkKey { get; set; } = string.Empty;
    public string FeatureKey { get; set; } = "subscriberservice-release-toggle";
    public int RetrySeconds { get; set; } = 5;
}
