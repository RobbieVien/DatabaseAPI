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
    public class FullnameDto // Ensure 'public' is here
    {
        public string Name { get; set; }
    }
    public class AddingUserDto // Ensure 'public' is here
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Role { get; set; }
        public string Status { get; set; }
        public string UserName { get; set; }

    }
    public class GettingUserDto // Ensure 'public' is here
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Role { get; set; }
        public string Status { get; set; }
        public string UserName { get; set; }

    }
    public class EditUserDto // Ensure 'public' is here
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public string ConfirmPassword { get; set; }
    }

}
