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
        public async Task<int> CreateGroup(int surveyId)
        {
            var group = await _dbContext.Groups
                              .FirstOrDefaultAsync(g => g.SurveyId == surveyId && g.Active);

            if (group != null)
                return group.Id;

            // 2️⃣ Yo‘q bo‘lsa — yangi yaratamiz
            var survey = await _dbContext.Surveys.FindAsync(surveyId);

            var newGroup = new Group
            {
                Name = $"⚽ Futbol ({DateTime.Now:dd.MM.yyyy})",
                SurveyId = surveyId,
                Active = true
            };

            _dbContext.Groups.Add(newGroup);
            await _dbContext.SaveChangesAsync();

            return newGroup.Id;
        }

        public async Task AddUserToGroup(long telegramUserId, int groupId, bool isGoing)
        {
            var groupUser = await _dbContext.GroupUsers.FirstOrDefaultAsync(x =>
                x.GroupId == groupId &&
                x.ChatId == telegramUserId);

            if (groupUser != null)
            { 
                groupUser.Active = isGoing;
                _dbContext.GroupUsers.Update(groupUser);
            }
            else
            {
                _dbContext.GroupUsers.Add(new GroupUser
                {
                    GroupId = groupId,
                    ChatId = telegramUserId,
                    Active = isGoing
                });
            }

            await _dbContext.SaveChangesAsync();
        }

    }
}
