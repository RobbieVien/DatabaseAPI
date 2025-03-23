namespace DatabaseAPI.Models
{
    public class CourtRecorddto
    {
        public int CourtRecordId { get; set; }
        public string RecordCaseNumber { get; set; } = string.Empty;
        public string RecordCaseTitle { get; set; } = string.Empty;
        public string RecordDateInputted { get; set; } = string.Empty; // Format: "yyyy-MM-dd"
        public string RecordTimeInputted { get; set; } = string.Empty; // Format: "HH:mm:ss"
        public string? RecordDateFiledOCC { get; set; }  // Format: "yyyy-MM-dd" 
        public string? RecordDateFiledReceived { get; set; }  // Format: "yyyy-MM-dd"
        public string RecordTransfer { get; set; } = string.Empty;
        public string RecordCaseStatus { get; set; } = string.Empty;
        public string RecordNatureCase { get; set; } = string.Empty;
        public string RecordNatureDescription { get; set; } = string.Empty;
    }
}
