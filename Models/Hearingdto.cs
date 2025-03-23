namespace DatabaseAPI.Models
{
    public class Hearingdto
    {
        public int HearingId { get; set; }
        public string HearingCaseTitle { get; set; }
        public string HearingCaseNumber { get; set; }
        public string HearingCaseStatus { get; set; }
        public string HearingCaseDate { get; set; } = string.Empty; // Format: "yyyy-MM-dd"
        public string HearingCaseTime { get; set; } = string.Empty; // Format: "HH:mm:ss"
    }
}
