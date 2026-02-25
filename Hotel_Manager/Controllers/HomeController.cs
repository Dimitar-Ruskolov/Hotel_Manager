using Hotel_Manager.Data;
using Hotel_Manager.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;

namespace Hotel_Manager.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            if (User.Identity?.IsAuthenticated == true &&
               (User.IsInRole("Receptionist") || User.IsInRole("Admin")))
            {
                ViewBag.UpcomingReservations = await _context.Reservations
                    .CountAsync(r => r.Status == "Upcoming");

                ViewBag.InProgressReservations = await _context.Reservations
                    .CountAsync(r => r.Status == "In progress");

                ViewBag.FreeRooms = await _context.Rooms
                    .CountAsync(r => r.IsAvailable);
            }

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        [AllowAnonymous]
        public async Task<IActionResult> Pricing()
        {
            ViewData["Title"] = "Цени";
            ViewData["HideNavbar"] = true;

            ViewBag.RoomTypes = await _context.RoomTypes
                .OrderBy(rt => rt.PricePerNight)
                .ToListAsync();

            ViewBag.Services = await _context.HotelServices
                .OrderBy(s => s.Price)
                .ToListAsync();

            return View();
        }
    }
}
