namespace WebDoAn.Models
{
    public class RoomComment
    {
        public int Id { get; set; }
        public string UserEmail { get; set; } = "";
        public string Content { get; set; } = "";
        public int? Rating { get; set; }
        public DateTime CreatedAt { get; set; }
        public int RoomPostId { get; set; }
        public int? ReplyToCommentId { get; set; }
        public string? Reaction { get; set; }
        public RoomPost? RoomPost { get; set; }
        
    }
}