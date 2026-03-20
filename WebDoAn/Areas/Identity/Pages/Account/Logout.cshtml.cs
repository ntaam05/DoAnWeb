// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using WebDoAn.Models;

namespace WebDoAn.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<LogoutModel> _logger;
        private const string REMEMBER_ME_EMAIL = "REMEMBER_ME_EMAIL";

        public LogoutModel(SignInManager<ApplicationUser> signInManager, ILogger<LogoutModel> logger)
        {
            _signInManager = signInManager;
            _logger = logger;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            try
            {
                // Sign out Identity cookie(s)
                await _signInManager.SignOutAsync();
                await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
                await HttpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);

                _logger.LogInformation("User logged out.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sign out.");
            }

            // Clear session + remember cookie
            HttpContext.Session.Remove("CURRENT_USER_EMAIL");
            HttpContext.Session.Remove("CURRENT_USER_TYPE");
            HttpContext.Session.Remove("RESET_EMAIL");
            HttpContext.Session.Remove("RESET_CODE");
            HttpContext.Session.Remove("RESET_EXPIRE");
            HttpContext.Session.Remove("RESET_VERIFIED");
            HttpContext.Session.Clear();

            Response.Cookies.Delete(REMEMBER_ME_EMAIL);

            // Redirect safely to provided returnUrl or home
            var target = string.IsNullOrEmpty(returnUrl) ? Url.Content("~/") : returnUrl;
            return LocalRedirect(target);
        }

        public IActionResult OnGet()
        {
            // GET -> redirect home
            return Redirect(Url.Content("~/"));
        }
    }
}
