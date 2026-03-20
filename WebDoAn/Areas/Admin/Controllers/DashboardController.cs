using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebDoAn.Data;
using WebDoAn.Models;

namespace WebDoAn.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(ApplicationDbContext context, ILogger<DashboardController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private string? GetCurrentUserEmail()
        {
            return HttpContext.Session.GetString("CURRENT_USER_EMAIL")?.Trim().ToLowerInvariant();
        }

        private bool IsAdmin(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            email = email.Trim().ToLowerInvariant();
            return _context.AdminRoles.Any(a => a.Email.ToLower() == email);
        }

        private IActionResult? CheckAdminAccess()
        {
            var email = GetCurrentUserEmail();

            if (string.IsNullOrWhiteSpace(email))
            {
                return Redirect("/Identity/Account/Login?ReturnUrl=/Admin");
            }

            if (!IsAdmin(email))
            {
                return Redirect("/Identity/Account/AccessDenied");
            }

            return null;
        }

        public IActionResult Index()
        {
            var denied = CheckAdminAccess();
            if (denied != null) return denied;

            var stats = new
            {
                TotalUsers = _context.UserAccounts.Count(),
                TotalTenants = _context.UserAccounts.Count(u => u.UserType == "Tenant"),
                TotalLandlords = _context.UserAccounts.Count(u => u.UserType == "Landlord"),
                TotalAdmins = _context.AdminRoles.Count(),
                TotalRoomPosts = _context.RoomPosts.Count(r => r.PostType == "Room"),
                TotalFindPosts = _context.RoomPosts.Count(r => r.PostType == "Find"),
                RentedRooms = _context.RoomPosts.Count(r => r.PostType == "Room" && r.IsRented)
            };

            ViewBag.Stats = stats;
            return View();
        }

        public IActionResult ManageAdmins()
        {
            var denied = CheckAdminAccess();
            if (denied != null) return denied;

            var admins = _context.AdminRoles
                .OrderByDescending(a => a.AssignedAt)
                .ToList();

            return View(admins);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddAdmin(string email)
        {
            var denied = CheckAdminAccess();
            if (denied != null) return denied;

            email = (email ?? "").Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(email))
            {
                TempData["Error"] = "Email không hợp lệ.";
                return RedirectToAction(nameof(ManageAdmins));
            }

            var user = _context.UserAccounts.FirstOrDefault(u => u.Email.ToLower() == email);
            if (user == null)
            {
                TempData["Error"] = "Email này chưa đăng ký.";
                return RedirectToAction(nameof(ManageAdmins));
            }

            var existingAdmin = _context.AdminRoles.FirstOrDefault(a => a.Email.ToLower() == email);
            if (existingAdmin != null)
            {
                TempData["Error"] = "Người này đã là Admin.";
                return RedirectToAction(nameof(ManageAdmins));
            }

            var currentAdmin = GetCurrentUserEmail();

            _context.AdminRoles.Add(new AdminRole
            {
                Email = email,
                AssignedBy = currentAdmin,
                AssignedAt = DateTime.UtcNow
            });

            _context.SaveChanges();

            TempData["Message"] = $"Đã cấp quyền Admin cho {email}.";
            return RedirectToAction(nameof(ManageAdmins));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveAdmin(int adminId)
        {
            var denied = CheckAdminAccess();
            if (denied != null) return denied;

            var admin = _context.AdminRoles.FirstOrDefault(a => a.Id == adminId);
            if (admin == null)
            {
                TempData["Error"] = "Không tìm thấy Admin.";
                return RedirectToAction(nameof(ManageAdmins));
            }

            var currentAdmin = GetCurrentUserEmail();
            if (!string.IsNullOrWhiteSpace(currentAdmin) &&
                admin.Email.Trim().ToLower() == currentAdmin)
            {
                TempData["Error"] = "Không thể xóa quyền Admin của chính mình.";
                return RedirectToAction(nameof(ManageAdmins));
            }

            _context.AdminRoles.Remove(admin);
            _context.SaveChanges();

            TempData["Message"] = $"Đã xóa quyền Admin của {admin.Email}.";
            return RedirectToAction(nameof(ManageAdmins));
        }

        public IActionResult ManageUsers()
        {
            var denied = CheckAdminAccess();
            if (denied != null) return denied;

            var users = _context.UserAccounts
                .OrderByDescending(u => u.CreatedAt)
                .ToList();

            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BanUser(string email)
        {
            var denied = CheckAdminAccess();
            if (denied != null) return denied;

            email = (email ?? "").Trim().ToLowerInvariant();

            var user = _context.UserAccounts.FirstOrDefault(u => u.Email.ToLower() == email);
            if (user == null)
            {
                TempData["Error"] = "Không tìm thấy người dùng.";
                return RedirectToAction(nameof(ManageUsers));
            }

            user.IsBanned = !user.IsBanned;
            _context.SaveChanges();

            TempData["Message"] = user.IsBanned
                ? "Đã ban người dùng."
                : "Đã bỏ ban người dùng.";

            return RedirectToAction(nameof(ManageUsers));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteUser(string email)
        {
            var denied = CheckAdminAccess();
            if (denied != null) return denied;

            email = (email ?? "").Trim().ToLowerInvariant();

            var user = _context.UserAccounts.FirstOrDefault(u => u.Email.ToLower() == email);
            if (user == null)
            {
                TempData["Error"] = "Không tìm thấy người dùng.";
                return RedirectToAction(nameof(ManageUsers));
            }

            _context.UserAccounts.Remove(user);
            _context.SaveChanges();

            TempData["Message"] = "Đã xóa người dùng.";
            return RedirectToAction(nameof(ManageUsers));
        }

        public IActionResult ManageRoomPosts()
        {
            var denied = CheckAdminAccess();
            if (denied != null) return denied;

            var posts = _context.RoomPosts
                .Where(r => r.PostType == "Room")
                .Include(r => r.Tenants)
                .OrderByDescending(r => r.Id)
                .ToList();

            return View(posts);
        }

        public IActionResult ViewRoomDetails(int postId)
        {
            var denied = CheckAdminAccess();
            if (denied != null) return denied;

            var post = _context.RoomPosts
                .Include(r => r.Tenants)
                .FirstOrDefault(r => r.Id == postId && r.PostType == "Room");

            if (post == null) return NotFound();

            return View(post);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteRoomPost(int postId)
        {
            var denied = CheckAdminAccess();
            if (denied != null) return denied;

            var post = _context.RoomPosts.FirstOrDefault(r => r.Id == postId && r.PostType == "Room");
            if (post == null)
            {
                TempData["Error"] = "Không tìm thấy bài đăng.";
                return RedirectToAction(nameof(ManageRoomPosts));
            }

            _context.RoomPosts.Remove(post);
            _context.SaveChanges();

            TempData["Message"] = "Đã xóa bài đăng phòng cho thuê.";
            return RedirectToAction(nameof(ManageRoomPosts));
        }

        public IActionResult ManageFindPosts()
        {
            var denied = CheckAdminAccess();
            if (denied != null) return denied;

            var posts = _context.RoomPosts
                .Where(r => r.PostType == "Find")
                .OrderByDescending(r => r.Id)
                .ToList();

            return View(posts);
        }

        public IActionResult ViewFindDetails(int postId)
        {
            var denied = CheckAdminAccess();
            if (denied != null) return denied;

            var post = _context.RoomPosts
                .FirstOrDefault(r => r.Id == postId && r.PostType == "Find");

            if (post == null) return NotFound();

            return View(post);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteFindPost(int postId)
        {
            var denied = CheckAdminAccess();
            if (denied != null) return denied;

            var post = _context.RoomPosts.FirstOrDefault(r => r.Id == postId && r.PostType == "Find");
            if (post == null)
            {
                TempData["Error"] = "Không tìm thấy bài đăng.";
                return RedirectToAction(nameof(ManageFindPosts));
            }

            _context.RoomPosts.Remove(post);
            _context.SaveChanges();

            TempData["Message"] = "Đã xóa bài đăng tìm người.";
            return RedirectToAction(nameof(ManageFindPosts));
        }
    }
}