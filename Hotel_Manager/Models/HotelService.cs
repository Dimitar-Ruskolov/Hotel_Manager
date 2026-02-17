namespace Hotel_Manager.Models
{
    public class HotelService
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;
        public decimal Price { get; set; }

        public ICollection<ReservationService> ReservationServices { get; set; } = new List<ReservationService>();
    }
}
