using Microsoft.EntityFrameworkCore;

namespace SubscriberService.Data;

public class SubscriberDbContextFactory(IConfiguration configuration)
{
    public SubscriberDbContext Create()
    {
        var connectionString = configuration.GetConnectionString("SubscriberService")
            ?? throw new InvalidOperationException(
                "No connection string for SubscriberService.");

        var options = new DbContextOptionsBuilder<SubscriberDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new SubscriberDbContext(options);
    }
}
