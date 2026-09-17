using System.ComponentModel.DataAnnotations;

namespace DraftService.Dtos;

public class CreateDraftRequestDto
{
    [Required]
    [MaxLength(100)]
    public string PublisherId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}
