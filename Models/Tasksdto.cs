namespace DatabaseAPI.Models
{
    public class Tasksdto
    {
        public string ScheduleTaskTitle { get; set; }
        public string ScheduleUser { get; set; }  // This should contain the username
        public string ScheduleTaskDescription { get; set; }
        public DateTime ScheduleDate { get; set; }
        public DateTime ScheduleInputted { get; set; }
        public Boolean ScheduleStatus { get; set; }
        public int ScheduleNotify { get; set; }
        public int? UserId { get; set; }  // Optional user ID field
    }

    public class taskDashboard
    {
        public string ScheduleTaskTitle { get; set; }
        public string ScheduleUser { get; set; }  // This should contain the username
        public string ScheduleTaskDescription { get; set; }
        public DateTime ScheduleDate { get; set; }
        public Boolean ScheduleStatus { get; set; }
    }
    public class taskDoubleClickDashboard
    {
        public string ScheduleTaskTitle { get; set; }
        public string ScheduleUser { get; set; }  // This should contain the username
        public string ScheduleTaskDescription { get; set; }
        public DateTime ScheduleDate { get; set; }
        public Boolean ScheduleStatus { get; set; }
        public int ScheduleNotify { get; set; }
    }
    public class UserTaskUpdateDto
    {
        public bool ScheduleStatus { get; set; }
        public int ScheduleNotify { get; set; }
    }

}
