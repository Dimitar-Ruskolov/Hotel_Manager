using Hotel_Manager.Data;
using Hotel_Manager.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace Hotel_Manager.Services
{
    public class ReservationLogic
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ReservationTotalPriceService _priceService;
        private readonly RoomAvailabilityService _roomAvailabilityService;

        public ReservationLogic(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ReservationTotalPriceService priceService,
            RoomAvailabilityService roomAvailabilityService)
        {
            _context = context;
            _userManager = userManager;
            _priceService = priceService;
            _roomAvailabilityService = roomAvailabilityService;
        }
        public class UpdateResult
        {
            public bool Success { get; set; }
            public List<string> Errors { get; set; } = new();
        }

        public string DetermineStatus(DateTime checkIn, DateTime checkOut)
        {
            var now = DateTime.UtcNow.Date;
            if (checkOut < now) return "Completed";
            if (checkIn <= now && checkOut >= now) return "In Progress";
            return "Upcoming";
        }

        public class CreateReservationResult
        {
            public bool Success { get; set; }
            public List<string> Errors { get; set; } = new();
            public string? NewGuestEmail { get; set; }
            public string? NewGuestPassword { get; set; }
            public int? ReservationId { get; set; }
        }

        public async Task<CreateReservationResult> CreateAsync(
            Reservation reservation,
            string guestEmail,
            string? firstName,
            string? lastName,
            int? age,
            int roomTypeId,
            List<int>? serviceIds,
            string? licensePlate)
        {
            var result = new CreateReservationResult();

            if (string.IsNullOrWhiteSpace(guestEmail))
            {
                result.Errors.Add("Guest email is required.");
                return result;
            }


            var user = await _userManager.FindByEmailAsync(guestEmail);
            if (user == null)
            {
                var generatedPassword = GenerateRandomPassword(10);

                user = new ApplicationUser
                {
                    UserName = guestEmail,
                    Email = guestEmail,
                    FirstName = string.IsNullOrWhiteSpace(firstName) ? "Guest" : firstName,
                    LastName = string.IsNullOrWhiteSpace(lastName) ? "Guest" : lastName,
                    Age = age ?? 0,
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var createUser = await _userManager.CreateAsync(user, generatedPassword);
                if (!createUser.Succeeded)
                {
                    result.Errors.AddRange(createUser.Errors.Select(e => e.Description));
                    return result;
                }

                await _userManager.AddToRoleAsync(user, "Guest");

                result.NewGuestEmail = guestEmail;
                result.NewGuestPassword = generatedPassword;
            }

            reservation.UserId = user.Id;

            if (!string.IsNullOrWhiteSpace(licensePlate))
            {
                var plate = licensePlate.Trim().ToUpperInvariant();
                var exists = await _context.UserCars.AnyAsync(c => c.UserId == user.Id && c.LicensePlate == plate);
                if (!exists)
                {
                    _context.UserCars.Add(new UserCar { LicensePlate = plate, UserId = user.Id });
                }
            }


            var room = await _context.Rooms
                .Include(r => r.RoomType)
                .FirstOrDefaultAsync(r => r.RoomTypeId == roomTypeId && r.IsAvailable);

            if (room == null)
            {
                result.Errors.Add("No available room of the selected type.");
                return result;
            }

            room.IsAvailable = false;
            _context.Rooms.Update(room);

            reservation.ReservationRooms = new List<ReservationRoom>
            {
                new ReservationRoom { RoomId = room.Id }
            };


            if (serviceIds?.Any() == true)
            {
                var selectedServices = await _context.HotelServices
                    .Where(s => serviceIds.Contains(s.Id))
                    .ToListAsync();

                if (selectedServices.Count != serviceIds.Count)
                {
                    result.Errors.Add("One or more selected services no longer exist.");
                    return result;
                }

                reservation.ReservationServices = selectedServices
                    .Select(s => new ReservationService
                    {
                        ServiceId = s.Id,
                        HotelService = s
                    })
                    .ToList();
            }

            reservation.Status = DetermineStatus(reservation.CheckInDate, reservation.CheckOutDate);
            reservation.CreatedAt = DateTime.UtcNow;

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var savedReservation = await _context.Reservations
                .Include(r => r.ReservationRooms).ThenInclude(rr => rr.Room).ThenInclude(r => r.RoomType)
                .Include(r => r.ReservationServices).ThenInclude(rs => rs.HotelService)
                .FirstOrDefaultAsync(r => r.Id == reservation.Id);

            if (savedReservation != null)
            {
                savedReservation.TotalPrice = _priceService.CalculateTotalPrice(savedReservation);
                await _context.SaveChangesAsync();
            }

            result.Success = true;
            result.ReservationId = reservation.Id;
            return result;
        }

        private static string GenerateRandomPassword(int length)
        {
            const string validChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz123456789!@#$%^&*";
            var bytes = RandomNumberGenerator.GetBytes(length);
            return new string(bytes.Select(b => validChars[b % validChars.Length]).ToArray());
        }
        public async Task<UpdateResult> UpdateReservationAsync(
    int reservationId,
    DateTime checkInDate,
    DateTime checkOutDate,
    int roomTypeId,
    List<int>? serviceIds)
        {
            var result = new UpdateResult();

            var existing = await _context.Reservations
                .Include(r => r.ReservationRooms).ThenInclude(rr => rr.Room).ThenInclude(r => r.RoomType)
                .Include(r => r.ReservationServices)
                .FirstOrDefaultAsync(r => r.Id == reservationId);

            if (existing == null)
            {
                result.Errors.Add("Reservation not found.");
                return result;
            }

            existing.CheckInDate = checkInDate;
            existing.CheckOutDate = checkOutDate;
            existing.Status = DetermineStatus(checkInDate, checkOutDate);

            var currentRoomTypeId = existing.ReservationRooms.FirstOrDefault()?.Room?.RoomTypeId;
            if (currentRoomTypeId == null)
            {
                result.Errors.Add("Reservation has no assigned room.");
                return result;
            }

            if (roomTypeId != currentRoomTypeId.Value)
            {
                await _roomAvailabilityService.SetAvailabilityByReservationIdAsync(existing.Id, true);

                _context.ReservationRooms.RemoveRange(existing.ReservationRooms);
                existing.ReservationRooms.Clear();

                var newRoom = await _context.Rooms
                    .Include(r => r.RoomType)
                    .FirstOrDefaultAsync(r => r.RoomTypeId == roomTypeId && r.IsAvailable);

                if (newRoom == null)
                {
                    result.Errors.Add("Няма свободна стая от избрания тип.");
                    return result;
                }

                newRoom.IsAvailable = false;
                _context.Rooms.Update(newRoom);

                existing.ReservationRooms.Add(new ReservationRoom { RoomId = newRoom.Id });
            }

            _context.ReservationServices.RemoveRange(existing.ReservationServices);
            existing.ReservationServices.Clear();

            if (serviceIds?.Any() == true)
            {
                var selectedServices = await _context.HotelServices
                    .Where(s => serviceIds.Contains(s.Id))
                    .ToListAsync();

                if (selectedServices.Count != serviceIds.Count)
                {
                    result.Errors.Add("One or more selected services no longer exist.");
                    return result;
                }

                existing.ReservationServices = selectedServices
                    .Select(s => new ReservationService
                    {
                        ServiceId = s.Id,
                        HotelService = s
                    })
                    .ToList();
            }
            else
            {
                existing.ReservationServices = new List<ReservationService>();
            }

            existing.TotalPrice = _priceService.CalculateTotalPrice(existing);

            await _context.SaveChangesAsync();

            result.Success = true;
            return result;
        }
    }

}