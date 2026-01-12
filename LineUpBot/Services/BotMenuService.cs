using LineUpBot.IServices;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
namespace LineUpBot.Services;
public class BotMenuService
{
    private readonly ITelegramBotClient _bot;

    public BotMenuService(ITelegramBotClient bot)
    {
        _bot = bot;
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

        await _bot.SendMessage(
            chatId: chatId,
            text: "⚽ LineUpBot menyusi:",
            replyMarkup: keyboard
        );
    }
}

