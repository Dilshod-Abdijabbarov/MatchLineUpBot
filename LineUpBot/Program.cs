using LineUpBot.Context.MatchDbContext;
using LineUpBot.Domain.Configuration;
using LineUpBot.Service.IServices;
using LineUpBot.Service.Services;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;

var builder = WebApplication.CreateBuilder(args);

// 🔹 Telegram bot client
builder.Services.AddSingleton<ITelegramBotClient>(
    new TelegramBotClient("8447766617:AAFCiEh6WpnS0fkzhVWQVUa7y5tmcggdrCw"));
builder.Services.AddScoped<ITelegramUpdateHandler, TelegramUpdateHandler>();
builder.Services.AddScoped<IUserService,UserService>();
builder.Services.AddScoped<BotMenuService>();
builder.Services.AddScoped<IGroupService, GroupService>();
// Add services to the container.
builder.Services.AddDbContext<MatchLineUpDbContext>(options =>
options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnectionString")));
builder.Services.AddHostedService<TelegramWebhookInitializer>();

var app = builder.Build();


// 🔹 Webhook endpoint
app.MapPost("/api/webhook", async (
    Update update,
    ITelegramUpdateHandler handler) =>
{
    await handler.HandleAsync(update);
    return Results.Ok();
});

app.MapGet("/health", () => Results.Ok("OK"));

app.Lifetime.ApplicationStarted.Register(async () =>
{
    using var scope = app.Services.CreateScope();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<MatchLineUpDbContext>();
        await db.Database.MigrateAsync();
        Console.WriteLine("✅ Database migrated");
    }
    catch (Exception ex)
    {
        Console.WriteLine("❌ Database migrate error:");
        Console.WriteLine(ex.Message);
    }
});


app.Run();
