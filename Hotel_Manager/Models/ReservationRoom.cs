namespace Hotel_Manager.Models
{
    public class ReservationRoom
    {
        public int ReservationId { get; set; }
        public Reservation Reservation { get; set; } = null!;

        public int RoomId { get; set; }
        public Room Room { get; set; } = null!;
    }
}
