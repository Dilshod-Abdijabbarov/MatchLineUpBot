namespace LineUpBot.Domain.Models
{
    public class BotUser
    {
        public int Id { get; set; }
        public long ChatId { get; set; }
        public string? PhoneNumber { get; set; }
        /// <summary>
        /// keyingi comanda 
        /// </summary>
        public string? NextCommand { get; set; }
        public string? FirstName { get; set; }
        public string? UserName { get; set; }
        public string? LastName { get; set; }
        public int Score { get; set; }
        public bool Active { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
