namespace DraftService.Infrastructure;

public class DatabaseInitializationOptions
{
    public const string SectionName = "DatabaseInitialization";

    public int MaxAttempts { get; set; } = 20;
    public int DelaySeconds { get; set; } = 3;
}
