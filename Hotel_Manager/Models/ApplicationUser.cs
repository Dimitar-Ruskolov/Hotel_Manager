using Microsoft.AspNetCore.Identity;  

namespace Hotel_Manager.Models
{
    public class ApplicationUser : IdentityUser  
    {
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public int Age { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<UserCar> Cars { get; set; } = new List<UserCar>();
        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    }
}