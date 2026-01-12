namespace LineUpBot.IServices
{
    public interface IGroupService
    {
        Task<int> CreateGroup(int surveyId);
        Task AddUserToGroup(long telegramUserId, int groupId,bool isGoing);
    }
}
