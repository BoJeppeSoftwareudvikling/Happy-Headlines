namespace ProfanityService.Dtos;
public class ProfanityCheckResponseDto
{
    public bool ContainsProfanity { get; set; }
    public string FilteredText { get; set; } = string.Empty;
    public List<string> MatchedWords { get; set; } = [];
}