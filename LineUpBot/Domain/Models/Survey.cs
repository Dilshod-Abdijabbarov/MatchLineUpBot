namespace LineUpBot.Domain.Models
{
    public class Survey
    {
        public int Id { get; set; }
        public int? MessageId { get; set; }
        public string Question { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public int CurrentWeek { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
