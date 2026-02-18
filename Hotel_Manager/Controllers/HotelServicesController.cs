using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Hotel_Manager.Data;
using Hotel_Manager.Models;

namespace Hotel_Manager.Controllers
{
    public class HotelServicesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HotelServicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: HotelServices
        public async Task<IActionResult> Index()
        {
            return View(await _context.HotelServices.ToListAsync());
        }

        // GET: HotelServices/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var hotelService = await _context.HotelServices
                .FirstOrDefaultAsync(m => m.Id == id);
            if (hotelService == null)
            {
                return NotFound();
            }

            return View(hotelService);
        }

        // GET: HotelServices/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: HotelServices/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Price")] HotelService hotelService)
        {
            if (ModelState.IsValid)
            {
                _context.Add(hotelService);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(hotelService);
        }

        // GET: HotelServices/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var hotelService = await _context.HotelServices.FindAsync(id);
            if (hotelService == null)
            {
                return NotFound();
            }
            return View(hotelService);
        }

        // POST: HotelServices/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Price")] HotelService hotelService)
        {
            if (id != hotelService.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(hotelService);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!HotelServiceExists(hotelService.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(hotelService);
        }

        // GET: HotelServices/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var hotelService = await _context.HotelServices
                .FirstOrDefaultAsync(m => m.Id == id);
            if (hotelService == null)
            {
                return NotFound();
            }

            return View(hotelService);
        }

        // POST: HotelServices/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var hotelService = await _context.HotelServices.FindAsync(id);
            if (hotelService != null)
            {
                _context.HotelServices.Remove(hotelService);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool HotelServiceExists(int id)
        {
            return _context.HotelServices.Any(e => e.Id == id);
        }
    }
}
