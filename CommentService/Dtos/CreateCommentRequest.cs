using CommentService.Models;

namespace CommentService.Dtos;

public class CreateCommentRequest
{
    public int ArticleId { get; set; }
    public Continent Continent { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
