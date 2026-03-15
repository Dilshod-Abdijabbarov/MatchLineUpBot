namespace LineUpBot.Service.IServices
{
    public interface IGroupService
    {
        Task<int> CreateTelegramGroup(long groupChatId);
    }
}
