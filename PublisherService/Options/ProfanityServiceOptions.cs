namespace PublisherService.Options;

public class ProfanityServiceOptions
{
    public const string SectionName = "ProfanityService";

    public string BaseUrl { get; set; } = "http://localhost:5050";
    public int TimeoutSeconds { get; set; } = 2;
}
