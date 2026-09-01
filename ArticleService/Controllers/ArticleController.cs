using ArticleService.Data;
using ArticleService.Models;
using Microsoft.AspNetCore.Mvc;

namespace ArticleService.Controllers;

[ApiController]
[Route("[controller]")]
public class ArticleController(ArticleDbContextFactory factory) : ControllerBase
{
    [HttpGet]
    public ActionResult<IEnumerable<Article>> GetAll([FromQuery] Continent continent)
    {
        using var db = factory.Create(continent);
        return Ok(db.Articles.ToList());
    }

    [HttpGet("instance")]
    public ActionResult<object> Instance() => Ok(new { Hostname = Environment.MachineName });

    [HttpGet("{id:int}")]
    public ActionResult<Article> GetById(int id, [FromQuery] Continent continent)
    {
        using var db = factory.Create(continent);
        var article = db.Articles.Find(id);
        return article is null ? NotFound() : Ok(article);
    }

    [HttpPost]
    public ActionResult<Article> Create([FromBody] Article article)
    {
        var toSave = new Article
        {
            Title = article.Title,
            Content = article.Content,
            Continent = article.Continent
        };

        using var db = factory.Create(toSave.Continent);
        db.Articles.Add(toSave);
        db.SaveChanges();
        return CreatedAtAction(nameof(GetById),
            new { id = toSave.Id, continent = toSave.Continent }, toSave);
    }

    [HttpPut("{id:int}")]
    public IActionResult Update(int id, [FromQuery] Continent continent, [FromBody] Article updated)
    {
        using var db = factory.Create(continent);
        var article = db.Articles.Find(id);
        if (article is null) return NotFound();

        article.Title = updated.Title;
        article.Content = updated.Content;
        db.SaveChanges();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id, [FromQuery] Continent continent)
    {
        using var db = factory.Create(continent);
        var article = db.Articles.Find(id);
        if (article is null) return NotFound();

        db.Articles.Remove(article);
        db.SaveChanges();
        return NoContent();
    }
}
