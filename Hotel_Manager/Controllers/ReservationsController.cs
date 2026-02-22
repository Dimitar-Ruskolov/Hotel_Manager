using Hotel_Manager.Data;
using Hotel_Manager.Models;
using Hotel_Manager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

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
            var reservations = await _context.Reservations
                .Include(r => r.User)
                .Include(r => r.ReservationRooms).ThenInclude(rr => rr.Room).ThenInclude(r => r.RoomType)
                .Include(r => r.ReservationServices).ThenInclude(rs => rs.HotelService)
                .ToListAsync();

            return View(reservations);
        }

        public async Task<IActionResult> Details(int id)
        {
            var reservation = await _context.Reservations
                .Include(r => r.User)
                .Include(r => r.ReservationRooms).ThenInclude(rr => rr.Room).ThenInclude(r => r.RoomType)
                .Include(r => r.ReservationServices).ThenInclude(rs => rs.HotelService)
                .FirstOrDefaultAsync(r => r.Id == id);

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
            int age,
            int roomTypeId,
            List<int> serviceIds)
        {
            if (string.IsNullOrEmpty(guestEmail))
            {
                ModelState.AddModelError("", "Email на госта е задължителен.");
                return ReloadCreateView(reservation, roomTypeId);
            }

            var user = await _userManager.FindByEmailAsync(guestEmail);

            if (user == null)
            {
                string generatedPassword = GenerateRandomPassword(10);

                user = new ApplicationUser
                {
                    UserName = guestEmail,
                    Email = guestEmail,
                    FirstName = firstName ?? "Guest",
                    LastName = lastName ?? "Guest",
                    Age = age,
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, generatedPassword);
                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                        ModelState.AddModelError("", error.Description);
                    return ReloadCreateView(reservation, roomTypeId);
                }

                await _userManager.AddToRoleAsync(user, "Guest");

                TempData["NewGuestEmail"] = guestEmail;
                TempData["NewGuestPassword"] = generatedPassword;
            }

            reservation.UserId = user.Id;

            var room = await _context.Rooms
                .Include(r => r.RoomType)
                .FirstOrDefaultAsync(r => r.RoomTypeId == roomTypeId && r.IsAvailable);

            if (room == null)
            {
                ModelState.AddModelError("", "Няма свободна стая от избрания тип.");
                return ReloadCreateView(reservation, roomTypeId);
            }

            room.IsAvailable = false;

            reservation.ReservationRooms = new List<ReservationRoom>
            {
                new ReservationRoom { RoomId = room.Id, Room = room }
            };

            if (serviceIds != null && serviceIds.Any())
            {
                var services = await _context.HotelServices
                    .Where(s => serviceIds.Contains(s.Id))
                    .ToListAsync();

                reservation.ReservationServices = services
                    .Select(s => new ReservationService { HotelService = s })
                    .ToList();
            }

            reservation.TotalPrice = _priceService.CalculateTotalPrice(reservation);
            reservation.CreatedAt = DateTime.UtcNow;
            reservation.Status ??= "Pending";

            _context.Add(reservation);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin,Receptionist")]
        public async Task<IActionResult> Edit(int id)
        {
            var reservation = await _context.Reservations
                .Include(r => r.ReservationRooms).ThenInclude(rr => rr.Room).ThenInclude(r => r.RoomType)
                .Include(r => r.ReservationServices).ThenInclude(rs => rs.HotelService)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null) return NotFound();

            var availableRoomTypes = await _context.RoomTypes
                .Where(rt => _context.Rooms.Any(r => r.RoomTypeId == rt.Id && r.IsAvailable))
                .Select(rt => new { rt.Id, rt.Name })
                .ToListAsync();

            var currentRoomTypeId = reservation.ReservationRooms.FirstOrDefault()?.Room?.RoomTypeId;
            if (currentRoomTypeId.HasValue && !availableRoomTypes.Any(t => t.Id == currentRoomTypeId.Value))
            {
                var currentType = await _context.RoomTypes
                    .Where(rt => rt.Id == currentRoomTypeId.Value)
                    .Select(rt => new { rt.Id, rt.Name })
                    .FirstOrDefaultAsync();

                if (currentType != null)
                    availableRoomTypes.Add(currentType);
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
            [Bind("Id,CheckInDate,CheckOutDate,Status,UserId")] Reservation reservation,
            int roomTypeId,
            List<int> serviceIds)
        {
            if (id != reservation.Id) return NotFound();


            ModelState.Remove("User");  

            var existing = await _context.Reservations
                .Include(r => r.ReservationRooms).ThenInclude(rr => rr.Room)
                .Include(r => r.ReservationServices)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (existing == null) return NotFound();

            if (!ModelState.IsValid)
            {
                var availableRoomTypes = await _context.RoomTypes
                    .Where(rt => _context.Rooms.Any(r => r.RoomTypeId == rt.Id && r.IsAvailable))
                    .Select(rt => new { rt.Id, rt.Name })
                    .ToListAsync();

                var currentRoomTypeIdd = existing.ReservationRooms.FirstOrDefault()?.Room?.RoomTypeId;
                if (currentRoomTypeIdd.HasValue && !availableRoomTypes.Any(t => t.Id == currentRoomTypeIdd.Value))
                {
                    var currentType = await _context.RoomTypes
                        .Where(rt => rt.Id == currentRoomTypeIdd.Value)
                        .Select(rt => new { rt.Id, rt.Name })
                        .FirstOrDefaultAsync();

                    if (currentType != null)
                        availableRoomTypes.Add(currentType);
                }

                ViewData["RoomTypes"] = new SelectList(availableRoomTypes, "Id", "Name", roomTypeId);

                ViewData["Services"] = new MultiSelectList(
                    _context.HotelServices,
                    "Id",
                    "Name",
                    serviceIds ?? existing.ReservationServices.Select(rs => rs.ServiceId)
                );

                return View(reservation);
            }


            existing.UserId = reservation.UserId;
            existing.CheckInDate = reservation.CheckInDate;
            existing.CheckOutDate = reservation.CheckOutDate;
            existing.Status = reservation.Status ?? "Pending";

            // Смяна на стая
            var currentRoomTypeId = existing.ReservationRooms.FirstOrDefault()?.Room?.RoomTypeId;
            if (roomTypeId != currentRoomTypeId)
            {
                foreach (var rr in existing.ReservationRooms)
                {
                    if (rr.Room != null)
                        rr.Room.IsAvailable = true;
                }

                var newRoom = await _context.Rooms
                    .Include(r => r.RoomType)
                    .FirstOrDefaultAsync(r => r.RoomTypeId == roomTypeId && r.IsAvailable);

                if (newRoom == null)
                {
                    ModelState.AddModelError("", "Няма свободна стая от избрания тип.");
                    return View(reservation);
                }

                newRoom.IsAvailable = false;

                existing.ReservationRooms.Clear();
                existing.ReservationRooms.Add(new ReservationRoom { RoomId = newRoom.Id, Room = newRoom });
            }

            // Обновяване на услугите
            existing.ReservationServices.Clear();
            if (serviceIds != null && serviceIds.Any())
            {
                var services = await _context.HotelServices
                    .Where(s => serviceIds.Contains(s.Id))
                    .ToListAsync();

                existing.ReservationServices = services
                    .Select(s => new ReservationService { HotelService = s })
                    .ToList();
            }

            existing.TotalPrice = _priceService.CalculateTotalPrice(existing);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin,Receptionist")]
        public async Task<IActionResult> Delete(int id)
        {
            var reservation = await _context.Reservations
                .Include(r => r.ReservationRooms)
                .ThenInclude(rr => rr.Room)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null) return NotFound();

            foreach (var rr in reservation.ReservationRooms)
            {
                if (rr.Room != null)
                    rr.Room.IsAvailable = true;
            }

            _context.Reservations.Remove(reservation);
            await _context.SaveChangesAsync();

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

        private string GenerateRandomPassword(int length)
        {
            const string validChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz123456789!@#$%^&*";
            var random = new Random();
            return new string(Enumerable.Repeat(validChars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}