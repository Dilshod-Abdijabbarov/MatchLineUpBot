using LineUpBot.Context.MatchDbContext;
using LineUpBot.Domain.Models;
using LineUpBot.Service.IServices;
using Microsoft.EntityFrameworkCore;

namespace LineUpBot.Service.Services
{
    public class GroupService : IGroupService
    {
        private readonly MatchLineUpDbContext _dbContext;
        public GroupService(MatchLineUpDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task<int> CreateTelegramGroup(long groupChatId)
        {
            var group = await _dbContext.TelegramGroups
                              .FirstOrDefaultAsync(g => g.TelegramGroupChatId == groupChatId && g.Active);

            if (group != null)
                return group.Id;

            var newGroup = new TelegramGroup
            {
                 Name = $"{groupChatId}",
                 TelegramGroupChatId = groupChatId,
                 Active = true,
                 CreatedDate = DateTime.UtcNow                
            };

            await _dbContext.TelegramGroups.AddAsync(newGroup);
            await _dbContext.SaveChangesAsync();

            return newGroup.Id;
        }
    }
}
