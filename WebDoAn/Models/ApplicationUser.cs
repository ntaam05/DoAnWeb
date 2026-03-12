using Microsoft.AspNetCore.Identity;

namespace WebDoAn.Models
{
    public enum UserType
    {
        Tenant,
        Landlord
    }

    public class ApplicationUser : IdentityUser
    {
        public UserType UserType { get; set; }
        public string? LifestyleTags { get; set; }
        public bool IsBanned { get; set; }
    }
}