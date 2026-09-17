using Microsoft.EntityFrameworkCore;

namespace DraftService.Data;

public class DraftDbContextFactory(IConfiguration config)
{
    public DraftDbContext Create()
    {
        var connectionString = config.GetConnectionString("DraftService")
            ?? throw new InvalidOperationException("No connection string for DraftService");

        var options = new DbContextOptionsBuilder<DraftDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new DraftDbContext(options);
    }
}
