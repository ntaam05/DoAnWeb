using System.ComponentModel.DataAnnotations;

namespace WebDoAn.Models;

public class UserAccount
{
    [Key]
    public string Email { get; set; } = "";

    public string Password { get; set; } = "";
    public string UserType { get; set; } = "Tenant";

    public string FullName { get; set; } = "";
    public string BirthYear { get; set; } = "";
    public string Gender { get; set; } = "";
    public string Hometown { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
}