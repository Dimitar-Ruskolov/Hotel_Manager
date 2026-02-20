namespace Hotel_Manager.Models
{
    public class UserCar
    {
        public int Id { get; set; }

        public string LicensePlate { get; set; } = null!;
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;
    }
}
