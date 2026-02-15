using LineUpBot.Domain.Enums;
using LineUpBot.Domain.Models;

namespace LineUpBot.Service.IServices
{
    public interface IUserService
    {
        void Clear(long userId);
        void SetState(long userId, UserState state);
        UserState GetState(long userId);
        Task<BotUser> GetOrCreateOrUpdateAsync(Telegram.Bot.Types.User tgUser);
        Task<List<BotUser>> GetUsersByGroupIdAsync(int groupId);
        Task<List<BotUser>> GetAllUsersAsync();
    }
}
