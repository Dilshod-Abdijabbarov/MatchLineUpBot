using LineUpBot.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace LineUpBot.Context.MatchDbContext;
public class MatchLineUpDbContext : DbContext
{
    public MatchLineUpDbContext(DbContextOptions<MatchLineUpDbContext> options) : base(options)
    {
        
    }

    public DbSet<BotUser> BotUsers { get; set; }
    public DbSet<Group> Groups { get; set; }
    public DbSet<Survey> Surveys { get; set; }
    public DbSet<GroupUser> GroupUsers { get; set; }
}

