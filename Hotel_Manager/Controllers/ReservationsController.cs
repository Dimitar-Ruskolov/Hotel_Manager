using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Hotel_Manager.Data;
using Hotel_Manager.Models;
using Hotel_Manager.Services;

namespace Hotel_Manager.Controllers
{
    public class ReservationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ReservationTotalPriceService _totalPriceService;

        public ReservationsController(ApplicationDbContext context, ReservationTotalPriceService reservationTotalPriceService)
        {
            _context = context;
            _totalPriceService = reservationTotalPriceService;
        }

        // GET: Reservations
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Reservations.Include(r => r.User);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Reservations/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var reservation = await _context.Reservations
                .Include(r => r.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (reservation == null) return NotFound();

            return View(reservation);
        }

        // GET: Reservations/Create
        public IActionResult Create()
        {
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Email");
            ViewBag.Rooms = new SelectList(_context.Rooms, "Id", "Id");
            ViewBag.Services = new SelectList(_context.HotelServices, "Id", "Name");
            return View();
        }

        // POST: Reservations/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,CheckInDate,CheckOutDate,Status,CreatedAt,UserId")] Reservation reservation, List<int> roomIds,
    List<int> serviceIds)
        {
            if (ModelState.IsValid)
            {
                reservation.ReservationRooms = roomIds
                    .Select(roomId => new ReservationRoom { RoomId = roomId })
                    .ToList();

                reservation.ReservationServices = serviceIds
                    .Select(serviceId => new ReservationService { ServiceId = serviceId })
                    .ToList();

                var rooms = await _context.Rooms
                    .Where(r => roomIds.Contains(r.Id))
                    .Include(r => r.RoomType)
                    .ToListAsync();

                var services = await _context.HotelServices
                    .Where(s => serviceIds.Contains(s.Id))
                    .ToListAsync();

                foreach (var rr in reservation.ReservationRooms)
                    rr.Room = rooms.First(r => r.Id == rr.RoomId);

                foreach (var rs in reservation.ReservationServices)
                    rs.HotelService = services.First(s => s.Id == rs.ServiceId);

                reservation.TotalPrice = _totalPriceService.CalculateTotalPrice(reservation);

                _context.Add(reservation);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Email", reservation.UserId);
            return View(reservation);
        }

        // GET: Reservations/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null) return NotFound();

            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Email", reservation.UserId);
            return View(reservation);
        }

        // POST: Reservations/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,CheckInDate,CheckOutDate,TotalPrice,Status,CreatedAt,UserId")] Reservation reservation)
        {
            if (id != reservation.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(reservation);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ReservationExists(reservation.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Email", reservation.UserId);
            return View(reservation);
        }

        // GET: Reservations/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var reservation = await _context.Reservations
                .Include(r => r.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (reservation == null) return NotFound();

            return View(reservation);
        }

        // POST: Reservations/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation != null)
                _context.Reservations.Remove(reservation);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ReservationExists(int id)
        {
            return _context.Reservations.Any(e => e.Id == id);
        }
    }
}