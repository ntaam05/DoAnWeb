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
                if (image == null || image.Length == 0) continue;
                var prefix = $"room_{Email}_{room.Title}_{index}";
                var url = SaveUpload(image, prefix, "uploads/rooms");
                if (!string.IsNullOrWhiteSpace(url)) imageUrls.Add(url);
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
        var posts = _context.RoomPosts.Where(x => x.PostType == "Find").ToList();
        return View(posts);
    }

    public IActionResult Detail(int id)
    {
        var post = _context.RoomPosts.Include(x => x.Tenants).FirstOrDefault(x => x.Id == id);
        if (post == null) return NotFound();

        var email = HttpContext.Session.GetString(CURRENT_EMAIL);
        ViewBag.IsSaved = !string.IsNullOrEmpty(email) && _context.SavedRooms.Any(x => x.UserEmail == email && x.RoomPostId == id);

        var reviews = _context.RoomComments
            .Where(x => x.RoomPostId == id && x.Rating > 0)
            .OrderByDescending(x => x.CreatedAt).ToList();

        ViewBag.Comments = reviews;
        ViewBag.AvgRate = Math.Round(reviews.Average(x => (decimal?)x.Rating) ?? 0.0m, 1);
        ViewBag.RatingCount = reviews.Count;
        ViewBag.HasReviewed = !string.IsNullOrEmpty(email) && reviews.Any(x => x.UserEmail == email);

        return View(post);
    }

    public IActionResult Discuss(int id)
    {
        var post = _context.RoomPosts.FirstOrDefault(x => x.Id == id);
        if (post == null) return NotFound();

        var rawComments = _context.RoomComments
            .Where(c => c.RoomPostId == id && c.Rating == 0)
            .Join(_context.UserAccounts,
                comment => comment.UserEmail,
                user => user.Email,
                (comment, user) => new { comment, user })
            .AsEnumerable()
            .Select(x => new
            {
                x.comment.Id,
                x.comment.Content,
                x.comment.CreatedAt,
                x.comment.UserEmail,
                x.comment.ReplyToCommentId,
                Nickname = string.IsNullOrEmpty(x.user.FullName) ? x.user.Email.Split('@')[0] : x.user.FullName,
                Avatar = string.IsNullOrEmpty(x.user.AvatarUrl) ? "https://i.pravatar.cc/150?img=3" : x.user.AvatarUrl
            }).OrderBy(x => x.CreatedAt).ToList();

        var comments = rawComments.Select(x =>
        {
            var replied = x.ReplyToCommentId != null ? rawComments.FirstOrDefault(c => c.Id == x.ReplyToCommentId) : null;
            return new
            {
                x.Id,
                x.Content,
                x.CreatedAt,
                x.UserEmail,
                x.Nickname,
                x.Avatar,
                x.ReplyToCommentId,
                ReplyToNickname = replied != null ? replied.Nickname : null,
                ReplyToContent = replied != null ? replied.Content : null
            };
        }).ToList();

        ViewBag.Comments = comments;
        return View(post);
    }

    public IActionResult Search(string[]? hashtags, string? priceRange, string? keyword)
    {
        var query = _context.RoomPosts.Where(x => x.PostType == "Room").AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.ToLower();
            query = query.Where(x => x.Title.ToLower().Contains(kw) || x.Description.ToLower().Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(priceRange))
        {
            var parts = priceRange.Split('-');
            if (parts.Length == 2 && long.TryParse(parts[0], out long min) && long.TryParse(parts[1], out long max))
            {
                query = query.Where(x => x.Price >= min && x.Price <= max);
            }
        }

        var results = query.OrderByDescending(x => x.Id).ToList();

        if (hashtags != null && hashtags.Length > 0)
        {
            results = results.Where(x => !string.IsNullOrEmpty(x.Hashtags) && hashtags.Any(h => x.Hashtags.Contains(h))).ToList();
        }

        ViewBag.Keyword = keyword;
        ViewBag.PriceRange = priceRange;
        ViewBag.SelectedHashtags = hashtags?.ToList() ?? new List<string>();

        return View(results);
    }

    public IActionResult MyRooms()
    {
        if (!IsLogin || UserType != "Landlord") return RedirectToAction("Index", "Home");
        var rooms = _context.RoomPosts.Include(x => x.Tenants).Where(x => x.PostType == "Room" && x.OwnerId == Email).ToList();
        return View(rooms);
    }

    public IActionResult Manage(int id)
    {
        if (!IsLogin || UserType != "Landlord") return RedirectToAction("Index", "Home");

        var room = _context.RoomPosts.Include(x => x.Tenants).FirstOrDefault(x => x.Id == id && x.PostType == "Room" && x.OwnerId == Email);
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

        return View(new ManageRoomViewModel { Room = room, Tenants = room.Tenants });
    }

    [HttpPost]
    public IActionResult RegenerateJoinCodeAjax(int roomPostId)
    {
        if (!IsLogin || UserType != "Landlord") return Json(new { success = false, message = "Chưa đăng nhập." });

        var room = _context.RoomPosts.FirstOrDefault(x => x.Id == roomPostId && x.PostType == "Room" && x.OwnerId == Email);
        if (room == null) return Json(new { success = false, message = "Không tìm thấy phòng." });

        room.JoinCode = BuildJoinCode(room.Id, room.OwnerId);
        _context.SaveChanges();

        return Json(new { success = true, joinCode = room.JoinCode });
    }

    [HttpPost]
    public IActionResult AddTenant(int roomPostId, string name, string email, string phone, IFormFile? avatar)
    {
        if (!IsLogin || UserType != "Landlord") return RedirectToAction("Index", "Home");

        var room = _context.RoomPosts.Include(x => x.Tenants).FirstOrDefault(x => x.Id == roomPostId && x.PostType == "Room" && x.OwnerId == Email);
        if (room == null) { TempData["AuthError"] = "Không tìm thấy phòng."; return RedirectToAction("MyRooms"); }

        name = (name ?? "").Trim(); email = (email ?? "").Trim().ToLowerInvariant(); phone = (phone ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(phone))
        {
            TempData["AuthError"] = "Vui lòng nhập đầy đủ thông tin người thuê."; return RedirectToAction("Manage", new { id = roomPostId });
        }

        var registeredUser = _context.UserAccounts.FirstOrDefault(x => x.Email == email);
        if (registeredUser == null) { TempData["AuthError"] = "Email này chưa được đăng ký."; return RedirectToAction("Manage", new { id = roomPostId }); }
        if (registeredUser.UserType != "Tenant") { TempData["AuthError"] = "Email này không phải tài khoản Người thuê."; return RedirectToAction("Manage", new { id = roomPostId }); }
        if (room.Tenants.Any(x => x.Email.Equals(email, StringComparison.OrdinalIgnoreCase))) { TempData["AuthError"] = "Người thuê này đã có trong phòng."; return RedirectToAction("Manage", new { id = roomPostId }); }

        var uploadedAvatar = SaveUpload(avatar, $"{registeredUser.Email}_{registeredUser.FullName}", "uploads/avatars");

        room.Tenants.Add(new RoomTenant
        {
            RoomPostId = roomPostId,
            Name = string.IsNullOrWhiteSpace(registeredUser.FullName) ? registeredUser.Email : registeredUser.FullName,
            Email = registeredUser.Email,
            Phone = phone,
            AvatarUrl = !string.IsNullOrEmpty(uploadedAvatar) ? uploadedAvatar : (!string.IsNullOrWhiteSpace(registeredUser.AvatarUrl) ? registeredUser.AvatarUrl : "https://i.pravatar.cc/150?img=3")
        });

        room.IsRented = room.Tenants.Any();
        _context.SaveChanges();
        TempData["Message"] = "Đã thêm người thuê.";
        return RedirectToAction("Manage", new { id = roomPostId });
    }

    [HttpPost]
    public IActionResult DeleteTenant(int roomPostId, int tenantId)
    {
        if (!IsLogin || UserType != "Landlord") return RedirectToAction("Index", "Home");
        var room = _context.RoomPosts.Include(x => x.Tenants).FirstOrDefault(x => x.Id == roomPostId && x.PostType == "Room" && x.OwnerId == Email);
        if (room == null) return RedirectToAction("MyRooms");

        var tenant = room.Tenants.FirstOrDefault(x => x.Id == tenantId);
        if (tenant == null) return RedirectToAction("Manage", new { id = roomPostId });

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
        if (!IsLogin || UserType != "Landlord") return RedirectToAction("Index", "Home");
        var room = _context.RoomPosts.Include(x => x.Tenants).FirstOrDefault(x => x.Id == id && x.PostType == "Room" && x.OwnerId == Email);
        if (room == null) return RedirectToAction("MyRooms");

        foreach (var image in room.ImageUrls) DeleteLocalFile(image);
        _context.RoomPosts.Remove(room);
        _context.SaveChanges();
        TempData["Message"] = "Đã xóa phòng.";
        return RedirectToAction("MyRooms");
    }

    [HttpPost]
    public IActionResult SaveRoom(int roomId)
    {
        var email = HttpContext.Session.GetString(CURRENT_EMAIL);
        if (string.IsNullOrEmpty(email)) return RedirectToAction("Login", "Account");

        var room = _context.RoomPosts.FirstOrDefault(x => x.Id == roomId && x.PostType == "Room");
        if (room == null) return RedirectToAction("Index", "Home");

        if (!_context.SavedRooms.Any(x => x.UserEmail == email && x.RoomPostId == roomId))
        {
            _context.SavedRooms.Add(new SavedRoom { UserEmail = email, RoomPostId = roomId });
            _context.SaveChanges();
            TempData["Message"] = "Đã lưu phòng.";
        }
        return RedirectToAction("Detail", new { id = roomId });
    }

    [HttpPost]
    public IActionResult UnsaveRoom(int roomId)
    {
        var email = HttpContext.Session.GetString(CURRENT_EMAIL);
        if (string.IsNullOrEmpty(email)) return RedirectToAction("Login", "Account");

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
        if (string.IsNullOrEmpty(email)) return RedirectToAction("Login", "Account");

        var rooms = _context.SavedRooms.Include(x => x.RoomPost).Where(x => x.UserEmail == email && x.RoomPost != null).Select(x => x.RoomPost!).ToList();
        return View(rooms);
    }

    private string BuildJoinCode(int roomId, string ownerId)
    {
        var ownerKey = CreateOwnerKey(ownerId);
        return $"RM{roomId}-{ownerKey}-{GenerateRandomAlphaNumeric(10)}";
    }

    private string CreateOwnerKey(string ownerId)
    {
        var clean = Regex.Replace(ownerId ?? "", @"[^a-zA-Z0-9]", "").ToUpperInvariant();
        return string.IsNullOrWhiteSpace(clean) ? "USER" : (clean.Length <= 6 ? clean : clean.Substring(0, 6));
    }

    private string GenerateRandomAlphaNumeric(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();
        var sb = new StringBuilder();
        for (int i = 0; i < length; i++) sb.Append(chars[random.Next(chars.Length)]);
        return sb.ToString();
    }

    private string Slugify(string? text)
    {
        text = (text ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(text)) return "empty";
        text = RemoveVietnameseSigns(text).Replace("@", "_at_");
        text = Regex.Replace(text, @"\s+", "_");
        return Regex.Replace(text, @"[^a-z0-9_.-]", "");
    }

    private string RemoveVietnameseSigns(string text)
    {
        text = text.Replace("đ", "d").Replace("Đ", "D");
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private string? SaveUpload(IFormFile? file, string prefix, string folder)
    {
        if (file == null || file.Length == 0) return null;
        var ext = Path.GetExtension(file.FileName);
        var fileName = $"{Slugify(prefix)}_{Guid.NewGuid():N}{ext}";
        var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", folder.Replace("/", Path.DirectorySeparatorChar.ToString()));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        using (var stream = new FileStream(path, FileMode.Create)) file.CopyTo(stream);
        return "/" + folder + "/" + fileName;
    }

    private void DeleteLocalFile(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || !relativePath.StartsWith("/uploads/")) return;
        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
        if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
    }

    [HttpPost]
    public IActionResult AddComment(int id, string content, int rating, int? replyToCommentId)
    {
        var email = HttpContext.Session.GetString(CURRENT_EMAIL);
        var userType = HttpContext.Session.GetString(CURRENT_TYPE);

        if (string.IsNullOrEmpty(email)) return Redirect("/Identity/Account/Login");
        var referer = Request.Headers["Referer"].ToString();

        if (string.IsNullOrWhiteSpace(content)) { TempData["AuthError"] = "Vui lòng nhập nội dung."; return Redirect(referer); }

        bool isDiscussPage = !string.IsNullOrWhiteSpace(referer) && referer.Contains("/Room/Discuss/", StringComparison.OrdinalIgnoreCase);
        bool isDetailPage = !string.IsNullOrWhiteSpace(referer) && referer.Contains("/Room/Detail/", StringComparison.OrdinalIgnoreCase);

        if (isDiscussPage) rating = 0;
        else if (isDetailPage)
        {
            if (userType != "Tenant") { TempData["AuthError"] = "Chỉ người thuê mới được đánh giá."; return Redirect(referer); }
            if (!_context.RoomTenants.Any(x => x.RoomPostId == id && x.Email == email)) { TempData["AuthError"] = "Chỉ tài khoản đang thuê phòng này mới được đánh giá."; return Redirect(referer); }
            if (rating < 1 || rating > 5) { TempData["AuthError"] = "Vui lòng chọn số sao hợp lệ từ 1 đến 5."; return Redirect(referer); }
            if (_context.RoomComments.Any(x => x.RoomPostId == id && x.UserEmail == email && x.Rating > 0)) { TempData["AuthError"] = "Bạn đã đánh giá phòng này rồi."; return Redirect(referer); }
            replyToCommentId = null;
        }

        _context.RoomComments.Add(new RoomComment { RoomPostId = id, Content = content.Trim(), Rating = rating, UserEmail = email, CreatedAt = DateTime.Now, ReplyToCommentId = replyToCommentId });
        _context.SaveChanges();
        return Redirect(referer);
    }

    // =========================================================
    // THUẬT TOÁN TÍNH ĐỘ PHÙ HỢP (DÙNG BẢNG UserAccounts)
    // =========================================================
    public IActionResult RecommendedRooms()
    {
        var email = HttpContext.Session.GetString("CURRENT_USER_EMAIL") ?? User.Identity?.Name;
        if (string.IsNullOrEmpty(email)) return RedirectToAction("Index", "Home");

        // Đã đổi sang lấy từ bảng UserAccounts
        var appUser = _context.UserAccounts.FirstOrDefault(x => x.Email.ToLower() == email.ToLower());

        string CleanTag(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            input = input.Replace("đ", "d").Replace("Đ", "D");
            var normalized = input.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (var c in normalized)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(System.Text.NormalizationForm.FormC).Replace("#", "").Replace(" ", "").Trim().ToLowerInvariant();
        }

        var userTags = (appUser?.LifestyleTags ?? "")
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(CleanTag).Where(t => !string.IsNullOrEmpty(t)).Distinct().ToList();

        var availableRooms = _context.RoomPosts
            .Where(r => r.IsRented == false && r.PostType == "Room" && !string.IsNullOrEmpty(r.Hashtags)).ToList();

        var recommendations = new Dictionary<RoomPost, double>();

        if (userTags.Any())
        {
            double requiredPercentage = 80;
            if (userTags.Count == 1) requiredPercentage = 100;
            else if (userTags.Count == 2) requiredPercentage = 50;

            foreach (var room in availableRooms)
            {
                var roomTags = room.Hashtags!
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(CleanTag).Where(t => !string.IsNullOrEmpty(t)).Distinct().ToList();

                if (!roomTags.Any()) continue;

                var matchCount = userTags.Intersect(roomTags).Count();
                double matchPercentage = (double)matchCount / userTags.Count * 100;

                if (matchPercentage >= requiredPercentage)
                {
                    recommendations.Add(room, Math.Round(matchPercentage, 0));
                }
            }
        }

        var finalResult = recommendations.OrderByDescending(x => x.Value).Select(x => x.Key).ToList();

        ViewBag.IsAIRecommendation = true;
        ViewBag.Keyword = "";
        return View("Search", finalResult);
    }
}