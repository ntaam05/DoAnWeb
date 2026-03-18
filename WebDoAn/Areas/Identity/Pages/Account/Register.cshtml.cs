// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebDoAn.Data;
using WebDoAn.Models;

namespace WebDoAn.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public RegisterModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập email")]
            [EmailAddress(ErrorMessage = "Email không hợp lệ")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
            [StringLength(100, ErrorMessage = "Mật khẩu phải dài từ {2} đến {1} ký tự.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Xác nhận mật khẩu")]
            [Compare("Password", ErrorMessage = "Mật khẩu không khớp")]
            public string ConfirmPassword { get; set; }

            [Display(Name = "Bạn là")]
            public string UserType { get; set; } = "Tenant";

            [Display(Name = "Họ tên")]
            public string FullName { get; set; }

            [Display(Name = "Năm sinh")]
            public string BirthYear { get; set; }

            [Display(Name = "Giới tính")]
            public string Gender { get; set; }

            [Display(Name = "Quê quán")]
            public string Hometown { get; set; }

            [Display(Name = "Avatar (tuỳ chọn)")]
            public IFormFile Avatar { get; set; }
        }

        public void OnGet()
        {
        }

        public IActionResult OnPost(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (ModelState.IsValid)
            {
                var email = Input.Email.Trim().ToLowerInvariant();
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

                HttpContext.Session.SetString("CURRENT_USER_EMAIL", email);
                HttpContext.Session.SetString("CURRENT_USER_TYPE", userType);

                return LocalRedirect(returnUrl);
            }

            return Page();
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
