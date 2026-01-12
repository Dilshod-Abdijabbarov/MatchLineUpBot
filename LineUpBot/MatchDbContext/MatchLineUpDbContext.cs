using LineUpBot.Models;
using Microsoft.EntityFrameworkCore;

namespace LineUpBot.MatchDbContext;
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

