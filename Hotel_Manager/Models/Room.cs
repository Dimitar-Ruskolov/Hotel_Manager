namespace Hotel_Manager.Models
{
    public class Room
    {
        public int Id { get; set; }

        public string RoomNumber { get; set; } = null!;
        public bool IsAvailable { get; set; } = true;

        public int RoomTypeId { get; set; }
        public RoomType RoomType { get; set; } = null!;

        public ICollection<ReservationRoom> ReservationRooms { get; set; } = new List<ReservationRoom>();
    }
}
