namespace LineUpBot.Domain.Models
{
    public class TelegramGroup
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public long TelegramGroupChatId { get; set; }
        public bool Active { get; set; } = true;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public ICollection<BotUser> BotUsers { get; set; } = new List<BotUser>();
        public ICollection<Survey> Surveys { get; set; } = new List<Survey>();
    }
}
