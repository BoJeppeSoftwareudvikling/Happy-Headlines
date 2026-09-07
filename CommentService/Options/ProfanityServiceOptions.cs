namespace CommentService.Options;

public class ProfanityServiceOptions
{
    public const string SectionName = "ProfanityService";

    public string BaseUrl { get; set; } = "http://localhost:5049/";
    public int TimeoutSeconds { get; set; } = 2;
    public int FailureThreshold { get; set; } = 3;
    public int BreakDurationSeconds { get; set; } = 30;
}
