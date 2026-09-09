using ProfanityService.Models;
using Microsoft.EntityFrameworkCore;


namespace ProfanityService.Data;

public class ProfanityDbContextFactory(IConfiguration config)
{
    public ProfanityDbContext Create()
    {
        var connectionString = config.GetConnectionString("ProfanityService")
            ?? throw new InvalidOperationException("No connection string for ProfanityService");

        var options = new DbContextOptionsBuilder<ProfanityDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ProfanityDbContext(options);
    }
}
