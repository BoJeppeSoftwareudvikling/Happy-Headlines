using System.ComponentModel.DataAnnotations;

namespace DraftService.Dtos;

public class UpdateDraftRequestDto
{
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}
