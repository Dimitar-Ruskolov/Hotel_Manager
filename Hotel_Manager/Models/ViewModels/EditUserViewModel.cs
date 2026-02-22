using System.ComponentModel.DataAnnotations;

namespace Hotel_Manager.Models
{
    public class EditUserViewModel
    {
        public string Id { get; set; } = null!;
        public string? Email { get; set; }
        [Required]
        public string FirstName { get; set; } = null!;
        [Required]
        public string LastName { get; set; } = null!;
        [Range(18, 120)]
        public int Age { get; set; }
        public bool IsActive { get; set; }
        public string CurrentRole { get; set; } = "";
        public string? NewRole { get; set; }
    }
}