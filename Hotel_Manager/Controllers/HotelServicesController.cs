using Hotel_Manager.Data;
using Hotel_Manager.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hotel_Manager.Controllers
{
    [Authorize(Roles = "Admin")]
    public class HotelServicesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HotelServicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _context.HotelServices.ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var hotelService = await _context.HotelServices.FirstOrDefaultAsync(m => m.Id == id);
            if (hotelService == null) return NotFound();

            return View(hotelService);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HotelService hotelService)
        {
            if (ModelState.IsValid)
            {
                _context.Add(hotelService);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(hotelService);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var hotelService = await _context.HotelServices.FindAsync(id);
            if (hotelService == null) return NotFound();

            return View(hotelService);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, HotelService hotelService)
        {
            if (id != hotelService.Id) return NotFound();

            if (ModelState.IsValid)
            {
                _context.Update(hotelService);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(hotelService);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var hotelService = await _context.HotelServices.FirstOrDefaultAsync(m => m.Id == id);
            if (hotelService == null) return NotFound();

            return View(hotelService);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var hotelService = await _context.HotelServices.FindAsync(id);
            if (hotelService != null)
            {
                _context.HotelServices.Remove(hotelService);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}