using Microsoft.AspNetCore.Mvc;
using WebDoAn.Data;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace WebDoAn.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        // 1. ĐỒNG BỘ IDENTITY VÀO SESSION (Sửa lỗi mất session khi vừa đăng ký)
        var email = HttpContext.Session.GetString("CURRENT_USER_EMAIL");

        if (string.IsNullOrEmpty(email) && User.Identity != null && User.Identity.IsAuthenticated)
        {
            email = User.Identity.Name; // Mặc định Identity dùng Email làm Name
            if (!string.IsNullOrEmpty(email))
            {
                HttpContext.Session.SetString("CURRENT_USER_EMAIL", email);
                var userAcc = _context.UserAccounts.FirstOrDefault(u => u.Email == email);
                if (userAcc != null)
                {
                    HttpContext.Session.SetString("CURRENT_USER_TYPE", userAcc.UserType ?? "Tenant");
                    HttpContext.Session.SetString("CURRENT_USER_NAME", userAcc.FullName ?? "");
                    HttpContext.Session.SetString("CURRENT_USER_AVATAR", userAcc.AvatarUrl ?? "");
                }
            }
        }

        // 2. KIỂM TRA SỞ THÍCH - CHUYỂN HƯỚNG ONBOARDING (Kiểu TikTok)
        if (!string.IsNullOrWhiteSpace(email))
        {
            var appUser = _context.Users.FirstOrDefault(u => u.Email == email || u.UserName == email);
            if (appUser != null && string.IsNullOrWhiteSpace(appUser.LifestyleTags))
            {
                // Nếu User chưa có Tag nào -> Ép sang trang chọn Tag
                return RedirectToAction("Onboarding", "Profile");
            }
        }

        // 3. CODE CŨ GIỮ NGUYÊN (Hiển thị phòng trên trang chủ)
        var rooms = _context.RoomPosts
            .Where(p => p.PostType == "Room")
            .ToList();

        return View(rooms);
    }

    public IActionResult Privacy() => View();
}