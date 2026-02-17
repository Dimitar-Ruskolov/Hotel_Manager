namespace Hotel_Manager.Models
{
    public class UserCar
    {
        public int Id { get; set; }

        public string LicensePlate { get; set; } = null!;

        public int UserId { get; set; }
        public User User { get; set; } = null!;
    }
}
