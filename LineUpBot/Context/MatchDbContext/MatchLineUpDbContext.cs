using LineUpBot.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace LineUpBot.Context.MatchDbContext;
public class MatchLineUpDbContext : DbContext
{
    public MatchLineUpDbContext(DbContextOptions<MatchLineUpDbContext> options) : base(options)
    {
        
    }

    public DbSet<BotUser> BotUsers { get; set; }
    public DbSet<TelegramGroup> TelegramGroups { get; set; }
    public DbSet<Survey> Surveys { get; set; }
    public DbSet<SurveyBotUser> SurveyBotUsers { get; set; }
}

