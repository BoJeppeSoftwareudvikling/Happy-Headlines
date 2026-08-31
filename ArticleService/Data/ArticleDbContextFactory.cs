using ArticleService.Models;
using Microsoft.EntityFrameworkCore;

namespace ArticleService.Data;

public class ArticleDbContextFactory(IConfiguration config)
{
    public ArticleDbContext Create(Continent continent)
    {
        var connectionString = config.GetConnectionString(continent.ToString())
            ?? throw new InvalidOperationException($"No connection string for {continent}");

        var options = new DbContextOptionsBuilder<ArticleDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ArticleDbContext(options);
    }
}
