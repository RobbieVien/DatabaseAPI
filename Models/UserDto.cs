namespace DatabaseAPI.Models
{
    public class UserDto // Ensure 'public' is here
    {
        public int UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Role { get; set; }
        public string Status { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
    }
}
