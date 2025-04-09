namespace DatabaseAPI.Models
{
    public class Marriagedto
    {
        public int MarriageId { get; set; }
        public DateTime MarriageOCC { get; set; }
        public DateTime MarriageBranch { get; set; } 
        public string MarriageBrideLastName { get; set; } 
        public string MarriageBrideFirstName { get; set; } 
        public string MarriageBrideMiddleName { get; set; } 
        public string MarriageGroomLastName { get; set; }
        public string MarriageGroomFirstName { get; set; }
        public string MarriageGroomMiddleName { get; set; }
        public string MarriageJudge { get; set; }
        public DateTime MarriageInputted { get; set; }
        public DateTime MarriageStartIn { get; set; }
        public Boolean NotifyMe { get; set; }
        public Boolean Checkbox { get; set; }

    }
}
