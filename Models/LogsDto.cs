namespace DatabaseAPI.Models
{
    public class LogsDto
    {
        public string Action { get; set; }
        public string TableName { get; set; }
        public int RecordId { get; set; }
        public string UserName { get; set; }
        public DateTime Timestamp { get; set; }
        public string Details { get; set; } // Added column}
    }
    public class LogsAddDto
    {
        public string Action { get; set; }
        public string TableName { get; set; }
        public string UserName { get; set; }
        public DateTime Timestamp { get; set; }
        public string Details { get; set; } // Added column}
    }
}
