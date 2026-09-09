namespace CommentService.Dtos;

public class CreateCommentRequestDto
{
    public int ArticleId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}