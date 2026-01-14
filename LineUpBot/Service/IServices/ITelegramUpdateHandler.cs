using Telegram.Bot.Types;

namespace LineUpBot.Service.IServices
{
    public interface ITelegramUpdateHandler
    {
        Task HandleAsync(Update update);
    }
}
