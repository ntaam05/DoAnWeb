using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WebDoAn.Areas.Identity.Pages.Account
{
    public class VerifyResetCodeModel : PageModel
    {
        private const string RESET_EMAIL_KEY = "RESET_EMAIL";
        private const string RESET_CODE_KEY = "RESET_CODE";
        private const string RESET_EXPIRE_KEY = "RESET_EXPIRE";
        private const string RESET_VERIFIED_KEY = "RESET_VERIFIED";

        [BindProperty]
        public InputModel Input { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public string Email { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập mã xác nhận")]
            [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã xác nhận phải gồm đúng 6 chữ số")]
            public string Code { get; set; }
        }

        public IActionResult OnGet()
        {
            var email = HttpContext.Session.GetString(RESET_EMAIL_KEY);
            var code = HttpContext.Session.GetString(RESET_CODE_KEY);
            var verified = HttpContext.Session.GetString(RESET_VERIFIED_KEY);

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(code) || verified == "true")
            {
                StatusMessage = "Mã đã được dùng hoặc không còn hợp lệ. Vui lòng lấy mã mới.";
                return RedirectToPage("ForgotPassword");
            }

            Email = email;
            return Page();
        }

        public IActionResult OnPost()
        {
            var savedEmail = HttpContext.Session.GetString(RESET_EMAIL_KEY);
            var savedCode = HttpContext.Session.GetString(RESET_CODE_KEY);
            var expireText = HttpContext.Session.GetString(RESET_EXPIRE_KEY);
            var verified = HttpContext.Session.GetString(RESET_VERIFIED_KEY);

            Email = savedEmail;

            if (verified == "true")
            {
                StatusMessage = "Mã này đã được sử dụng. Vui lòng lấy mã mới.";
                return RedirectToPage("ForgotPassword");
            }

            if (string.IsNullOrEmpty(savedEmail) || string.IsNullOrEmpty(savedCode) || string.IsNullOrEmpty(expireText))
            {
                StatusMessage = "Phiên đặt lại mật khẩu đã hết. Vui lòng lấy mã lại.";
                return RedirectToPage("ForgotPassword");
            }

            if (!DateTime.TryParse(expireText, out var expireAt))
            {
                ClearResetSession();
                StatusMessage = "Mã xác nhận không hợp lệ.";
                return RedirectToPage("ForgotPassword");
            }

            if (DateTime.UtcNow > expireAt)
            {
                ClearResetSession();
                StatusMessage = "Mã xác nhận đã hết hạn.";
                return RedirectToPage("ForgotPassword");
            }

            if (Input.Code.Length != 6 || !Input.Code.All(char.IsDigit))
            {
                ModelState.AddModelError(string.Empty, "Mã xác nhận phải gồm đúng 6 chữ số.");
                return Page();
            }

            if (savedCode != Input.Code)
            {
                ModelState.AddModelError(string.Empty, "Mã xác nhận không đúng.");
                return Page();
            }

            HttpContext.Session.SetString(RESET_VERIFIED_KEY, "true");
            HttpContext.Session.Remove(RESET_CODE_KEY);
            HttpContext.Session.Remove(RESET_EXPIRE_KEY);

            return RedirectToPage("ResetPassword");
        }

        private void ClearResetSession()
        {
            HttpContext.Session.Remove(RESET_EMAIL_KEY);
            HttpContext.Session.Remove(RESET_CODE_KEY);
            HttpContext.Session.Remove(RESET_EXPIRE_KEY);
            HttpContext.Session.Remove(RESET_VERIFIED_KEY);
        }
    }
}