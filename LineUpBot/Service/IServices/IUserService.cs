using LineUpBot.Domain.Enums;
using LineUpBot.Domain.Models;
using Telegram.Bot.Types;

namespace LineUpBot.Service.IServices
{
    public interface IUserService
    {
        void Clear(long userId);
        void SetState(long userId, UserState state);
        UserState GetState(long userId);
        Task<BotUser> CreateUserAsync(User tgUser,long groupId);
        Task<List<BotUser>> GetUsersBySurveyIdAsync(int surveyId);
        //Task<List<BotUser>> GetAllUsersAsync();
        Task AddUserToSursey(int userId, int surveyId, bool isGoing);

        Task<BotUser> GetByUserChatId(long chatId);
    }
}
