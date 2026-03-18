// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebDoAn.Data;

namespace WebDoAn.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        private const string RESET_EMAIL_KEY = "RESET_EMAIL";
        private const string RESET_CODE_KEY = "RESET_CODE";
        private const string RESET_EXPIRE_KEY = "RESET_EXPIRE";
        private const string RESET_VERIFIED_KEY = "RESET_VERIFIED";

        public ForgotPasswordModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public string EmailChecked { get; set; }
        public bool EmailExists { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập email")]
            [EmailAddress(ErrorMessage = "Email không hợp lệ")]
            public string Email { get; set; }
        }

        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            if (ModelState.IsValid)
            {
                var email = Input.Email.Trim().ToLowerInvariant();
                var user = _context.UserAccounts.FirstOrDefault(x => x.Email == email);

                EmailChecked = email;
                EmailExists = user != null;

                if (!EmailExists)
                {
                    StatusMessage = "Email này chưa được đăng ký.";
                }

                return Page();
            }

            return Page();
        }

        public IActionResult OnPostSendCode(string email)
        {
            email = (email ?? "").Trim().ToLowerInvariant();

            var user = _context.UserAccounts.FirstOrDefault(x => x.Email == email);
            if (user == null)
            {
                StatusMessage = "Email này chưa được đăng ký.";
                return RedirectToPage();
            }

            var code = Generate6DigitCode();

            HttpContext.Session.SetString(RESET_EMAIL_KEY, email);
            HttpContext.Session.SetString(RESET_CODE_KEY, code);
            HttpContext.Session.SetString(RESET_EXPIRE_KEY, DateTime.UtcNow.AddMinutes(5).ToString("O"));
            HttpContext.Session.Remove(RESET_VERIFIED_KEY);

            try
            {
                SendResetMail(email, code);
                StatusMessage = "Đã gửi mã xác nhận đến email của bạn.";
                return RedirectToPage("VerifyResetCode");
            }
            catch (Exception ex)
            {
                StatusMessage = "Gửi email thất bại: " + ex.Message;
                return RedirectToPage();
            }
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
    }
}
