using Microsoft.EntityFrameworkCore;
using Hotel_Manager.Data;     
using Hotel_Manager.Models;   

namespace Hotel_Manager.Services
{
    public class ReservationTotalPriceService
    {
        private readonly ApplicationDbContext _context;

        public ReservationTotalPriceService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task CalculateTotalPriceAsync(int reservationId)
        {
            var reservation = await _context.Reservations
                .FirstOrDefaultAsync(r => r.Id == reservationId);        

            var pricePerNight = await _context.Rooms
                .Where(r => r.Id == reservation.RoomId)
                .Select(r => r.RoomType.PricePerNight)
                .FirstAsync();

            var servicePrices = await _context.ReservationServices
                .Where(rs => rs.ReservationId == reservation.Id)
                .Select(rs => rs.HotelService.Price)
                .ToListAsync();

            var nights = (reservation.CheckOutDate.Date - reservation.CheckInDate.Date).Days;

            reservation.TotalPrice = (nights * pricePerNight) + servicePrices.Sum();

            await _context.SaveChangesAsync();
        }
    }
}