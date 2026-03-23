#nullable disable
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using WebDoAn.Data;
using WebDoAn.Models;
namespace WebDoAn.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        private const string REGISTER_EMAIL_CODE = "REGISTER_EMAIL_CODE";
        private const string REGISTER_EMAIL_CODE_EXPIRE = "REGISTER_EMAIL_CODE_EXPIRE";
        private const string REGISTER_EMAIL_VERIFIED = "REGISTER_EMAIL_VERIFIED";
        private const string REGISTER_PENDING_EMAIL = "REGISTER_PENDING_EMAIL";

        public RegisterModel(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        private static readonly string[] VietnamProvinces = new[]
        {
            "An Giang",
            "Bắc Ninh",
            "Cà Mau",
            "Cao Bằng",
            "Cần Thơ",
            "Đà Nẵng",
            "Đắk Lắk",
            "Điện Biên",
            "Đồng Nai",
            "Đồng Tháp",
            "Gia Lai",
            "Hà Nội",
            "Hà Tĩnh",
            "Hải Phòng",
            "Hưng Yên",
            "Huế",
            "Khánh Hòa",
            "Lai Châu",
            "Lạng Sơn",
            "Lâm Đồng",
            "Lào Cai",
            "Nghệ An",
            "Ninh Bình",
            "Phú Thọ",
            "Quảng Ngãi",
            "Quảng Ninh",
            "Quảng Trị",
            "Sơn La",
            "Tây Ninh",
            "Thái Nguyên",
            "Thanh Hóa",
            "Thành phố Hồ Chí Minh",
            "Tuyên Quang",
            "Vĩnh Long"
        };

        public List<SelectListItem> ProvinceOptions { get; set; } = new();

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel : IValidatableObject
        {
            [Required(ErrorMessage = "Vui lòng nhập email")]
            [EmailAddress(ErrorMessage = "Email không hợp lệ")]
            [StringLength(100, ErrorMessage = "Email tối đa {1} ký tự")]
            public string Email { get; set; }

            [Display(Name = "Mã xác thực email")]
            public string EmailCode { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
            [StringLength(100, ErrorMessage = "Mật khẩu phải dài từ {2} đến {1} ký tự.", MinimumLength = 8)]
            [DataType(DataType.Password)]
            [RegularExpression(
                @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d]).{8,}$",
                ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt."
            )]
            public string Password { get; set; }

            [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu")]
            [DataType(DataType.Password)]
            [Display(Name = "Xác nhận mật khẩu")]
            [Compare("Password", ErrorMessage = "Mật khẩu không khớp")]
            public string ConfirmPassword { get; set; }

            [Display(Name = "Bạn là")]
            public string UserType { get; set; } = "Tenant";

            [Required(ErrorMessage = "Vui lòng nhập họ tên")]
            [StringLength(100, ErrorMessage = "Họ tên tối đa {1} ký tự")]
            public string FullName { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập năm sinh")]
            [Display(Name = "Năm sinh")]
            public string BirthYear { get; set; }

            [Required(ErrorMessage = "Vui lòng chọn giới tính")]
            [Display(Name = "Giới tính")]
            public string Gender { get; set; }

            [Required(ErrorMessage = "Vui lòng chọn tỉnh/thành")]
            [Display(Name = "Quê quán")]
            public string Hometown { get; set; }

            [Display(Name = "Avatar (tuỳ chọn)")]
            public IFormFile Avatar { get; set; }

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                var currentYear = DateTime.Now.Year;

                if (!int.TryParse(BirthYear, out int year))
                {
                    yield return new ValidationResult("Năm sinh phải là số hợp lệ.", new[] { nameof(BirthYear) });
                }
                else
                {
                    if (year < currentYear - 85)
                    {
                        yield return new ValidationResult(
                            $"Tuổi không được lớn hơn 85. Năm sinh phải từ {currentYear - 85} trở đi.",
                            new[] { nameof(BirthYear) });
                    }

                    if (year > currentYear)
                    {
                        yield return new ValidationResult(
                            "Năm sinh không được lớn hơn năm hiện tại.",
                            new[] { nameof(BirthYear) });
                    }
                }

                var allowedGenders = new[] { "Nam", "Nữ", "Khác" };
                if (!string.IsNullOrWhiteSpace(Gender) && !allowedGenders.Contains(Gender))
                {
                    yield return new ValidationResult("Giới tính không hợp lệ.", new[] { nameof(Gender) });
                }

                if (Avatar != null)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                    var ext = Path.GetExtension(Avatar.FileName)?.ToLowerInvariant();

                    if (!allowedExtensions.Contains(ext))
                    {
                        yield return new ValidationResult(
                            "Avatar chỉ chấp nhận file .jpg, .jpeg, .png, .webp",
                            new[] { nameof(Avatar) });
                    }

                    if (Avatar.Length > 2 * 1024 * 1024)
                    {
                        yield return new ValidationResult(
                            "Avatar không được vượt quá 2MB.",
                            new[] { nameof(Avatar) });
                    }
                }
            }
        }

        private void LoadProvinceOptions()
        {
            ProvinceOptions = VietnamProvinces
                .Select(x => new SelectListItem { Value = x, Text = x })
                .ToList();
        }

        public void OnGet()
        {
            LoadProvinceOptions();
        }

        public JsonResult OnPostSendEmailCode(string email)
        {
            email = (email ?? "").Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(email))
                return new JsonResult(new { success = false, message = "Vui lòng nhập email." });

            if (!new EmailAddressAttribute().IsValid(email))
                return new JsonResult(new { success = false, message = "Email không hợp lệ." });

            var exists = _context.UserAccounts.Any(x => x.Email == email);
            if (exists)
                return new JsonResult(new { success = false, message = "Email đã tồn tại." });

            var code = new Random().Next(100000, 999999).ToString();
            var expire = DateTime.Now.AddMinutes(5);

            HttpContext.Session.SetString(REGISTER_EMAIL_CODE, code);
            HttpContext.Session.SetString(REGISTER_EMAIL_CODE_EXPIRE, expire.ToString("O"));
            HttpContext.Session.SetString(REGISTER_EMAIL_VERIFIED, "");
            HttpContext.Session.SetString(REGISTER_PENDING_EMAIL, email);

            try
            {
                SendConfirmMail(email, code);
                return new JsonResult(new { success = true, message = "Đã gửi mã xác thực đến email." });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = "Gửi email thất bại: " + ex.Message });
            }
        }

        public JsonResult OnPostVerifyEmailCode(string email, string code)
        {
            email = (email ?? "").Trim().ToLowerInvariant();
            code = (code ?? "").Trim();

            var savedCode = HttpContext.Session.GetString(REGISTER_EMAIL_CODE);
            var savedEmail = HttpContext.Session.GetString(REGISTER_PENDING_EMAIL);
            var expireText = HttpContext.Session.GetString(REGISTER_EMAIL_CODE_EXPIRE);

            if (string.IsNullOrWhiteSpace(savedCode) || string.IsNullOrWhiteSpace(savedEmail) || string.IsNullOrWhiteSpace(expireText))
                return new JsonResult(new { success = false, message = "Mã xác thực không tồn tại hoặc đã hết hạn." });

            if (!DateTime.TryParse(expireText, out var expireAt) || DateTime.Now > expireAt)
                return new JsonResult(new { success = false, message = "Mã xác thực đã hết hạn." });

            if (savedEmail != email)
                return new JsonResult(new { success = false, message = "Email xác thực không khớp." });

            if (savedCode != code)
                return new JsonResult(new { success = false, message = "Mã xác thực không đúng." });

            HttpContext.Session.SetString(REGISTER_EMAIL_VERIFIED, email);
            return new JsonResult(new { success = true, message = "Xác thực email thành công." });
        }

        public IActionResult OnPost(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            LoadProvinceOptions();

            var email = (Input.Email ?? "").Trim().ToLowerInvariant();
            if (Input.Password != null) Input.Password = Input.Password.Trim();
            if (Input.ConfirmPassword != null) Input.ConfirmPassword = Input.ConfirmPassword.Trim();

            if (!VietnamProvinces.Contains((Input.Hometown ?? "").Trim()))
            {
                ModelState.AddModelError("Input.Hometown", "Tỉnh/thành không hợp lệ.");
            }

            var verifiedEmail = HttpContext.Session.GetString(REGISTER_EMAIL_VERIFIED);
            if (verifiedEmail != email)
            {
                ModelState.AddModelError("Input.Email", "Vui lòng xác thực email trước khi đăng ký.");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var userType = (Input.UserType == "Landlord" || Input.UserType == "Tenant") ? Input.UserType : "Tenant";

            var exists = _context.UserAccounts.Any(x => x.Email == email);
            if (exists)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản đã tồn tại. Vui lòng đăng nhập.");
                return Page();
            }

            var user = new UserAccount
            {
                Email = email,
                Password = Input.Password,
                UserType = userType,
                FullName = (Input.FullName ?? "").Trim(),
                BirthYear = (Input.BirthYear ?? "").Trim(),
                Gender = (Input.Gender ?? "").Trim(),
                Hometown = (Input.Hometown ?? "").Trim(),
                AvatarUrl = SaveUpload(Input.Avatar, "avatars") ?? ""
            };

            _context.UserAccounts.Add(user);
            _context.SaveChanges();

            HttpContext.Session.Remove(REGISTER_EMAIL_CODE);
            HttpContext.Session.Remove(REGISTER_EMAIL_CODE_EXPIRE);
            HttpContext.Session.Remove(REGISTER_EMAIL_VERIFIED);
            HttpContext.Session.Remove(REGISTER_PENDING_EMAIL);

            HttpContext.Session.SetString("CURRENT_USER_EMAIL", email);
            HttpContext.Session.SetString("CURRENT_USER_TYPE", userType);

            // NẾU LÀ NGƯỜI THUÊ -> CHUYỂN SANG TRANG CHỌN TAG AI
            if (Input.UserType == "Tenant")
            {
                return Redirect("/Profile/Onboarding");
            }
            // NẾU LÀ CHỦ TRỌ -> VỀ THẲNG TRANG CHỦ HOẶC QUẢN LÝ PHÒNG
            else
            {
                return LocalRedirect(returnUrl ?? "~/");
            }
        }

        private void SendConfirmMail(string toEmail, string code)
        {
            var fromEmail = "turtle2773@gmail.com";
            var appPassword = "lqcpynxdxsrclynq";

            var message = new MailMessage();
            message.From = new MailAddress(fromEmail, "WebDoAn");
            message.To.Add(toEmail);
            message.Subject = "Mã xác thực email đăng ký";
            message.Body = $"Mã xác thực email của bạn là: {code}\nMã có hiệu lực trong 5 phút.";

            using var smtp = new SmtpClient("smtp.gmail.com", 587);
            smtp.Credentials = new NetworkCredential(fromEmail, appPassword);
            smtp.EnableSsl = true;
            smtp.Send(message);
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
    }
}