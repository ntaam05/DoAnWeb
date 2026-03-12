namespace WebDoAn.Models
{
    public class RoomComment
    {
        public string UserEmail { get; set; } = "";
        public string Content { get; set; } = "";
        public int Rating { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}