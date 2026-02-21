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

            var availableRoomTypes = _context.RoomTypes
               .Where(rt => _context.Rooms
                   .Any(r => r.RoomTypeId == rt.Id && r.IsAvailable))
               .Select(rt => new { rt.Id, rt.Name })
               .ToList();

            ViewData["RoomTypes"] = new SelectList(availableRoomTypes, "Id", "Name");

            ViewData["Services"] = new SelectList(_context.HotelServices, "Id", "Name");
            ViewBag.Services = new SelectList(_context.HotelServices, "Id", "Name");
            return View();
        }

        // POST: Reservations/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,CheckInDate,CheckOutDate,Status,CreatedAt,UserId")] Reservation reservation, int roomTypeId, 
    List<int> serviceIds)
        {
            ModelState.Remove("User");
            if (ModelState.IsValid)
            {
                var room = await _context.Rooms
                    .Include(r => r.RoomType)
                    .FirstOrDefaultAsync(r => r.RoomTypeId == roomTypeId && r.IsAvailable);

                if (room == null)
                {
                    ModelState.AddModelError("", "Няма свободна стая от избрания тип.");
                }
                else
                {
                    room.IsAvailable = false;

                    reservation.ReservationRooms = new List<ReservationRoom> {new ReservationRoom { RoomId = room.Id, Room = room }};
                }

                var services = await _context.HotelServices
                    .Where(s => serviceIds.Contains(s.Id))
                    .ToListAsync();

                reservation.ReservationServices = services
                    .Select(s => new ReservationService { ServiceId = s.Id, HotelService = s })
                    .ToList();

                if (!ModelState.IsValid)
                {
                    ViewData["UserId"] = new SelectList(_context.Users, "Id", "Email", reservation.UserId);

                    var availableRoomTypes = _context.RoomTypes
                        .Where(rt => _context.Rooms.Any(r => r.RoomTypeId == rt.Id && r.IsAvailable))
                        .Select(rt => new { rt.Id, rt.Name })
                        .ToList();

                    ViewData["RoomTypes"] = new SelectList(availableRoomTypes, "Id", "Name", roomTypeId);
                    ViewData["Services"] = new SelectList(_context.HotelServices, "Id", "Name");

                    return View(reservation);
                }

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