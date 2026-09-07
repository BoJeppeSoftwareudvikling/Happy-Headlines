namespace CommentService.Dtos;

public sealed class ProfanityCheckResponse
{
    public bool ContainsProfanity { get; set; }
    public string FilteredText { get; set; } = string.Empty;
    public List<string> MatchedWords { get; set; } = [];
}
