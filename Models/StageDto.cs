namespace DatabaseAPI.Models
{
    public class StageDto
    {
        public string Stage { get; set; }
    }
    public class UpdateStageRequest
    {
        public StageDto Stage { get; set; }
    }
}
