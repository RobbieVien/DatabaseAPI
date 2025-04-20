namespace DatabaseAPI.Models
{
    //Wala lang to
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

    public class CaseTitleReportdto
    {
        public string RecordCaseTitle { get; set; } = string.Empty;
    }

    //ETO YUNG BAGO SA POST TO
    public class NewCourtRecorddto
    {
        public int CourtRecordId { get; set; }
        public string RecordCaseNumber { get; set; } = string.Empty;
        public string RecordCaseTitle { get; set; } = string.Empty;
        public string RecordDateInputted { get; set; } = string.Empty; // Format: "yyyy-MM-dd"
        public string RecordTimeInputted { get; set; } = string.Empty; // Format: "HH:mm:ss"
        public string? RecordDateFiledOCC { get; set; }  // Format: "yyyy-MM-dd" 
        public string? RecordDateFiledReceived { get; set; }  // Format: "yyyy-MM-dd"
        public string RecordTransfer { get; set; } = string.Empty;
        public string RecordRepublicAct { get; set; } = string.Empty;
        public string RecordNatureDescription { get; set; } = string.Empty;
    }

    // ETO SA UPDATE
    public class UpdateCourtRecorddto
    {
        public string RecordCaseNumber { get; set; } = string.Empty;
        public string RecordCaseTitle { get; set; } = string.Empty;
        public string RecordCaseStatus { get; set; } = string.Empty;
        public string RecordRepublicAct { get; set; } = string.Empty;
        public string RecordNatureDescription { get; set; } = string.Empty;
        public string RecordTransfer { get; set; } = string.Empty;

        // Update to Nullable DateTime (DateTime?)
        public DateTime? RecordDateFiledOCC { get; set; }
        public DateTime? RecordDateFiledReceived { get; set; }
        public DateTime? RecordDateDisposal { get; set; }
        public DateTime? RecordDateArchival { get; set; }
        public DateTime? RecordDateRevival { get; set; }

        public string CaseStage { get; set; } = string.Empty;

        // Update to Nullable DateTime (DateTime?)
        public DateTime? RecordNextHearing { get; set; }
    }


    //Eto yung getALL sa Datagridview naka base kay joie
    public class GetAllCourtRecorddto
    {   
        public string RecordCaseNumber { get; set; } = string.Empty;
        public string RecordCaseTitle { get; set; } = string.Empty;
        public string RecordDateInputted { get; set; } = string.Empty; // Format: "yyyy-MM-dd"
        public string? RecordDateFiledReceived { get; set; }  // Format: "yyyy-MM-dd"
        public string RecordCaseStatus { get; set; } = string.Empty;
        public string RecordRepublicAct { get; set; } = string.Empty;
        public string RecordNatureDescription { get; set; } = string.Empty;
    }



}
