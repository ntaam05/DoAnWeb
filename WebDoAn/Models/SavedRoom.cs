namespace WebDoAn.Models;

public class SavedRoom
{
    public int Id { get; set; }

    public string UserEmail { get; set; } = "";

    public int RoomPostId { get; set; }
    public RoomPost? RoomPost { get; set; }
}