using LineUpBot.Context.MatchDbContext;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
namespace LineUpBot.Service.Services;
public class BotMenuService
{
    private readonly ITelegramBotClient _bot;
    private readonly MatchLineUpDbContext _dbContext;

    public BotMenuService(ITelegramBotClient bot, MatchLineUpDbContext dbContext)
    {
        _bot = bot;
        _dbContext = dbContext;
    }

    public async Task SendMainMenu(long chatId)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            //new[]
            //{
            //    InlineKeyboardButton.WithCallbackData(
            //        "➕ Create Group",
            //        "CREATE_GROUP"
            //    )
            //},
            new[]
             {
                InlineKeyboardButton.WithCallbackData(
                    "📊 So‘rovnoma yaratish",
                    "CREATE_POLL"
                )
            }
        });

        var currentWeek = GetWeekNumber();
        if (await _dbContext.Surveys.AnyAsync(x=>x.IsActive && x.CurrentWeek == currentWeek))
        {
            keyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "📊 So'rovnomada qatnashish",
                        "CREATE_POLL"
                    )
                }
            });
        }

        await _bot.SendMessage(
            chatId: chatId,
            text: "⚽ LineUpBot menyusi:",
            replyMarkup: keyboard
        );
    }
    private int GetWeekNumber()
    {
        var calendar = CultureInfo.InvariantCulture.Calendar;

        return calendar.GetWeekOfYear(
            DateTime.UtcNow,
            CalendarWeekRule.FirstFourDayWeek,
            DayOfWeek.Monday
        );
    }
}

