namespace PublisherService.Dtos;

public class ProfanityCheckResponseDto
{
    public bool ContainsProfanity { get; set; }
    public List<string> MatchedWords { get; set; } = [];
}
