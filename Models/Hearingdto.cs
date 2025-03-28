namespace DatabaseAPI.Models
{
    public class Hearingdto
    {
        public int HearingId { get; set; }
        public string HearingCaseTitle { get; set; }
        public string HearingCaseNumber { get; set; }
        public bool HearingCaseStatus { get; set; } // ✅ Changed from string to bool
        public string HearingCaseDate { get; set; } = string.Empty; // Format: "yyyy-MM-dd"
        public string HearingCaseTime { get; set; } = string.Empty; // Format: "HH:mm:ss"
        public string HearingCaseInputted { get; set; } = string.Empty;
    }
}
