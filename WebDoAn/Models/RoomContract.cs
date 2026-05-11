using System.ComponentModel.DataAnnotations;

namespace WebDoAn.Models;

public class RoomContract
{
    [Key]
    public int Id { get; set; }
    public int RoomPostId { get; set; }

    public string LandlordEmail { get; set; } = "";
    public string TenantEmail { get; set; } = "";

    // NỘI DUNG HỢP ĐỒNG (Lưu dưới dạng HTML hoặc Text)
    public string ContractContent { get; set; } = "";

    // DỮ LIỆU eKYC CỦA NGƯỜI THUÊ
    public string? TenantFrontIdCardUrl { get; set; }
    public string? TenantBackIdCardUrl { get; set; }
    public string? TenantFaceImageUrl { get; set; } // Dùng ảnh chụp từ Webcam thay cho video để dễ xử lý

    // CHỮ KÝ & XÁC THỰC
    public string? TenantSignatureUrl { get; set; } // Ảnh chữ ký Base64
    public string? OtpCode { get; set; }
    public DateTime? OtpExpiry { get; set; }

    // TÍNH PHÁP LÝ & BẢO MẬT
    public string Status { get; set; } = "Pending"; // Pending, eKYC_Done, Signed
    public string? DocumentHash { get; set; } // Mã băm SHA-256 chống chỉnh sửa hợp đồng
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? SignedAt { get; set; }
}