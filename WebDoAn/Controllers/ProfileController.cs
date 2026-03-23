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

    public IActionResult Index()
    {
        var email = HttpContext.Session.GetString(CURRENT_EMAIL);
        if (string.IsNullOrEmpty(email)) return RedirectToAction("Login", "Account");

        var user = _context.UserAccounts.FirstOrDefault(x => x.Email == email);
        if (user == null) return RedirectToAction("Login", "Account");

        return View(user);
    }

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

        if (!string.IsNullOrEmpty(croppedAvatar) && croppedAvatar.Contains(","))
        {
            try
            {
                string base64Data = croppedAvatar.Split(',')[1];
                byte[] imageBytes = Convert.FromBase64String(base64Data);

                string fileName = $"avt_{Guid.NewGuid():N}.jpg";
                string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars");

                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                string filePath = Path.Combine(folderPath, fileName);
                DeleteLocalFile(user.AvatarUrl);
                System.IO.File.WriteAllBytes(filePath, imageBytes);
                user.AvatarUrl = $"/uploads/avatars/{fileName}";

                UpdateTenantAvatarAcrossRooms(user.Email, user.AvatarUrl);
            }
            catch (Exception ex)
            {
                TempData["AuthError"] = "Lỗi khi lưu ảnh: " + ex.Message;
            }
        }

        _context.SaveChanges();
        HttpContext.Session.SetString(CURRENT_AVATAR, user.AvatarUrl ?? "");
        HttpContext.Session.SetString(CURRENT_NAME, string.IsNullOrWhiteSpace(user.FullName) ? user.Email.Split('@')[0] : user.FullName);

        TempData["Message"] = "Cập nhật hồ sơ thành công.";
        return RedirectToAction("Index");
    }

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

    private void UpdateTenantAvatarAcrossRooms(string email, string avatarUrl)
    {
        var tenants = _context.RoomTenants.Where(t => t.Email == email).ToList();
        foreach (var t in tenants) t.AvatarUrl = avatarUrl;
    }

    private void DeleteLocalFile(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || !relativePath.StartsWith("/uploads/")) return;
        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath.TrimStart('/'));
        if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
    }

    // =========================================================
    // TRANG CHỌN TAG SỞ THÍCH (DÙNG ĐÚNG BẢNG UserAccounts)
    // =========================================================
    public IActionResult Onboarding()
    {
        var email = HttpContext.Session.GetString(CURRENT_EMAIL);

        if (string.IsNullOrEmpty(email) && User.Identity != null && User.Identity.IsAuthenticated)
        {
            email = User.Identity.Name;
            HttpContext.Session.SetString(CURRENT_EMAIL, email!);
            var userAcc = _context.UserAccounts.FirstOrDefault(u => u.Email == email);
            if (userAcc != null)
            {
                HttpContext.Session.SetString(CURRENT_TYPE, userAcc.UserType ?? "Tenant");
                HttpContext.Session.SetString(CURRENT_NAME, userAcc.FullName ?? "");
                HttpContext.Session.SetString(CURRENT_AVATAR, userAcc.AvatarUrl ?? "https://i.pravatar.cc/150?img=3");
            }
            else
            {
                HttpContext.Session.SetString(CURRENT_TYPE, "Tenant");
            }
        }

        if (string.IsNullOrEmpty(email)) return Redirect("/Identity/Account/Login");

        // Đã sửa thành UserAccounts
        var user = _context.UserAccounts.FirstOrDefault(u => u.Email.ToLower() == email.ToLower());
        ViewBag.CurrentTags = user?.LifestyleTags ?? "";

        return View();
    }

    [HttpPost]
    public IActionResult SaveOnboarding(string LifestyleTags)
    {
        var email = HttpContext.Session.GetString(CURRENT_EMAIL) ?? User.Identity?.Name;
        if (string.IsNullOrEmpty(email)) return Redirect("/Identity/Account/Login");

        // Đã sửa thành UserAccounts
        var user = _context.UserAccounts.FirstOrDefault(x => x.Email.ToLower() == email.ToLower());
        if (user != null)
        {
            user.LifestyleTags = LifestyleTags;
            _context.SaveChanges();
        }

        return RedirectToAction("RecommendedRooms", "Room");
    }
    [HttpPost]
    public IActionResult ChangePassword(string currentPassword, string newPassword, string confirmPassword)
    {
        var email = HttpContext.Session.GetString(CURRENT_EMAIL);
        if (string.IsNullOrEmpty(email)) return RedirectToAction("Login", "Account");

        var user = _context.UserAccounts.FirstOrDefault(x => x.Email == email);
        if (user == null) return RedirectToAction("Login", "Account");

        if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
        {
            TempData["AuthError"] = "Vui lòng nhập đầy đủ thông tin mật khẩu.";
            return RedirectToAction("Edit");
        }

        if (user.Password != currentPassword)
        {
            TempData["AuthError"] = "Mật khẩu hiện tại không chính xác.";
            return RedirectToAction("Edit");
        }

        if (newPassword != confirmPassword)
        {
            TempData["AuthError"] = "Mật khẩu xác nhận không khớp.";
            return RedirectToAction("Edit");
        }

        if (newPassword.Length < 8)
        {
            TempData["AuthError"] = "Mật khẩu mới phải có ít nhất 8 ký tự.";
            return RedirectToAction("Edit");
        }

        // Cập nhật mật khẩu mới vào Database
        user.Password = newPassword;
        _context.SaveChanges();

        TempData["Message"] = "Đổi mật khẩu thành công!";
        return RedirectToAction("Edit");
    }
}