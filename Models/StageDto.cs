namespace DatabaseAPI.Models
{
    public class StageDto
    {
        public string Stage { get; set; }
        public int UsageCount { get; set; }
    }
    public class UpdateStageDto
    {
        public int StageId { get; set; }
        public string Stage { get; set; }
        public int UsageCount { get; set; }
    }

}
