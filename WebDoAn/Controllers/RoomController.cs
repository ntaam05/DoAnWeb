using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;
using WebDoAn.Data;
using WebDoAn.Models;

namespace WebDoAn.Controllers;

public class RoomController : Controller
{
    private readonly ApplicationDbContext _context;

    private const string CURRENT_EMAIL = "CURRENT_USER_EMAIL";
    private const string CURRENT_TYPE = "CURRENT_USER_TYPE";

    public RoomController(ApplicationDbContext context)
    {
        _context = context;
    }

    private string? Email => HttpContext.Session.GetString(CURRENT_EMAIL);
    private string? UserType => HttpContext.Session.GetString(CURRENT_TYPE);
    private bool IsLogin => !string.IsNullOrEmpty(Email);

    public IActionResult Create()
    {
        if (!IsLogin || UserType != "Landlord")
            return RedirectToAction("Index", "Home");

        return View(new RoomPost());
    }

    [HttpPost]
    public IActionResult Create(RoomPost room, List<IFormFile>? images)
    {
        if (!IsLogin || UserType != "Landlord")
            return RedirectToAction("Index", "Home");

        var imageUrls = new List<string>();

        if (images != null && images.Count > 0)
        {
            int index = 1;
            foreach (var image in images)
            {
                if (image == null || image.Length == 0)
                    continue;

                var prefix = $"room_{Email}_{room.Title}_{index}";
                var url = SaveUpload(image, prefix, "uploads/rooms");

                if (!string.IsNullOrWhiteSpace(url))
                {
                    imageUrls.Add(url);
                }

                index++;
            }
        }

        if (imageUrls.Count == 0)
        {
            imageUrls.Add($"https://picsum.photos/seed/{Guid.NewGuid():N}/800/500");
        }

        room.OwnerId = Email!;
        room.PostType = "Room";
        room.Tenants = new List<RoomTenant>();
        room.IsRented = false;
        room.ImageUrls = imageUrls;

        _context.RoomPosts.Add(room);
        _context.SaveChanges();

        room.JoinCode = BuildJoinCode(room.Id, room.OwnerId);
        _context.SaveChanges();

        TempData["Message"] = "Đăng phòng thành công.";
        return RedirectToAction("MyRooms");
    }

    public IActionResult CreateFind()
    {
        if (!IsLogin || UserType != "Tenant")
            return RedirectToAction("Index", "Home");

        return View(new RoomPost());
    }

    [HttpPost]
    public IActionResult CreateFind(RoomPost post)
    {
        if (!IsLogin || UserType != "Tenant")
            return RedirectToAction("Index", "Home");

        post.OwnerId = Email!;
        post.PostType = "Find";

        _context.RoomPosts.Add(post);
        _context.SaveChanges();

        return RedirectToAction("FindFeed");
    }

    public IActionResult FindFeed()
    {
        var posts = _context.RoomPosts
            .Where(x => x.PostType == "Find")
            .ToList();

        return View(posts);
    }

    public IActionResult Detail(int id)
    {
        var post = _context.RoomPosts
            .Include(x => x.Tenants)
            .FirstOrDefault(x => x.Id == id);

        if (post == null) return NotFound();

        var email = HttpContext.Session.GetString(CURRENT_EMAIL);
        if (!string.IsNullOrEmpty(email))
        {
            ViewBag.IsSaved = _context.SavedRooms.Any(x => x.UserEmail == email && x.RoomPostId == id);
        }
        else
        {
            ViewBag.IsSaved = false;
        }

        ViewBag.Comments = new List<RoomComment>();
        ViewBag.AvgRate = 0.0;

        return View(post);
    }

    public IActionResult MyRooms()
    {
        if (!IsLogin || UserType != "Landlord")
            return RedirectToAction("Index", "Home");

        var rooms = _context.RoomPosts
            .Include(x => x.Tenants)
            .Where(x => x.PostType == "Room" && x.OwnerId == Email)
            .ToList();

        return View(rooms);
    }

    public IActionResult Manage(int id)
    {
        if (!IsLogin || UserType != "Landlord")
            return RedirectToAction("Index", "Home");

        var room = _context.RoomPosts
            .Include(x => x.Tenants)
            .FirstOrDefault(x => x.Id == id && x.PostType == "Room" && x.OwnerId == Email);

        if (room == null)
        {
            TempData["AuthError"] = "Không tìm thấy phòng.";
            return RedirectToAction("MyRooms");
        }

        if (string.IsNullOrWhiteSpace(room.JoinCode))
        {
            room.JoinCode = BuildJoinCode(room.Id, room.OwnerId);
            _context.SaveChanges();
        }

        room.IsRented = room.Tenants.Any();
        _context.SaveChanges();

        var vm = new ManageRoomViewModel
        {
            Room = room,
            Tenants = room.Tenants
        };

        return View(vm);
    }

    [HttpPost]
    public IActionResult RegenerateJoinCodeAjax(int roomPostId)
    {
        if (!IsLogin || UserType != "Landlord")
            return Json(new { success = false, message = "Chưa đăng nhập." });

        var room = _context.RoomPosts
            .FirstOrDefault(x => x.Id == roomPostId && x.PostType == "Room" && x.OwnerId == Email);

        if (room == null)
            return Json(new { success = false, message = "Không tìm thấy phòng." });

        room.JoinCode = BuildJoinCode(room.Id, room.OwnerId);
        _context.SaveChanges();

        return Json(new
        {
            success = true,
            joinCode = room.JoinCode
        });
    }

    [HttpPost]
    public IActionResult AddTenant(int roomPostId, string name, string email, string phone, IFormFile? avatar)
    {
        if (!IsLogin || UserType != "Landlord")
            return RedirectToAction("Index", "Home");

        var room = _context.RoomPosts
            .Include(x => x.Tenants)
            .FirstOrDefault(x => x.Id == roomPostId && x.PostType == "Room" && x.OwnerId == Email);

        if (room == null)
        {
            TempData["AuthError"] = "Không tìm thấy phòng.";
            return RedirectToAction("MyRooms");
        }

        name = (name ?? "").Trim();
        email = (email ?? "").Trim().ToLowerInvariant();
        phone = (phone ?? "").Trim();

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(phone))
        {
            TempData["AuthError"] = "Vui lòng nhập đầy đủ thông tin người thuê.";
            return RedirectToAction("Manage", new { id = roomPostId });
        }

        var registeredUser = _context.UserAccounts.FirstOrDefault(x => x.Email == email);
        if (registeredUser == null)
        {
            TempData["AuthError"] = "Email này chưa được đăng ký.";
            return RedirectToAction("Manage", new { id = roomPostId });
        }

        if (registeredUser.UserType != "Tenant")
        {
            TempData["AuthError"] = "Email này không phải tài khoản Người thuê.";
            return RedirectToAction("Manage", new { id = roomPostId });
        }

        if (room.Tenants.Any(x => x.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
        {
            TempData["AuthError"] = "Người thuê này đã có trong phòng.";
            return RedirectToAction("Manage", new { id = roomPostId });
        }

        string avatarPath;
        var tenantPrefix = $"{registeredUser.Email}_{registeredUser.FullName}";
        var uploadedAvatar = SaveUpload(avatar, tenantPrefix, "uploads/avatars");

        if (!string.IsNullOrEmpty(uploadedAvatar))
            avatarPath = uploadedAvatar;
        else if (!string.IsNullOrWhiteSpace(registeredUser.AvatarUrl))
            avatarPath = registeredUser.AvatarUrl;
        else
            avatarPath = "https://i.pravatar.cc/150?img=3";

        room.Tenants.Add(new RoomTenant
        {
            RoomPostId = roomPostId,
            Name = string.IsNullOrWhiteSpace(registeredUser.FullName) ? registeredUser.Email : registeredUser.FullName,
            Email = registeredUser.Email,
            Phone = phone,
            AvatarUrl = avatarPath
        });

        room.IsRented = room.Tenants.Any();
        _context.SaveChanges();

        TempData["Message"] = "Đã thêm người thuê.";
        return RedirectToAction("Manage", new { id = roomPostId });
    }

    [HttpPost]
    public IActionResult DeleteTenant(int roomPostId, int tenantId)
    {
        if (!IsLogin || UserType != "Landlord")
            return RedirectToAction("Index", "Home");

        var room = _context.RoomPosts
            .Include(x => x.Tenants)
            .FirstOrDefault(x => x.Id == roomPostId && x.PostType == "Room" && x.OwnerId == Email);

        if (room == null)
        {
            TempData["AuthError"] = "Không tìm thấy phòng.";
            return RedirectToAction("MyRooms");
        }

        var tenant = room.Tenants.FirstOrDefault(x => x.Id == tenantId);
        if (tenant == null)
        {
            TempData["AuthError"] = "Không tìm thấy người thuê.";
            return RedirectToAction("Manage", new { id = roomPostId });
        }

        DeleteLocalFile(tenant.AvatarUrl);

        _context.RoomTenants.Remove(tenant);
        room.IsRented = room.Tenants.Count > 1;

        _context.SaveChanges();

        TempData["Message"] = "Đã xóa người thuê.";
        return RedirectToAction("Manage", new { id = roomPostId });
    }

    [HttpPost]
    public IActionResult DeleteRoom(int id)
    {
        if (!IsLogin || UserType != "Landlord")
            return RedirectToAction("Index", "Home");

        var room = _context.RoomPosts
            .Include(x => x.Tenants)
            .FirstOrDefault(x => x.Id == id && x.PostType == "Room" && x.OwnerId == Email);

        if (room == null)
        {
            TempData["AuthError"] = "Không tìm thấy phòng.";
            return RedirectToAction("MyRooms");
        }

        foreach (var image in room.ImageUrls)
        {
            DeleteLocalFile(image);
        }

        _context.RoomPosts.Remove(room);
        _context.SaveChanges();

        TempData["Message"] = "Đã xóa phòng.";
        return RedirectToAction("MyRooms");
    }

    [HttpPost]
    public IActionResult SaveRoom(int roomId)
    {
        var email = HttpContext.Session.GetString(CURRENT_EMAIL);
        if (string.IsNullOrEmpty(email))
            return RedirectToAction("Login", "Account");

        var room = _context.RoomPosts.FirstOrDefault(x => x.Id == roomId && x.PostType == "Room");
        if (room == null)
        {
            TempData["AuthError"] = "Không tìm thấy phòng.";
            return RedirectToAction("Index", "Home");
        }

        var existed = _context.SavedRooms.FirstOrDefault(x => x.UserEmail == email && x.RoomPostId == roomId);
        if (existed == null)
        {
            _context.SavedRooms.Add(new SavedRoom
            {
                UserEmail = email,
                RoomPostId = roomId
            });
            _context.SaveChanges();
            TempData["Message"] = "Đã lưu phòng.";
        }

        return RedirectToAction("Detail", new { id = roomId });
    }

    [HttpPost]
    public IActionResult UnsaveRoom(int roomId)
    {
        var email = HttpContext.Session.GetString(CURRENT_EMAIL);
        if (string.IsNullOrEmpty(email))
            return RedirectToAction("Login", "Account");

        var saved = _context.SavedRooms.FirstOrDefault(x => x.UserEmail == email && x.RoomPostId == roomId);
        if (saved != null)
        {
            _context.SavedRooms.Remove(saved);
            _context.SaveChanges();
            TempData["Message"] = "Đã bỏ lưu phòng.";
        }

        return RedirectToAction("Detail", new { id = roomId });
    }

    public IActionResult Saved()
    {
        var email = HttpContext.Session.GetString(CURRENT_EMAIL);
        if (string.IsNullOrEmpty(email))
            return RedirectToAction("Login", "Account");

        var rooms = _context.SavedRooms
            .Include(x => x.RoomPost)
            .Where(x => x.UserEmail == email && x.RoomPost != null)
            .Select(x => x.RoomPost!)
            .ToList();

        return View(rooms);
    }

    private string BuildJoinCode(int roomId, string ownerId)
    {
        var ownerKey = CreateOwnerKey(ownerId);
        var randomPart = GenerateRandomAlphaNumeric(10);
        return $"RM{roomId}-{ownerKey}-{randomPart}";
    }

    private string CreateOwnerKey(string ownerId)
    {
        var clean = Regex.Replace(ownerId ?? "", @"[^a-zA-Z0-9]", "");
        clean = clean.ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(clean))
            return "USER";

        return clean.Length <= 6 ? clean : clean.Substring(0, 6);
    }

    private string GenerateRandomAlphaNumeric(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();
        var sb = new StringBuilder();

        for (int i = 0; i < length; i++)
        {
            sb.Append(chars[random.Next(chars.Length)]);
        }

        return sb.ToString();
    }

    private string Slugify(string? text)
    {
        text = (text ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(text)) return "empty";

        text = RemoveVietnameseSigns(text);
        text = text.Replace("@", "_at_");
        text = Regex.Replace(text, @"\s+", "_");
        text = Regex.Replace(text, @"[^a-z0-9_.-]", "");
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
        var fileName = $"{safePrefix}_{Guid.NewGuid():N}{ext}";

        var dir = Path.Combine(
            Directory.GetCurrentDirectory(),
            "wwwroot",
            folder.Replace("/", Path.DirectorySeparatorChar.ToString())
        );

        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, fileName);

        using (var stream = new FileStream(path, FileMode.Create))
        {
            file.CopyTo(stream);
        }

        return "/" + folder + "/" + fileName;
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