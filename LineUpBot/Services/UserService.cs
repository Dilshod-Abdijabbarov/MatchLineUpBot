using LineUpBot.Enums;
using LineUpBot.IServices;
using LineUpBot.MatchDbContext;
using LineUpBot.Models;
using Microsoft.EntityFrameworkCore;

namespace LineUpBot.Services
{
    public  class UserService : IUserService
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


        public async Task<BotUser> GetOrCreateOrUpdateAsync(Telegram.Bot.Types.User tgUser)
        {
            var user = await _dbContext.BotUsers
                .FirstOrDefaultAsync(x =>
                    x.ChatId == tgUser.Id
                );

            if (user == null)
            {
                user = new BotUser
                {
                    ChatId = tgUser.Id,
                    UserName = tgUser.Username,
                    FirstName = tgUser.FirstName,
                    LastName = tgUser.LastName,
                    CreatedDate = DateTime.UtcNow,
                };

                await _dbContext.BotUsers.AddAsync(user);
                await _dbContext.SaveChangesAsync();
            }

            return user;
        }

        public async Task<List<BotUser>> GetUsersByGroupIdAsync(int groupId)
        {

            var userIds = await _dbContext.GroupUsers
                .Where(x => x.GroupId == groupId && x.Active).Select(x=>x.ChatId).ToListAsync();

            return await _dbContext.BotUsers
                .Where(x => userIds.Contains(x.ChatId))
                .ToListAsync();
        }

    }
}
