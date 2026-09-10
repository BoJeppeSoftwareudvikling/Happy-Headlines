using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using ProfanityService.Data;
using ProfanityService.Dtos;
using ProfanityService.Models;

namespace ProfanityService.Controllers;

[ApiController]
[Route("[controller]")]
public class ProfanityController(ProfanityDbContextFactory factory) : ControllerBase
{
    [HttpPost("check")]
    public ActionResult<ProfanityCheckResponseDto> Check([FromBody] ProfanityCheckRequestDto request)
    {
        var text = request.Text.Trim().ToLowerInvariant();

        using var db = factory.Create();
        var words = db.Profanity
            .Select(entry => entry.Word)
            .ToList();

        var matchedWords = words
            .Select(NormalizeWord)
            .Where(word => word.Length > 0)
            .Where(word => Regex.IsMatch(text, WordPattern(word), RegexOptions.IgnoreCase))
            .Distinct()
            .ToList();

        var filteredText = text;
        foreach (var word in matchedWords)
        {
            filteredText = Regex.Replace(
                filteredText,
                WordPattern(word),
                new string('*', word.Length),
                RegexOptions.IgnoreCase);
        }

        return Ok(new ProfanityCheckResponseDto
        {
            ContainsProfanity = matchedWords.Count > 0,
            FilteredText = filteredText,
            MatchedWords = matchedWords.ToList()
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

    private static string WordPattern(string word) =>
        $@"\b{Regex.Escape(word)}\b";
}