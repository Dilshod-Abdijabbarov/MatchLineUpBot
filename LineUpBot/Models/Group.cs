namespace LineUpBot.Models
{
    public class Group
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
        public string Description { get; set; } = string.Empty;
        public int SurveyId { get; set; }
        public Survey? Survey { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
