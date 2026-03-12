namespace WebDoAn.Models;

public class RoomTenant
{
    public int Id { get; set; }

    public int RoomPostId { get; set; }
    public RoomPost? RoomPost { get; set; }

    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
}