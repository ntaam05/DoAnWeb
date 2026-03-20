using System.ComponentModel.DataAnnotations.Schema;

namespace WebDoAn.Models;

public class RoomPost
{
    public int Id { get; set; }

    public string PostType { get; set; } = "Room";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Price { get; set; } = 0;

    public string ImageUrlsData { get; set; } = "";

    [NotMapped]
    public List<string> ImageUrls
    {
        get => string.IsNullOrWhiteSpace(ImageUrlsData)
            ? new List<string>()
            : ImageUrlsData.Split("||", StringSplitOptions.RemoveEmptyEntries).ToList();

        set => ImageUrlsData = value == null
            ? ""
            : string.Join("||", value.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    public string OwnerId { get; set; } = "";
    public bool IsRented { get; set; } = false;

    public List<RoomTenant> Tenants { get; set; } = new();

    public string JoinCode { get; set; } = "";
    public string? MapLink { get; set; }
    public string? Hashtags { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}