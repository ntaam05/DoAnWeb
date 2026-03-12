namespace WebDoAn.Models
{
    public class CommentVM
    {
        public string UserName { get; set; }
        public int Rating { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}