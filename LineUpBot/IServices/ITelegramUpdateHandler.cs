using Telegram.Bot.Types;

namespace LineUpBot.IServices
{
    public interface ITelegramUpdateHandler
    {
        Task HandleAsync(Update update);
    }
}
