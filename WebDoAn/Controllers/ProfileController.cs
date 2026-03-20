using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;
using WebDoAn.Data;
using WebDoAn.Models;

namespace WebDoAn.Controllers;

public class ProfileController : Controller
{
    private readonly ApplicationDbContext _context;

    private const string CURRENT_EMAIL = "CURRENT_USER_EMAIL";
    private const string CURRENT_TYPE = "CURRENT_USER_TYPE";
    private const string CURRENT_AVATAR = "CURRENT_USER_AVATAR";
    private const string CURRENT_NAME = "CURRENT_USER_NAME";

    public ProfileController(ApplicationDbContext context)
    {
        _context = context;
    }

    // TRANG XEM HỒ SƠ (Cho cả Chủ trọ và Người thuê)
    public IActionResult Index()
    {
        var email = HttpContext.Session.GetString(CURRENT_EMAIL);
        if (string.IsNullOrEmpty(email)) return RedirectToAction("Login", "Account");

        var user = _context.UserAccounts.FirstOrDefault(x => x.Email == email);
        if (user == null) return RedirectToAction("Login", "Account");

        return View(user);
    }

    // TRANG CHỈNH SỬA (Cho cả Chủ trọ và Người thuê)
    public IActionResult Edit()
    {
        var email = HttpContext.Session.GetString(CURRENT_EMAIL);
        if (string.IsNullOrEmpty(email)) return RedirectToAction("Login", "Account");

        var user = _context.UserAccounts.FirstOrDefault(x => x.Email == email);
        if (user == null) return RedirectToAction("Login", "Account");

        return View(user);
    }

    [HttpPost]
    public IActionResult Edit(string fullName, string birthYear, string gender, string hometown, string? croppedAvatar)
    {
        var email = HttpContext.Session.GetString(CURRENT_EMAIL);
        if (string.IsNullOrEmpty(email)) return RedirectToAction("Login", "Account");

        var user = _context.UserAccounts.FirstOrDefault(x => x.Email == email);
        if (user == null) return RedirectToAction("Login", "Account");

        user.FullName = (fullName ?? "").Trim();
        user.BirthYear = (birthYear ?? "").Trim();
        user.Gender = (gender ?? "").Trim();
        user.Hometown = (hometown ?? "").Trim();

        // XỬ LÝ ẢNH ĐÃ CẮT (Dạng Base64 từ Cropper.js)
        if (!string.IsNullOrEmpty(croppedAvatar) && croppedAvatar.Contains(","))
        {
            try
            {
                // Tách chuỗi base64 (bỏ phần data:image/jpeg;base64,)
                string base64Data = croppedAvatar.Split(',')[1];
                byte[] imageBytes = Convert.FromBase64String(base64Data);

                // Tạo tên file duy nhất
                string fileName = $"avt_{Guid.NewGuid():N}.jpg";
                string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars");

                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                string filePath = Path.Combine(folderPath, fileName);

                // Xóa ảnh cũ nếu có
                DeleteLocalFile(user.AvatarUrl);

                // Lưu file mới
                System.IO.File.WriteAllBytes(filePath, imageBytes);
                user.AvatarUrl = $"/uploads/avatars/{fileName}";

                // Đồng bộ ảnh vào các bảng liên quan nếu là Người thuê
                UpdateTenantAvatarAcrossRooms(user.Email, user.AvatarUrl);
            }
            catch (Exception ex)
            {
                TempData["AuthError"] = "Lỗi khi lưu ảnh: " + ex.Message;
            }
        }

        _context.SaveChanges();

        // CẬP NHẬT SESSION ĐỂ NAVBAR ĐỔI NGAY LẬP TỨC
        HttpContext.Session.SetString(CURRENT_AVATAR, user.AvatarUrl ?? "");
        HttpContext.Session.SetString(CURRENT_NAME, string.IsNullOrWhiteSpace(user.FullName) ? user.Email.Split('@')[0] : user.FullName);

        TempData["Message"] = "Cập nhật hồ sơ thành công.";
        return RedirectToAction("Index");
    }

    // CHỈ NGƯỜI THUÊ MỚI CÓ QUYỀN DÙNG MÃ THAM GIA PHÒNG
    [HttpPost]
    public IActionResult JoinRoomByCode(string joinCode)
    {
        var email = HttpContext.Session.GetString(CURRENT_EMAIL);
        var type = HttpContext.Session.GetString(CURRENT_TYPE);

        if (string.IsNullOrEmpty(email) || type != "Tenant")
        {
            TempData["AuthError"] = "Chỉ người thuê mới có thể tham gia phòng.";
            return RedirectToAction("Index");
        }

        joinCode = (joinCode ?? "").Trim();
        var user = _context.UserAccounts.FirstOrDefault(x => x.Email == email);
        var room = _context.RoomPosts.Include(x => x.Tenants)
                           .FirstOrDefault(x => x.PostType == "Room" && x.JoinCode == joinCode);

        if (room == null)
        {
            TempData["AuthError"] = "Mã phòng không chính xác.";
            return RedirectToAction("Index");
        }

        if (room.Tenants.Any(x => x.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
        {
            TempData["AuthError"] = "Bạn đã có mặt trong phòng này.";
            return RedirectToAction("Index");
        }

        room.Tenants.Add(new RoomTenant
        {
            RoomPostId = room.Id,
            Name = string.IsNullOrWhiteSpace(user!.FullName) ? user.Email : user.FullName,
            Email = user.Email,
            Phone = "",
            AvatarUrl = user.AvatarUrl ?? "https://i.pravatar.cc/150?img=3"
        });

        _context.SaveChanges();
        TempData["Message"] = $"Chào mừng bạn đến với phòng: {room.Title}";
        return RedirectToAction("Index");
    }

    // Helper: Cập nhật ảnh đại diện ở các phòng đang ở
    private void UpdateTenantAvatarAcrossRooms(string email, string avatarUrl)
    {
        var tenants = _context.RoomTenants.Where(t => t.Email == email).ToList();
        foreach (var t in tenants) t.AvatarUrl = avatarUrl;
    }

    // Helper: Xóa file vật lý
    private void DeleteLocalFile(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || !relativePath.StartsWith("/uploads/")) return;
        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath.TrimStart('/'));
        if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
    }
}