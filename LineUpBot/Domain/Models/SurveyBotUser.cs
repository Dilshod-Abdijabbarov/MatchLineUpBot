namespace LineUpBot.Domain.Models
{
    public class SurveyBotUser
    {
        public int Id { get; set; }
        public bool Active { get; set; } = true;
        public int BotUserId { get; set; }
        public int SurveyId { get; set; }
        public DateTime JoinedDate { get; set; } = DateTime.UtcNow;

        public Survey Survey { get; set; } = null!;
        public BotUser BotUser { get; set; } = null!;
    }
}
