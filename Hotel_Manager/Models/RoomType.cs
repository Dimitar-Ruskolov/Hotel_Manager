namespace Hotel_Manager.Models
{
    public class RoomType
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;
        public int Capacity { get; set; }
        public decimal PricePerNight { get; set; }

        public ICollection<Room> Rooms { get; set; } = new List<Room>();
    }
}
