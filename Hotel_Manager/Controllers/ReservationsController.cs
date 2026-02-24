using Hotel_Manager.Data;
using Hotel_Manager.Models;
using Hotel_Manager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hotel_Manager.Controllers
{
    [Authorize]
    public class ReservationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ReservationTotalPriceService _priceService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoomAvailabilityService _roomAvailabilityService;
        private readonly ReservationLogic _reservationLogic;

        public ReservationsController(
            ApplicationDbContext context,
            ReservationTotalPriceService priceService,
            UserManager<ApplicationUser> userManager,
            RoomAvailabilityService roomAvailabilityService,
            ReservationLogic reservationLogic)
        {
            _context = context;
            _priceService = priceService;
            _userManager = userManager;
            _roomAvailabilityService = roomAvailabilityService;
            _reservationLogic = reservationLogic;
        }        

        public async Task<IActionResult> Index()
        {
            await _roomAvailabilityService.AutoCompleteExpiredReservationsAsync();

            IQueryable<Reservation> query = _context.Reservations
                .Include(r => r.User)
                .Include(r => r.ReservationRooms).ThenInclude(rr => rr.Room).ThenInclude(r => r.RoomType)
                .Include(r => r.ReservationServices).ThenInclude(rs => rs.HotelService)
                .OrderByDescending(r => r.CreatedAt);

            // Guests should see ONLY their own reservations
            if (User.IsInRole("Guest"))
            {
                var currentUserId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(currentUserId))
                {
                    return RedirectToAction("Index", "Home");
                }

                query = query.Where(r => r.UserId == currentUserId);
            }
            // Admins & Receptionists see everything 

            var reservations = await query.ToListAsync();
         
            ViewBag.IsGuest = User.IsInRole("Guest");
            ViewBag.ShowCreateButton = User.IsInRole("Admin") || User.IsInRole("Receptionist");

            return View(reservations);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var reservation = await _context.Reservations
                .Include(r => r.User)
                .Include(r => r.ReservationRooms).ThenInclude(rr => rr.Room).ThenInclude(r => r.RoomType)
                .Include(r => r.ReservationServices).ThenInclude(rs => rs.HotelService)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (reservation == null) return NotFound();

            if (User.IsInRole("Guest") && reservation.UserId != _userManager.GetUserId(User))
                return Forbid();

            return View(reservation);
        }

        [Authorize(Roles = "Admin,Receptionist")]
        public IActionResult Create()
        {
            var availableRoomTypes = _context.RoomTypes
                .Where(rt => _context.Rooms.Any(r => r.RoomTypeId == rt.Id && r.IsAvailable))
                .Select(rt => new { rt.Id, rt.Name })
                .ToList();

            ViewData["RoomTypes"] = new SelectList(availableRoomTypes, "Id", "Name");
            ViewData["Services"] = new SelectList(_context.HotelServices, "Id", "Name");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Receptionist")]
        public async Task<IActionResult> Create(
            Reservation reservation,
            string guestEmail,
            string firstName,
            string lastName,
            int? age,
            int roomTypeId,
            List<int>? serviceIds,
            string? licensePlate)
        {
            var result = await _reservationLogic.CreateAsync(
                reservation, guestEmail, firstName, lastName, age,
                roomTypeId, serviceIds, licensePlate);

            if (!result.Success)
            {
                foreach (var err in result.Errors)
                    ModelState.AddModelError("", err);

                return ReloadCreateView(reservation, roomTypeId);
            }

            if (!string.IsNullOrEmpty(result.NewGuestEmail) && !string.IsNullOrEmpty(result.NewGuestPassword))
            {
                TempData["NewGuestEmail"] = result.NewGuestEmail;
                TempData["NewGuestPassword"] = result.NewGuestPassword;
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin,Receptionist")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var reservation = await _context.Reservations
                .Include(r => r.User)
                .Include(r => r.ReservationRooms).ThenInclude(rr => rr.Room).ThenInclude(r => r.RoomType)
                .Include(r => r.ReservationServices).ThenInclude(rs => rs.HotelService)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null) return NotFound();

            var availableRoomTypes = await _context.RoomTypes
                .Where(rt => _context.Rooms.Any(r => r.RoomTypeId == rt.Id && r.IsAvailable))
                .Select(rt => new { rt.Id, rt.Name })
                .ToListAsync();

            var currentRoomTypeId = reservation.ReservationRooms.FirstOrDefault()?.Room?.RoomTypeId;
            if (currentRoomTypeId.HasValue && !availableRoomTypes.Any(t => t.Id == currentRoomTypeId.Value))
            {
                var current = await _context.RoomTypes
                    .Where(rt => rt.Id == currentRoomTypeId.Value)
                    .Select(rt => new { rt.Id, rt.Name })
                    .FirstOrDefaultAsync();

                if (current != null) availableRoomTypes.Add(current);
            }

            ViewData["RoomTypes"] = new SelectList(availableRoomTypes, "Id", "Name", currentRoomTypeId);

            ViewData["Services"] = new MultiSelectList(
                _context.HotelServices,
                "Id",
                "Name",
                reservation.ReservationServices.Select(rs => rs.ServiceId)
            );

            return View(reservation);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Receptionist")]
        public async Task<IActionResult> Edit(int id,
            [Bind("Id,CheckInDate,CheckOutDate,UserId")] Reservation reservation,
            int roomTypeId,
            List<int>? serviceIds)
        {
            if (id != reservation.Id) return NotFound();

            var updateResult = await _reservationLogic.UpdateReservationAsync(id,reservation.CheckInDate,reservation.CheckOutDate,roomTypeId,serviceIds);

            if (!updateResult.Success)
            {
                foreach (var err in updateResult.Errors)
                    ModelState.AddModelError("", err);

                var availableRoomTypes = await _context.RoomTypes
                    .Where(rt => _context.Rooms.Any(r => r.RoomTypeId == rt.Id && r.IsAvailable))
                    .Select(rt => new { rt.Id, rt.Name })
                    .ToListAsync();

                var current = (await _context.Reservations
                        .Include(r => r.ReservationRooms).ThenInclude(rr => rr.Room)
                        .FirstOrDefaultAsync(r => r.Id == id))
                    ?.ReservationRooms.FirstOrDefault()?.Room?.RoomTypeId;

                if (current.HasValue && !availableRoomTypes.Any(t => t.Id == current.Value))
                {
                    var ct = await _context.RoomTypes
                        .Where(rt => rt.Id == current.Value)
                        .Select(rt => new { rt.Id, rt.Name })
                        .FirstOrDefaultAsync();
                    if (ct != null) availableRoomTypes.Add(ct);
                }

                ViewData["RoomTypes"] = new SelectList(availableRoomTypes, "Id", "Name", roomTypeId);

                ViewData["Services"] = new MultiSelectList(
                    _context.HotelServices,
                    "Id",
                    "Name",
                    serviceIds
                );

                return View(reservation);
            }

            return RedirectToAction(nameof(Index));
        }

        private IActionResult ReloadCreateView(Reservation reservation, int roomTypeId)
        {
            var availableRoomTypes = _context.RoomTypes
                .Where(rt => _context.Rooms.Any(r => r.RoomTypeId == rt.Id && r.IsAvailable))
                .Select(rt => new { rt.Id, rt.Name })
                .ToList();

            ViewData["RoomTypes"] = new SelectList(availableRoomTypes, "Id", "Name", roomTypeId);
            ViewData["Services"] = new SelectList(_context.HotelServices, "Id", "Name");

            return View(reservation);
        }

        [Authorize(Roles = "Admin,Receptionist")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var reservation = await _context.Reservations
                .Include(r => r.ReservationRooms).ThenInclude(rr => rr.Room)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (reservation == null) return NotFound();

            await _roomAvailabilityService
                .SetAvailabilityByReservationIdAsync(reservation.Id, true);

            _context.Reservations.Remove(reservation);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}