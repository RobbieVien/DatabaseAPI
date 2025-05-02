namespace DatabaseAPI.Models
{
    public class StageDto
    {
        public int StageID { get; set; }
        public string Stage { get; set; }
        public int StageUsageCount { get; set; }
    }
    public class UpdateStageRequest
    {
        public EditStageDto Stage { get; set; }
    }

    public class EditStageDto
    {
        public int StageID { get; set; }
        public string Stage { get; set; }
    }
    
}

