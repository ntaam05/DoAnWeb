using Microsoft.AspNetCore.Mvc;
using WebDoAn.Data;

namespace WebDoAn.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        var rooms = _context.RoomPosts
            .Where(p => p.PostType == "Room")
            .ToList();

        return View(rooms);
    }

    public IActionResult Privacy() => View();
}