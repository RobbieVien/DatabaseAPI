namespace DatabaseAPI.Models
{
    public class Tasksdto
    {
        public int ScheduleId { get; set; }
        public string ScheduleTaskTitle { get; set; }
        public string ScheduleTaskDescription { get; set; }
        public DateTime ScheduleDate { get; set; }
        public string ScheduleStatus { get; set; }
    }
}
