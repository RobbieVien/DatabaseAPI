namespace DatabaseAPI.Models
{
    public class Tasksdto
    {
        public int ScheduleId { get; set; }
        public string ScheduleTaskTitle { get; set; } = string.Empty;
        public string ScheduleUser { get; set; } = string.Empty;
        public string ScheduleTaskDescription { get; set; } = string.Empty;
        public DateTime ScheduleDate { get; set; }
        public DateTime ScheduleInputted { get; set; }
        public bool ScheduleStatus { get; set; }
    }
}
