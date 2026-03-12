using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Mail;
using WebDoAn.Data;
using WebDoAn.Models;

namespace WebDoAn.Controllers;

public class AccountController : Controller
{
    private readonly ApplicationDbContext _context;

    private const string CURRENT_EMAIL = "CURRENT_USER_EMAIL";
    private const string CURRENT_TYPE = "CURRENT_USER_TYPE";

    private const string RESET_EMAIL_KEY = "RESET_EMAIL";
    private const string RESET_CODE_KEY = "RESET_CODE";
    private const string RESET_EXPIRE_KEY = "RESET_EXPIRE";
    private const string RESET_VERIFIED_KEY = "RESET_VERIFIED";

    public AccountController(ApplicationDbContext context)
    {
        _context = context;
    }

    public IActionResult Login() => View();
    public IActionResult Register() => View();

    [HttpPost]
    public IActionResult Register(
        string email,
        string password,
        string userType,
        string? fullName,
        string? birthYear,
        string? gender,
        string? hometown,
        IFormFile? avatar)
    {
        email = (email ?? "").Trim().ToLowerInvariant();
        password = (password ?? "").Trim();
        userType = (userType == "Landlord" || userType == "Tenant") ? userType : "Tenant";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            TempData["AuthError"] = "Vui lòng nhập Email và Password.";
            return View();
        }

        var exists = _context.UserAccounts.Any(x => x.Email == email);
        if (exists)
        {
            TempData["AuthError"] = "Tài khoản đã tồn tại. Vui lòng đăng nhập.";
            return View();
        }

        var user = new UserAccount
        {
            Email = email,
            Password = password,
            UserType = userType,
            FullName = (fullName ?? "").Trim(),
            BirthYear = (birthYear ?? "").Trim(),
            Gender = (gender ?? "").Trim(),
            Hometown = (hometown ?? "").Trim(),
            AvatarUrl = SaveUpload(avatar, "avatars") ?? ""
        };

        _context.UserAccounts.Add(user);
        _context.SaveChanges();

        HttpContext.Session.SetString(CURRENT_EMAIL, email);
        HttpContext.Session.SetString(CURRENT_TYPE, userType);

        TempData["Message"] = $"Đăng ký thành công ({(userType == "Landlord" ? "Người cho thuê" : "Người thuê")}).";
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public IActionResult Login(string email, string password)
    {
        email = (email ?? "").Trim().ToLowerInvariant();

        var user = _context.UserAccounts.FirstOrDefault(x => x.Email == email);
        if (user == null)
        {
            TempData["AuthError"] = "Tài khoản chưa tồn tại. Vui lòng đăng ký.";
            return View();
        }

        if (user.Password != password)
        {
            TempData["AuthError"] = "Sai mật khẩu.";
            return View();
        }

        HttpContext.Session.SetString(CURRENT_EMAIL, user.Email);
        HttpContext.Session.SetString(CURRENT_TYPE, user.UserType);

        TempData["Message"] = "Đăng nhập thành công.";
        return RedirectToAction("Index", "Home");
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Remove(CURRENT_EMAIL);
        HttpContext.Session.Remove(CURRENT_TYPE);
        ClearResetSession();

        TempData["Message"] = "Đã đăng xuất.";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult ForgotPassword(string email)
    {
        email = (email ?? "").Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["AuthError"] = "Vui lòng nhập email.";
            return View();
        }

        var user = _context.UserAccounts.FirstOrDefault(x => x.Email == email);
        if (user == null)
        {
            TempData["AuthError"] = "Email này chưa được đăng ký.";
            return View();
        }

        ViewBag.EmailChecked = email;
        ViewBag.EmailExists = true;
        return View();
    }

    [HttpPost]
    public IActionResult SendResetCode(string email)
    {
        email = (email ?? "").Trim().ToLowerInvariant();

        var user = _context.UserAccounts.FirstOrDefault(x => x.Email == email);
        if (user == null)
        {
            TempData["AuthError"] = "Email này chưa được đăng ký.";
            return RedirectToAction("ForgotPassword");
        }

        var code = Generate6DigitCode();

        HttpContext.Session.SetString(RESET_EMAIL_KEY, email);
        HttpContext.Session.SetString(RESET_CODE_KEY, code);
        HttpContext.Session.SetString(RESET_EXPIRE_KEY, DateTime.UtcNow.AddMinutes(5).ToString("O"));
        HttpContext.Session.Remove(RESET_VERIFIED_KEY);

        try
        {
            SendResetMail(email, code);
            TempData["Message"] = "Đã gửi mã xác nhận đến email của bạn.";
            return RedirectToAction("VerifyResetCode");
        }
        catch (Exception ex)
        {
            TempData["AuthError"] = "Gửi email thất bại: " + ex.Message;
            return RedirectToAction("ForgotPassword");
        }
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult VerifyResetCode()
    {
        var email = HttpContext.Session.GetString(RESET_EMAIL_KEY);
        var code = HttpContext.Session.GetString(RESET_CODE_KEY);
        var verified = HttpContext.Session.GetString(RESET_VERIFIED_KEY);

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(code) || verified == "true")
        {
            TempData["AuthError"] = "Mã đã được dùng hoặc không còn hợp lệ. Vui lòng lấy mã mới.";
            return RedirectToAction("ForgotPassword");
        }

        ViewBag.Email = email;
        return View();
    }

    [HttpPost]
    public IActionResult VerifyResetCode(string code)
    {
        code = (code ?? "").Trim();

        var savedEmail = HttpContext.Session.GetString(RESET_EMAIL_KEY);
        var savedCode = HttpContext.Session.GetString(RESET_CODE_KEY);
        var expireText = HttpContext.Session.GetString(RESET_EXPIRE_KEY);
        var verified = HttpContext.Session.GetString(RESET_VERIFIED_KEY);

        if (verified == "true")
        {
            TempData["AuthError"] = "Mã này đã được sử dụng. Vui lòng lấy mã mới.";
            return RedirectToAction("ForgotPassword");
        }

        if (string.IsNullOrEmpty(savedEmail) || string.IsNullOrEmpty(savedCode) || string.IsNullOrEmpty(expireText))
        {
            TempData["AuthError"] = "Phiên đặt lại mật khẩu đã hết. Vui lòng lấy mã lại.";
            return RedirectToAction("ForgotPassword");
        }

        if (!DateTime.TryParse(expireText, out var expireAt))
        {
            ClearResetSession();
            TempData["AuthError"] = "Mã xác nhận không hợp lệ.";
            return RedirectToAction("ForgotPassword");
        }

        if (DateTime.UtcNow > expireAt)
        {
            ClearResetSession();
            TempData["AuthError"] = "Mã xác nhận đã hết hạn.";
            return RedirectToAction("ForgotPassword");
        }

        if (code.Length != 6 || !code.All(char.IsDigit))
        {
            ViewBag.Email = savedEmail;
            TempData["AuthError"] = "Mã xác nhận phải gồm đúng 6 chữ số.";
            return View();
        }

        if (savedCode != code)
        {
            ViewBag.Email = savedEmail;
            TempData["AuthError"] = "Mã xác nhận không đúng.";
            return View();
        }

        HttpContext.Session.SetString(RESET_VERIFIED_KEY, "true");
        HttpContext.Session.Remove(RESET_CODE_KEY);
        HttpContext.Session.Remove(RESET_EXPIRE_KEY);

        return RedirectToAction("ResetPassword");
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult ResetPassword()
    {
        var email = HttpContext.Session.GetString(RESET_EMAIL_KEY);
        var verified = HttpContext.Session.GetString(RESET_VERIFIED_KEY);

        if (string.IsNullOrEmpty(email) || verified != "true")
        {
            TempData["AuthError"] = "Vui lòng lấy mã và xác thực lại.";
            return RedirectToAction("ForgotPassword");
        }

        ViewBag.Email = email;
        return View();
    }

    [HttpPost]
    public IActionResult ResetPassword(string newPassword, string confirmPassword)
    {
        var email = HttpContext.Session.GetString(RESET_EMAIL_KEY);
        var verified = HttpContext.Session.GetString(RESET_VERIFIED_KEY);

        if (string.IsNullOrEmpty(email) || verified != "true")
        {
            TempData["AuthError"] = "Phiên đặt lại mật khẩu không hợp lệ.";
            return RedirectToAction("ForgotPassword");
        }

        newPassword = (newPassword ?? "").Trim();
        confirmPassword = (confirmPassword ?? "").Trim();

        if (string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
        {
            ViewBag.Email = email;
            TempData["AuthError"] = "Vui lòng nhập đầy đủ mật khẩu mới.";
            return View();
        }

        if (newPassword != confirmPassword)
        {
            ViewBag.Email = email;
            TempData["AuthError"] = "Mật khẩu xác nhận không khớp.";
            return View();
        }

        var user = _context.UserAccounts.FirstOrDefault(x => x.Email == email);
        if (user == null)
        {
            ClearResetSession();
            TempData["AuthError"] = "Tài khoản không tồn tại.";
            return RedirectToAction("ForgotPassword");
        }

        user.Password = newPassword;
        _context.SaveChanges();

        ClearResetSession();

        TempData["Message"] = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại.";
        return RedirectToAction("Login");
    }

    private string? SaveUpload(IFormFile? file, string folder)
    {
        if (file == null || file.Length == 0) return null;

        var ext = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", folder);
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, fileName);
        using var stream = System.IO.File.Create(path);
        file.CopyTo(stream);

        return $"/uploads/{folder}/{fileName}";
    }

    private string Generate6DigitCode()
    {
        var random = new Random();
        var chars = new char[6];

        for (int i = 0; i < 6; i++)
        {
            chars[i] = (char)('0' + random.Next(10));
        }

        return new string(chars);
    }

    private void SendResetMail(string toEmail, string code)
    {
        var fromEmail = "turtle2773@gmail.com";
        var appPassword = "lqcpynxdxsrclynq";

        var message = new MailMessage();
        message.From = new MailAddress(fromEmail, "WebDoAn");
        message.To.Add(toEmail);
        message.Subject = "Mã đặt lại mật khẩu";
        message.Body = $"Mã xác nhận đặt lại mật khẩu của bạn là: {code}\nMã có hiệu lực trong 5 phút.";

        using var smtp = new SmtpClient("smtp.gmail.com", 587);
        smtp.Credentials = new NetworkCredential(fromEmail, appPassword);
        smtp.EnableSsl = true;
        smtp.Send(message);
    }

    private void ClearResetSession()
    {
        HttpContext.Session.Remove(RESET_EMAIL_KEY);
        HttpContext.Session.Remove(RESET_CODE_KEY);
        HttpContext.Session.Remove(RESET_EXPIRE_KEY);
        HttpContext.Session.Remove(RESET_VERIFIED_KEY);
    }
}