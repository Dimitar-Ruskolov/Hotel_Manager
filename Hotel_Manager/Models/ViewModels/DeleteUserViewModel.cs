namespace Hotel_Manager.Models
{
    public class DeleteUserViewModel
    {
        public string Id { get; set; } = null!;
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Roles { get; set; }
    }
}