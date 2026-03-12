namespace WebDoAn.Models
{
    public class Like
    {
        public int Id { get; set; }
        public string FromUserId { get; set; }
        public string ToUserId { get; set; }
        public bool IsMatch { get; set; }
    }
}
