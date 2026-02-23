namespace Hotel_Manager.Models
{
    public class EditUserViewModel
    {
        public string Id { get; set; } = null!;

        public string Email { get; set; } = null!;

        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public int Age { get; set; }
        public bool IsActive { get; set; }

        public string CurrentRole { get; set; } = string.Empty;
        public string? NewRole { get; set; }          
    }
}