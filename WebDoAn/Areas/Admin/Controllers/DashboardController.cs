using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebDoAn.Areas.Admin.Controllers
{
    // BẮT BUỘC: Báo cho .NET biết Controller này thuộc Area Admin
    [Area("Admin")]

    // BẮT BUỘC: Chỉ những tài khoản được cấp quyền "Admin" mới được vào đây
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}