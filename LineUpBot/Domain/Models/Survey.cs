namespace LineUpBot.Domain.Models
{
    public class Survey
    {
        public int Id { get; set; }
        public int? MessageId { get; set; }
        public string? PollId { get; set; } // Telegram Poll ID uchun
        public string Question { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public int CurrentWeek { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public int TelegramGroupId { get; set; }

        public TelegramGroup TelegramGroup { get; set; } = null!;
        public ICollection<SurveyBotUser> SurveyUsers { get; set; } = new List<SurveyBotUser>();
    }
}
