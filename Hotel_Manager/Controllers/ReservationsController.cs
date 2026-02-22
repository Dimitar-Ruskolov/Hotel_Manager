using Hotel_Manager.Data;
using Hotel_Manager.Models;
using Hotel_Manager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Hotel_Manager.Controllers
{
    [Authorize]
    public class ReservationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ReservationTotalPriceService _priceService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReservationsController(
            ApplicationDbContext context,
            ReservationTotalPriceService priceService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _priceService = priceService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            if (User.IsInRole("Guest"))
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                return View(await _context.Reservations
                    .Where(r => r.UserId == userId)
                    .ToListAsync());
            }

            return View(await _context.Reservations
                .Include(r => r.User)
                .ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var reservation = await _context.Reservations
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return NotFound();

            if (User.IsInRole("Guest") && reservation.UserId != _userManager.GetUserId(User))
                return Forbid();

            return View(reservation);
        }

        [Authorize(Roles = "Admin,Receptionist")]
        public IActionResult Create()
        {
            ViewData["UserId"] = _context.Users.ToList();
            return View();
        }

        [Authorize(Roles = "Admin,Receptionist")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Reservation reservation)
        {
            reservation.TotalPrice = _priceService.CalculateTotalPrice(reservation);
            _context.Add(reservation);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin,Receptionist")]
        public async Task<IActionResult> Delete(int id)
        {
            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null) return NotFound();

            _context.Reservations.Remove(reservation);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}