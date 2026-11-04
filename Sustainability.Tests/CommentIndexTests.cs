using CommentService.Data;
using CommentService.Models;
using Microsoft.EntityFrameworkCore;

namespace Sustainability.Tests;

public class CommentIndexTests
{
    [Fact]
    public void Comment_model_indexes_article_and_creation_time()
    {
        var options = new DbContextOptionsBuilder<CommentDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelInspection;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var db = new CommentDbContext(options);

        var comment = db.Model.FindEntityType(typeof(Comment));
        var index = Assert.Single(comment!.GetIndexes());
        var propertyNames = index.Properties
            .Select(property => property.Name)
            .ToArray();

        Assert.Equal([nameof(Comment.ArticleId), nameof(Comment.CreatedAtUtc)], propertyNames);
    }
}
