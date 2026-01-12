namespace LineUpBot.Models
{
    public class GroupUser
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public long ChatId { get; set; }
        public bool Active { get; set; }
    }
}
