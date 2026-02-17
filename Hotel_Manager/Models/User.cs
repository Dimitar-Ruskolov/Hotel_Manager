namespace Hotel_Manager.Models
{
    public class User
    {
        public int Id { get; set; }

        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public int Age { get; set; }

        public string Email { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public ICollection<UserCar> Cars { get; set; } = new List<UserCar>();
        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    }
}
