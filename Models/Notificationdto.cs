namespace DatabaseAPI.Models
{
    public class Notification
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }  // Make sure it's a string, not a byte array
        public string Type { get; set; }
    }
}
