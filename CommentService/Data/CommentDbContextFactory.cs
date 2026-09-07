using Microsoft.EntityFrameworkCore;

namespace CommentService.Data;

public class CommentDbContextFactory(IConfiguration config)
{
    public CommentDbContext Create()
    {
        var connectionString = config.GetConnectionString("CommentDatabase")
            ?? throw new InvalidOperationException("No connection string for CommentDatabase");

        var options = new DbContextOptionsBuilder<CommentDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new CommentDbContext(options);
    }
}
