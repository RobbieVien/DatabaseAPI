namespace DatabaseAPI.Models
{
    public class Hearingdto
    {
        public int HearingId { get; set; }
        public string HearingCaseTitle { get; set; }
        public string HearingCaseNumber { get; set; }
        public string HearingJudge { get; set; }
        public string HearingTrialProsecutor { get; set; }
        public string HearingBranchClerk { get; set; }
        public string HearingPublicAttorney { get; set; }
        public string HearingCourtInterpreter { get; set; }
        public string HearingCourtStenographer { get; set; }
        public Boolean HearingCaseStatus { get; set; }
        public DateOnly HearingCaseDate { get; set; }
        public TimeOnly HearingCaseTime { get; set; } 
        public string HearingCaseInputted { get; set; }
        public int HearingNotify { get; set; }
    }
}
