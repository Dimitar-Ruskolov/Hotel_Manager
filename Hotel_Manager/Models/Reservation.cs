namespace Hotel_Manager.Models
{
    public class Reservation
    {
        public int Id { get; set; }

        public DateTime CheckInDate { get; set; }
        public DateTime CheckOutDate { get; set; }

        public decimal TotalPrice { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public ICollection<ReservationRoom> ReservationRooms { get; set; } = new List<ReservationRoom>();
        public ICollection<ReservationService> ReservationServices { get; set; } = new List<ReservationService>();

        public void CalculateTotalPrice(decimal pricePerNight, IEnumerable<decimal> servicePrices)
        {
            var nights = (CheckOutDate.Date - CheckInDate.Date).Days;
            if (nights <= 0)
            {
                throw new InvalidOperationException("Check-out must be after check-in.");
            }
            else
            {
                var basePrice = nights * pricePerNight;
                var servicesTotal = servicePrices.Sum();
                TotalPrice = basePrice + servicesTotal;
            }
                

            
        }
    }
}
