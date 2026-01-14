using System;
using LineUpBot.IServices;
using LineUpBot.MatchDbContext;
using LineUpBot.Services;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:8080");
// 🔹 Telegram bot client
builder.Services.AddSingleton<ITelegramBotClient>(
    new TelegramBotClient("8447766617:AAFCiEh6WpnS0fkzhVWQVUa7y5tmcggdrCw"));
builder.Services.AddScoped<ITelegramUpdateHandler, TelegramUpdateHandler>();
builder.Services.AddScoped<IUserService,UserService>();
builder.Services.AddScoped<BotMenuService>();
builder.Services.AddScoped<IGroupService, GroupService>();
// Add services to the container.
builder.Services.AddDbContext<MatchLineUpDbContext>(options =>
options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnectionString")));


var app = builder.Build();


// 🔹 Webhook endpoint
app.MapPost("/webhook", async (
    Update update,
    ITelegramUpdateHandler handler) =>
{
    await handler.HandleAsync(update);
    return Results.Ok();
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MatchLineUpDbContext>();
    db.Database.Migrate();
}


app.Run();
