namespace LineUpBot.Models
{
    public class Survey
    {
        public int Id { get; set; }
        public int? MessageId { get; set; }
        public string Question { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
