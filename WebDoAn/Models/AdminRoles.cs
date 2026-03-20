using System.ComponentModel.DataAnnotations;

namespace WebDoAn.Models;

public class AdminRole
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Email { get; set; } = "";

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public string? AssignedBy { get; set; } // Email của admin gán quyền
}
