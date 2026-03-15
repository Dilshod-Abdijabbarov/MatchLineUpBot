using LineUpBot.Domain.Enums;

namespace LineUpBot.Domain.Models
{
    public class BotUser
    {
        public int Id { get; set; }
        public long TelegramUserChatId { get; set; }
        public string? PhoneNumber { get; set; }
        public string? NextCommand { get; set; }
        public string? FirstName { get; set; }
        public string? UserName { get; set; }
        public string? LastName { get; set; }
        public int Score { get; set; }
        public UserRole UserRole { get; set; } = UserRole.User;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public ICollection<TelegramGroup> TelegramGroups { get; set; } = new List<TelegramGroup>();
        public ICollection<SurveyBotUser> SurveyUsers { get; set; } = new List<SurveyBotUser>();
    }
}
