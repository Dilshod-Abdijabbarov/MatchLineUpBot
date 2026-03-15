using LineUpBot.Context.MatchDbContext;
using LineUpBot.Domain.Models;
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

    public async Task SendMainMenu(long telegramGroupChatId)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
             {
                InlineKeyboardButton.WithCallbackData(
                    "📊 So‘rovnoma yaratish",
                    "CREATE_POLL"
                )
            }
        });

        var telegramGroup = await _dbContext.TelegramGroups
            .FirstOrDefaultAsync(x=>x.TelegramGroupChatId == telegramGroupChatId && x.Active);

        if( telegramGroup == null ) 
        {
            telegramGroup = new TelegramGroup
            {
                 Active = true ,             
                 Name = $"{telegramGroupChatId}",
                 TelegramGroupChatId = telegramGroupChatId,
            };

            await _dbContext.TelegramGroups.AddAsync(telegramGroup);
            await _dbContext.SaveChangesAsync();
        }

        var currentWeek = await GetWeekNumber();
        var isSurvey = await _dbContext.Surveys
            .AnyAsync(x => x.IsActive && x.CurrentWeek == currentWeek && x.TelegramGroupId == telegramGroup.Id);

        if (isSurvey)
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
            chatId: telegramGroupChatId,
            text: "⚽ LineUpBot menyusi:",
            replyMarkup: keyboard
        );
    }
    public async Task<int> GetWeekNumber()
    {
        var calendar = CultureInfo.InvariantCulture.Calendar;

        return calendar.GetWeekOfYear(
            DateTime.UtcNow,
            CalendarWeekRule.FirstFourDayWeek,
            DayOfWeek.Monday
        );
    }
}

