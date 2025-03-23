namespace DatabaseAPI.Models
{
    public class ReportDto
    {
        public int Report_Id { get; set; }
        public string Report_NatureCase { get; set; }
        public int? CourtRecord_LinkId { get; set; }
        public int CaseCount { get; set; }
    }
}
