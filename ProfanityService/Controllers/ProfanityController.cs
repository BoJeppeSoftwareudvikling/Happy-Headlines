using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProfanityService.Data;
using ProfanityService.Models;

namespace ProfanityService.Controllers;

[ApiController]
[Route("[controller]")]
public class ProfanityController(ProfanityDbContextFactory factory) : ControllerBase
{
    [HttpGet("instance")]
    public ActionResult<object> Instance() => Ok(new { Hostname = Environment.MachineName });

    [HttpPost("check")]
    public async Task<ActionResult<ProfanityCheckResponse>> Check(
        [FromBody] ProfanityCheckRequest request,
        CancellationToken cancellationToken)
    {
        var text = request.Text ?? string.Empty;

        await using var db = factory.Create();
        var words = await db.ProfanityWords
            .AsNoTracking()
            .Select(word => word.Word)
            .ToListAsync(cancellationToken);

        var matchedWords = words
            .Select(NormalizeWord)
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .Where(word => Regex.IsMatch(
                text,
                WordPattern(word),
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            .Distinct()
            .ToList();

        var filteredText = text;

        foreach (var word in matchedWords)
        {
            filteredText = Regex.Replace(
                filteredText,
                WordPattern(word),
                new string('*', word.Length),
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return Ok(new ProfanityCheckResponse
        {
            ContainsProfanity = matchedWords.Count > 0,
            FilteredText = filteredText,
            MatchedWords = matchedWords
        });
    }

    [HttpGet("words")]
    public async Task<ActionResult<IEnumerable<ProfanityWord>>> GetWords(CancellationToken cancellationToken)
    {
        await using var db = factory.Create();

        var words = await db.ProfanityWords
            .AsNoTracking()
            .OrderBy(word => word.Word)
            .ToListAsync(cancellationToken);

        return Ok(words);
    }

    [HttpPost("words")]
    public async Task<ActionResult<ProfanityWord>> CreateWord(
        [FromBody] ProfanityWordRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedWord = NormalizeWord(request.Word);

        if (string.IsNullOrWhiteSpace(normalizedWord))
        {
            return BadRequest("word is required.");
        }

        await using var db = factory.Create();

        var alreadyExists = await db.ProfanityWords
            .AnyAsync(word => word.Word == normalizedWord, cancellationToken);

        if (alreadyExists)
        {
            return Conflict($"The word '{normalizedWord}' already exists.");
        }

        var toSave = new ProfanityWord
        {
            Word = normalizedWord
        };

        db.ProfanityWords.Add(toSave);
        await db.SaveChangesAsync(cancellationToken);

        return Created($"/Profanity/words/{toSave.Id}", toSave);
    }

    [HttpDelete("words/{id:int}")]
    public async Task<IActionResult> DeleteWord(int id, CancellationToken cancellationToken)
    {
        await using var db = factory.Create();

        var word = await db.ProfanityWords.FindAsync(new object[] { id }, cancellationToken);

        if (word is null)
        {
            return NotFound();
        }

        db.ProfanityWords.Remove(word);
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static string NormalizeWord(string word) => word.Trim().ToLowerInvariant();

    private static string WordPattern(string word) => $@"\b{Regex.Escape(word)}\b";
}
