namespace CommentService.Models;

public class Comment
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    public Continent Continent { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
