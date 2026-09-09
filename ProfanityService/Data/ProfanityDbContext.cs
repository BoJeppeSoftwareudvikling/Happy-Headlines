using ProfanityService.Models;
using Microsoft.EntityFrameworkCore;

namespace ProfanityService.Data;

public class ProfanityDbContext : DbContext
{
    public ProfanityDbContext(DbContextOptions<ProfanityDbContext> options) : base(options) { }
    public DbSet<Profanity> Profanity => Set<Profanity>();
}
