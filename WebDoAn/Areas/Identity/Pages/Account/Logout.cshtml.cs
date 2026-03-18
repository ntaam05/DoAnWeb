// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using WebDoAn.Models;

namespace WebDoAn.Areas.Identity.Pages.Account
{
    public class LogoutModel : PageModel
    {
        private const string CURRENT_EMAIL = "CURRENT_USER_EMAIL";
        private const string CURRENT_TYPE = "CURRENT_USER_TYPE";
        private const string RESET_EMAIL_KEY = "RESET_EMAIL";
        private const string RESET_CODE_KEY = "RESET_CODE";
        private const string RESET_EXPIRE_KEY = "RESET_EXPIRE";
        private const string RESET_VERIFIED_KEY = "RESET_VERIFIED";

        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<LogoutModel> _logger;

        public LogoutModel(SignInManager<ApplicationUser> signInManager, ILogger<LogoutModel> logger)
        {
            _signInManager = signInManager;
            _logger = logger;
        }

        public IActionResult OnGet()
        {
            HttpContext.Session.Remove(CURRENT_EMAIL);
            HttpContext.Session.Remove(CURRENT_TYPE);
            HttpContext.Session.Remove(RESET_EMAIL_KEY);
            HttpContext.Session.Remove(RESET_CODE_KEY);
            HttpContext.Session.Remove(RESET_EXPIRE_KEY);
            HttpContext.Session.Remove(RESET_VERIFIED_KEY);

            return RedirectToPage("/Index", new { area = "" });
        }

        public async Task<IActionResult> OnPost(string returnUrl = null)
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");
            HttpContext.Session.Remove(CURRENT_EMAIL);
            HttpContext.Session.Remove(CURRENT_TYPE);
            HttpContext.Session.Remove(RESET_EMAIL_KEY);
            HttpContext.Session.Remove(RESET_CODE_KEY);
            HttpContext.Session.Remove(RESET_EXPIRE_KEY);
            HttpContext.Session.Remove(RESET_VERIFIED_KEY);

            if (returnUrl != null)
            {
                return LocalRedirect(returnUrl);
            }
            else
            {
                // This needs to be a redirect so that the browser performs a new
                // request and the identity for the user gets updated.
                return RedirectToPage();
            }
        }
    }
}
