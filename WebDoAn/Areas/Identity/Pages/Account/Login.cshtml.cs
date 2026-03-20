// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WebDoAn.Data;
using WebDoAn.Models;

namespace WebDoAn.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly ApplicationDbContext _context;
        private const string REMEMBER_ME_EMAIL = "REMEMBER_ME_EMAIL";

        public LoginModel(SignInManager<ApplicationUser> signInManager, ILogger<LoginModel> logger, ApplicationDbContext context)
        {
            _signInManager = signInManager;
            _logger = logger;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập email")]
            [EmailAddress(ErrorMessage = "Email không hợp lệ")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Nhớ tôi")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            ReturnUrl = returnUrl;

            // Load saved email from cookie
            var savedEmail = HttpContext.Request.Cookies[REMEMBER_ME_EMAIL];
            if (!string.IsNullOrEmpty(savedEmail))
            {
                Input = new InputModel { Email = savedEmail, RememberMe = true };
            }
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                var email = Input.Email.Trim().ToLowerInvariant();
                var user = _context.UserAccounts.FirstOrDefault(x => x.Email == email);

                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "Tài khoản chưa tồn tại. Vui lòng đăng ký.");
                    return Page();
                }

                if (user.Password != Input.Password)
                {
                    ModelState.AddModelError(string.Empty, "Sai mật khẩu.");
                    return Page();
                }

                HttpContext.Session.SetString("CURRENT_USER_EMAIL", user.Email);
                HttpContext.Session.SetString("CURRENT_USER_TYPE", user.UserType);
                HttpContext.Session.SetString("CURRENT_USER_NAME", user.FullName ?? "");
                HttpContext.Session.SetString("CURRENT_USER_AVATAR", user.AvatarUrl ?? "");
                TempData["LoginWelcome"] = string.IsNullOrWhiteSpace(user.FullName) ? user.Email.Split('@')[0] : user.FullName;

                // Save email cookie if Remember Me checked
                if (Input.RememberMe)
                {
                    HttpContext.Response.Cookies.Append(REMEMBER_ME_EMAIL, email, new CookieOptions
                    {
                        HttpOnly = true,
                        Expires = DateTimeOffset.UtcNow.AddDays(30)
                    });
                }
                else
                {
                    HttpContext.Response.Cookies.Delete(REMEMBER_ME_EMAIL);
                }

                return LocalRedirect(returnUrl);
            }

            return Page();
        }
    }
}
