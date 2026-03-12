using WebDoAn.Models;

namespace WebDoAn.Models
{
    public class ManageRoomViewModel
    {
        public RoomPost? Room { get; set; }
        public List<RoomTenant> Tenants { get; set; } = new();
    }
}