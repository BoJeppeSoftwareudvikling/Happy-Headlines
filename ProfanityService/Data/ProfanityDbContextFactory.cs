using Microsoft.EntityFrameworkCore;

namespace ProfanityService.Data;

public class ProfanityDbContextFactory(IConfiguration config)
{
    public ProfanityDbContext Create()
    {
        var connectionString = config.GetConnectionString("ProfanityDatabase")
            ?? throw new InvalidOperationException("No connection string for ProfanityDatabase");

        var options = new DbContextOptionsBuilder<ProfanityDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ProfanityDbContext(options);
    }
}
