using System;
using System.Linq;
using Hotel_Manager.Models;

namespace Hotel_Manager.Services
{
    public class ReservationTotalPriceService
    {
        public decimal CalculateTotalPrice(Reservation reservation)
        {
            if (reservation is null)
                throw new ArgumentNullException(nameof(reservation));

            var nights = (reservation.CheckOutDate.Date - reservation.CheckInDate.Date).Days;
            if (nights <= 0)
                throw new InvalidOperationException("Invalid reservation dates.");

            var roomsPerNight = reservation.ReservationRooms
                .Sum(rr => rr.Room.RoomType.PricePerNight);

            var servicesTotal = reservation.ReservationServices
                .Sum(rs => rs.HotelService.Price);

            var total = (roomsPerNight * nights) + servicesTotal;
            return decimal.Round(total, 2);
        }
    }
}
