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

//using (var scope = app.Services.CreateScope())
//{
//    var db = scope.ServiceProvider.GetRequiredService<MatchLineUpDbContext>();
//    db.Database.Migrate();
//}


app.Run();
