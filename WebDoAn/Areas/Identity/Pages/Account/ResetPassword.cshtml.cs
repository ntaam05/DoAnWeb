// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebDoAn.Data;

namespace WebDoAn.Areas.Identity.Pages.Account
{
    public class ResetPasswordModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        private const string RESET_EMAIL_KEY = "RESET_EMAIL";
        private const string RESET_VERIFIED_KEY = "RESET_VERIFIED";

        public ResetPasswordModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public string Email { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
            [StringLength(100, ErrorMessage = "Mật khẩu phải dài từ {2} đến {1} ký tự.", MinimumLength = 8)]
            [DataType(DataType.Password)]
            [RegularExpression(
                @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d]).{8,}$",
                ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt."
            )]
            public string NewPassword { get; set; }

            [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu")]
            [DataType(DataType.Password)]
            [Display(Name = "Xác nhận mật khẩu")]
            [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp")]
            public string ConfirmPassword { get; set; }
        }

        public IActionResult OnGet()
        {
            var email = HttpContext.Session.GetString(RESET_EMAIL_KEY);
            var verified = HttpContext.Session.GetString(RESET_VERIFIED_KEY);

            if (string.IsNullOrEmpty(email) || verified != "true")
            {
                StatusMessage = "Vui lòng lấy mã và xác thực lại.";
                return RedirectToPage("ForgotPassword");
            }

            Email = email;
            return Page();
        }

        public IActionResult OnPost()
        {
            var email = HttpContext.Session.GetString(RESET_EMAIL_KEY);
            var verified = HttpContext.Session.GetString(RESET_VERIFIED_KEY);

            Email = email;

            if (string.IsNullOrEmpty(email) || verified != "true")
            {
                StatusMessage = "Phiên đặt lại mật khẩu không hợp lệ.";
                return RedirectToPage("ForgotPassword");
            }

            if (ModelState.IsValid)
            {
                var user = _context.UserAccounts.FirstOrDefault(x => x.Email == email);
                if (user == null)
                {
                    ClearResetSession();
                    StatusMessage = "Tài khoản không tồn tại.";
                    return RedirectToPage("ForgotPassword");
                }

                user.Password = Input.NewPassword;
                _context.SaveChanges();

                ClearResetSession();

                StatusMessage = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại.";
                return RedirectToPage("Login");
            }

            return Page();
        }

        private void ClearResetSession()
        {
            HttpContext.Session.Remove(RESET_EMAIL_KEY);
            HttpContext.Session.Remove(RESET_VERIFIED_KEY);
            HttpContext.Session.Remove("RESET_CODE");
            HttpContext.Session.Remove("RESET_EXPIRE");
        }
    }
}
