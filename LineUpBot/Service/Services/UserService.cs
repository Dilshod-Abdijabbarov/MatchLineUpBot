using LineUpBot.Context.MatchDbContext;
using LineUpBot.Domain.Enums;
using LineUpBot.Domain.Models;
using LineUpBot.Service.IServices;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot.Types;

namespace LineUpBot.Service.Services
{
    public class UserService : IUserService
    {
        private readonly Dictionary<long, UserState> _states = new();
        private readonly MatchLineUpDbContext _dbContext;
        public UserService(MatchLineUpDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public void SetState(long userId, UserState state)
        {
            _states[userId] = state;
        }

        public UserState GetState(long userId)
        {
            return _states.ContainsKey(userId)
                ? _states[userId]
                : UserState.None;
        }

        public void Clear(long userId)
        {
            _states.Remove(userId);
        }


        public async Task<BotUser> CreateUserAsync(User tgUser, long groupId)
        {
            var user = await _dbContext.BotUsers
                .FirstOrDefaultAsync(x =>
                    x.TelegramUserChatId == tgUser.Id
                );

            var group = await _dbContext.TelegramGroups
                .Where(x => x.TelegramGroupChatId == groupId)
                .Include(x => x.BotUsers)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                user = new BotUser
                {
                    TelegramUserChatId = tgUser.Id,                    
                    UserName = tgUser?.Username,
                    FirstName = tgUser?.FirstName,
                    LastName = tgUser?.LastName,
                    CreatedDate = DateTime.UtcNow,
                };

                group?.BotUsers.Add(user);
                await _dbContext.BotUsers.AddAsync(user);
                await _dbContext.SaveChangesAsync();
            }

            if (!group.BotUsers.Any(x => x.Id == user.Id))
            {
                group.BotUsers.Add(user);
                await _dbContext.SaveChangesAsync();
            }

            return user;
        }

        public async Task<BotUser?> GetByUserChatId(long chatId)
        {
            try
            {
               return await _dbContext.BotUsers.FirstOrDefaultAsync(x => x.TelegramUserChatId == chatId);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        public async Task<List<BotUser>> GetUsersBySurveyIdAsync(int surveyId)
        {
            var surveyUsers = await _dbContext.SurveyBotUsers
                .Where(x => x.SurveyId == surveyId && x.Active).Include(x=>x.BotUser).ToListAsync();

            return surveyUsers.Select(x=>x.BotUser).ToList();
        }

        public async Task AddUserToSursey(int userId, int surveyId, bool isGoing)
        {
            var surveyUser = await _dbContext.SurveyBotUsers.FirstOrDefaultAsync(x =>
                x.SurveyId == surveyId &&
                x.BotUserId == userId
            );

            if (surveyUser != null)
            {
                surveyUser.Active = isGoing;
                _dbContext.SurveyBotUsers.Update(surveyUser);
            }
            else
            {
                _dbContext.SurveyBotUsers.Add(new SurveyBotUser
                {
                    BotUserId = userId,
                    SurveyId = surveyId,
                    Active = isGoing
                });
            }

            await _dbContext.SaveChangesAsync();
        }



    }
}
