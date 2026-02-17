namespace Hotel_Manager.Models
{
    public class ReservationService
    {
        public int ReservationId { get; set; }
        public Reservation Reservation { get; set; } = null!;

        public int ServiceId { get; set; }
        public HotelService HotelService { get; set; } = null!;
    }
}
