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

    public ProfileController(ApplicationDbContext context)
    {
        _context = context;
    }

    public IActionResult Edit()
    {
        var email = HttpContext.Session.GetString(CURRENT_EMAIL);
        var type = HttpContext.Session.GetString(CURRENT_TYPE);

        if (string.IsNullOrEmpty(email)) return RedirectToAction("Login", "Account");
        if (type != "Tenant") return RedirectToAction("Index", "Home");

        var user = _context.UserAccounts.FirstOrDefault(x => x.Email == email);
        if (user == null) return RedirectToAction("Login", "Account");

        return View(user);
    }

    [HttpPost]
    public IActionResult Edit(string fullName, string birthYear, string gender, string hometown, IFormFile? avatar)
    {
        var email = HttpContext.Session.GetString(CURRENT_EMAIL);
        var type = HttpContext.Session.GetString(CURRENT_TYPE);

        if (string.IsNullOrEmpty(email)) return RedirectToAction("Login", "Account");
        if (type != "Tenant") return RedirectToAction("Index", "Home");

        var user = _context.UserAccounts.FirstOrDefault(x => x.Email == email);
        if (user == null) return RedirectToAction("Login", "Account");

        user.FullName = (fullName ?? "").Trim();
        user.BirthYear = (birthYear ?? "").Trim();
        user.Gender = (gender ?? "").Trim();
        user.Hometown = (hometown ?? "").Trim();

        var avatarPrefix = $"{user.Email}_{user.FullName}";
        var newAvatar = SaveUpload(avatar, avatarPrefix, "avatars");

        if (!string.IsNullOrEmpty(newAvatar))
        {
            DeleteLocalFile(user.AvatarUrl);

            user.AvatarUrl = newAvatar;
            UpdateTenantAvatarAcrossRooms(user.Email, newAvatar);
        }

        _context.SaveChanges();

        TempData["Message"] = "Cập nhật hồ sơ thành công.";
        return RedirectToAction("Index");
    }

    public IActionResult Index()
    {
        var email = HttpContext.Session.GetString(CURRENT_EMAIL);
        var type = HttpContext.Session.GetString(CURRENT_TYPE);

        if (string.IsNullOrEmpty(email)) return RedirectToAction("Login", "Account");
        if (type != "Tenant") return RedirectToAction("Index", "Home");

        var user = _context.UserAccounts.FirstOrDefault(x => x.Email == email);
        if (user == null) return RedirectToAction("Login", "Account");

        return View(user);
    }

    [HttpPost]
    public IActionResult JoinRoomByCode(string joinCode)
    {
        var email = HttpContext.Session.GetString(CURRENT_EMAIL);
        var type = HttpContext.Session.GetString(CURRENT_TYPE);

        if (string.IsNullOrEmpty(email)) return RedirectToAction("Login", "Account");
        if (type != "Tenant") return RedirectToAction("Index", "Home");

        joinCode = (joinCode ?? "").Trim();

        if (string.IsNullOrWhiteSpace(joinCode))
        {
            TempData["AuthError"] = "Vui lòng nhập mã phòng.";
            return RedirectToAction("Index");
        }

        var user = _context.UserAccounts.FirstOrDefault(x => x.Email == email);
        if (user == null)
        {
            TempData["AuthError"] = "Không tìm thấy tài khoản.";
            return RedirectToAction("Index");
        }

        var room = _context.RoomPosts
            .Include(x => x.Tenants)
            .FirstOrDefault(x => x.PostType == "Room" && x.JoinCode == joinCode);

        if (room == null)
        {
            TempData["AuthError"] = "Mã phòng không hợp lệ.";
            return RedirectToAction("Index");
        }

        if (room.Tenants.Any(x => x.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
        {
            TempData["AuthError"] = "Bạn đã tham gia phòng này rồi.";
            return RedirectToAction("Index");
        }

        room.Tenants.Add(new RoomTenant
        {
            RoomPostId = room.Id,
            Name = string.IsNullOrWhiteSpace(user.FullName) ? user.Email : user.FullName,
            Email = user.Email,
            Phone = "",
            AvatarUrl = string.IsNullOrWhiteSpace(user.AvatarUrl)
                ? "https://i.pravatar.cc/150?img=3"
                : user.AvatarUrl
        });

        room.IsRented = room.Tenants.Any();
        _context.SaveChanges();

        TempData["Message"] = $"Tham gia phòng \"{room.Title}\" thành công.";
        return RedirectToAction("Index");
    }

    private void UpdateTenantAvatarAcrossRooms(string email, string avatarUrl)
    {
        var tenants = _context.RoomTenants
            .Where(t => t.Email == email)
            .ToList();

        foreach (var tenant in tenants)
        {
            tenant.AvatarUrl = avatarUrl;
        }
    }

    private string Slugify(string? text)
    {
        text = (text ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(text)) return "empty";

        text = RemoveVietnameseSigns(text);
        text = Regex.Replace(text, @"\s+", "_");
        text = Regex.Replace(text, @"[^a-z0-9_@.-]", "");
        return string.IsNullOrWhiteSpace(text) ? "empty" : text;
    }

    private string RemoveVietnameseSigns(string text)
    {
        text = text.Replace("đ", "d").Replace("Đ", "D");
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private string? SaveUpload(IFormFile? file, string prefix, string folder)
    {
        if (file == null || file.Length == 0) return null;

        var ext = Path.GetExtension(file.FileName);
        var safePrefix = Slugify(prefix);
        var fileName = $"{safePrefix}{ext}";

        var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", folder);
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, fileName);
        using var stream = System.IO.File.Create(path);
        file.CopyTo(stream);

        return $"/uploads/{folder}/{fileName}";
    }

    private void DeleteLocalFile(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;
        if (!relativePath.StartsWith("/uploads/")) return;

        var relative = relativePath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString());
        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relative);

        if (System.IO.File.Exists(fullPath))
        {
            System.IO.File.Delete(fullPath);
        }
    }
}