namespace Hotel_Manager.Models
{
    public class Reservation
    {
        public int Id { get; set; }

        public DateTime CheckInDate { get; set; }
        public DateTime CheckOutDate { get; set; }

        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public ICollection<ReservationRoom> ReservationRooms { get; set; } = new List<ReservationRoom>();
        public ICollection<ReservationService> ReservationServices { get; set; } = new List<ReservationService>();
    
    }
}
