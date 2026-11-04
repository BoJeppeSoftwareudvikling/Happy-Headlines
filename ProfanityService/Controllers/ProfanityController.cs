using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProfanityService.Data;
using ProfanityService.Dtos;
using ProfanityService.Models;
using ProfanityService.Services;

namespace ProfanityService.Controllers;

[ApiController]
[Route("[controller]")]
public class ProfanityController(ProfanityDbContextFactory factory) : ControllerBase
{
    [HttpPost("check")]
    public ActionResult<ProfanityCheckResponseDto> Check([FromBody] ProfanityCheckRequestDto request)
    {
        using var db = factory.Create();
        var words = db.Profanity
            .AsNoTracking()
            .Select(entry => entry.Word)
            .ToList();
        var result = ProfanityTextFilter.Filter(request.Text, words);

        return Ok(new ProfanityCheckResponseDto
        {
            ContainsProfanity = result.MatchedWords.Count > 0,
            FilteredText = result.FilteredText,
            MatchedWords = result.MatchedWords.ToList()
        });
    }

    [HttpGet("words")]
    public ActionResult<IEnumerable<Profanity>> GetWords()
    {
        using var db = factory.Create();
        return Ok(db.Profanity.OrderBy(entry => entry.Word).ToList());
    }

    [HttpPost("words")]
    public ActionResult<Profanity> CreateWord([FromBody] ProfanityWordRequestDto request)
    {
        var normalized = NormalizeWord(request.Word);
        if (normalized.Length == 0)
        {
            return BadRequest("word is required.");
        }

        using var db = factory.Create();
        if (db.Profanity.Any(entry => entry.Word == normalized))
        {
            return Conflict($"The word '{normalized}' already exists.");
        }

        var toSave = new Profanity { Word = normalized };
        db.Profanity.Add(toSave);
        db.SaveChanges();

        return Created($"/Profanity/words/{toSave.Id}", toSave);
    }

    private static string NormalizeWord(string? word) =>
        word?.Trim().ToLowerInvariant() ?? string.Empty;
}
