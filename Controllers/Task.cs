using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Dapper;
using System.Collections.Generic;
using DatabaseAPI.Models;
using DatabaseAPI.Utilities;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Office2010.Excel;
using System.Security.Claims;
using DocumentFormat.OpenXml.Office2021.DocumentTasks;


//ITEXT
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.IO.Font.Constants;
using iText.IO.Font;
using iText.Kernel.Font;
using iText.Kernel.Exceptions;

[Route("api/[controller]")]
[ApiController]
public class TaskController : ControllerBase
{
    private readonly string _connectionString;

    public TaskController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }

    // Add Tasks
    [HttpPost("AddTasks")]
    public async Task<IActionResult> AddTasks([FromBody] Tasksdto tasks)
    {
        if (tasks == null || string.IsNullOrWhiteSpace(tasks.ScheduleTaskTitle))
        {
            return BadRequest("Invalid Task data.");
        }

        var username = HttpContext.Session.GetString("UserName");

        // Get Philippine time (UTC+8)
        DateTime philippineTime = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time")
        );

        // Use the provided ScheduleDate and ScheduleInputted directly
        DateTime taskDate = tasks.ScheduleDate;
        DateTime taskInputted = tasks.ScheduleInputted;

        // Check if task date is in the past (with a small buffer)
        if (taskDate.Date < philippineTime.Date)
        {
            return BadRequest("Cannot add a task with a past date. Please choose a future date.");
        }

        // Calculate the difference in days between sched_date and sched_inputted
        int daysDifference = (taskDate - taskInputted).Days;

        // Validate that sched_notify is not greater than the difference in days
        if (tasks.ScheduleNotify > daysDifference)
        {
            return BadRequest($"Notification cannot be set to {tasks.ScheduleNotify} days. The maximum valid notification time is {daysDifference} days.");
        }

        using (var con = new MySqlConnection(_connectionString))
        {
            await con.OpenAsync();
            try
            {
                // Find user ID based on the provided username
                string userQuery = @"SELECT user_Id, user_Name FROM ManageUsers 
                               WHERE user_Name = @UserName";

                var userResult = await con.QueryFirstOrDefaultAsync<dynamic>(
                    userQuery,
                    new { UserName = tasks.ScheduleUser }
                );

                if (userResult == null)
                {
                    return BadRequest("User not found. Please provide a valid username.");
                }

                int userId = userResult.user_Id;
                string userName = userResult.user_Name;

                // Check if a task with the same title, date, and user already exists
                string checkQuery = @"SELECT COUNT(*) FROM Tasks 
                                WHERE sched_taskTitle = @TaskTitle 
                                AND DATE(sched_date) = DATE(@Date)
                                AND user_Id = @UserId";

                var existingCount = await con.ExecuteScalarAsync<int>(
                    checkQuery,
                    new
                    {
                        TaskTitle = tasks.ScheduleTaskTitle.Trim(),
                        Date = tasks.ScheduleDate,
                        UserId = userId
                    }
                );

                if (existingCount > 0)
                {
                    return Conflict($"A task with the title '{tasks.ScheduleTaskTitle}' already exists for this user on the selected date.");
                }

                // Insert new task with user ID
                string insertQuery = @"INSERT INTO Tasks (
                sched_taskTitle,
                sched_user,
                sched_taskDescription, 
                sched_date, 
                sched_inputted, 
                sched_status,
                sched_notify,
                user_Id
            ) VALUES (
                @TaskTitle,
                @TaskUser,
                @TaskDescription, 
                @Date, 
                @InputtedTime, 
                @Status,
                @Notify,
                @UserId
            )";

                // Ensure that tasks are created as "Pending" (false) by default
                const bool defaultStatusValue = false; // Always create tasks as pending

                int rowsAffected = await con.ExecuteAsync(
                    insertQuery,
                    new
                    {
                        TaskTitle = tasks.ScheduleTaskTitle.Trim(),
                        TaskUser = userName,   // Store the username for display purposes
                        TaskDescription = tasks.ScheduleTaskDescription,
                        Date = tasks.ScheduleDate,
                        InputtedTime = philippineTime,
                        Status = defaultStatusValue,
                        Notify = tasks.ScheduleNotify,
                        UserId = userId        // Store the user ID for relationships
                    }
                );

                if (rowsAffected == 0)
                {
                    return StatusCode(500, "Task insertion failed.");
                }

                // Log the action
                await Logger.LogActionAdd(HttpContext,
                    action: "INSERT",
                    tableName: "Tasks",
                    details: $"Task '{tasks.ScheduleTaskTitle}' added successfully for user '{userName}' (ID: {userId})."
                );

                return Ok(new { message = "Task added successfully.", userId = userId, userName = userName });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }
    //--------------------------------------------------------------------------------------------------------------------------------------------

    //eto yung add task kaso yung 3 lang nirerequest
    [HttpPost("NewAddTasks")]
    public async Task<IActionResult> NewAddTasks([FromBody] TaskAdding task)
    {
        if (task == null || string.IsNullOrWhiteSpace(task.ScheduleTaskTitle))
        {
            return BadRequest("Invalid Task data.");
        }

        DateTime philippineTime = TimeZoneInfo.ConvertTimeFromUtc(
       DateTime.UtcNow,
       TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time")
   );

        DateTime taskDate = task.ScheduleDate;

        // 🚫 Prevent adding past tasks
        if (taskDate.Date < philippineTime.Date)
        {
            return BadRequest("Cannot add a task with a past date. Please choose a future date.");
        }
        using (var con = new MySqlConnection(_connectionString))
        {
            await con.OpenAsync();
            try
            {
                // Check if a task with the same title and date already exists
                string checkQuery = @"SELECT COUNT(*) FROM Tasks 
                                  WHERE sched_taskTitle = @TaskTitle 
                                  AND DATE(sched_date) = DATE(@Date)";

                var existingCount = await con.ExecuteScalarAsync<int>(
                    checkQuery,
                    new
                    {
                        TaskTitle = task.ScheduleTaskTitle.Trim(),
                        Date = task.ScheduleDate
                    }
                );

                // Insert new task (only 3 fields)
                string insertQuery = @"INSERT INTO Tasks (
                sched_taskTitle,
                sched_date,
                sched_taskDescription
            ) VALUES (
                @TaskTitle,
                @Date,
                @TaskDescription
            )";

                int rowsAffected = await con.ExecuteAsync(
                    insertQuery,
                    new
                    {
                        TaskTitle = task.ScheduleTaskTitle.Trim(),
                        Date = task.ScheduleDate,
                        TaskDescription = task.ScheduleTaskDescription
                    }
                );

                if (rowsAffected == 0)
                {
                    return StatusCode(500, "Task insertion failed.");
                }

                return Ok(new { message = "Task added successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }

    [HttpPut("NewUpdateTask")]
    public async Task<IActionResult> NewUpdateTask(int id, [FromBody] TaskAdding task)
    {
        if (task == null || string.IsNullOrWhiteSpace(task.ScheduleTaskTitle))
        {
            return BadRequest("Invalid Task data.");
        }
        DateTime philippineTime = TimeZoneInfo.ConvertTimeFromUtc(
          DateTime.UtcNow,
          TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time")
         );

        DateTime taskDate = task.ScheduleDate;

        // 🚫 Prevent updating to a past date
        if (taskDate.Date < philippineTime.Date)
        {
            return BadRequest("Cannot update a task to a past date. Please choose a future date.");
        }

        using (var con = new MySqlConnection(_connectionString))
        {
            await con.OpenAsync();
            try
            {
                // Check if task exists by ID
                string checkQuery = @"SELECT COUNT(*) FROM Tasks WHERE sched_Id = @Id";

                var existingCount = await con.ExecuteScalarAsync<int>(
                    checkQuery,
                    new { Id = id }
                );

                if (existingCount == 0)
                {
                    return NotFound("Task not found. Cannot update a non-existing task.");
                }

                // Update the task details
                string updateQuery = @"UPDATE Tasks
                                   SET 
                                       sched_taskTitle = @TaskTitle,
                                       sched_date = @Date,
                                       sched_taskDescription = @TaskDescription
                                   WHERE sched_Id = @Id";

                int rowsAffected = await con.ExecuteAsync(
                    updateQuery,
                    new
                    {
                        TaskTitle = task.ScheduleTaskTitle.Trim(),
                        Date = task.ScheduleDate,
                        TaskDescription = task.ScheduleTaskDescription,
                        Id = id
                    }
                );

                if (rowsAffected == 0)
                {
                    return StatusCode(500, "Task update failed.");
                }

                return Ok(new { message = "Task updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }


    //eto sa datagridview

    [HttpGet("DisplayTasks")]
    public async Task<IActionResult> DisplayTasks()
    {
        using (var con = new MySqlConnection(_connectionString))
        {
            await con.OpenAsync();
            try
            {
                // Get Philippine time
                DateTime philippineTime = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow,
                    TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time")
                );

                // Retrieve all tasks with sched_Id
                var tasks = (await con.QueryAsync<TaskDisplay>(@"
                SELECT 
                    sched_Id AS TaskId,
                    sched_taskTitle AS ScheduleTaskTitle,
                    sched_date AS ScheduleDate,
                    sched_taskDescription AS ScheduleTaskDescription,
                    sched_status AS ScheduleStatus,
                    sched_checkbox AS ScheduleCheckbox
                FROM Tasks
            ")).ToList();

                foreach (var task in tasks.ToList()) // Use ToList() to allow safe removal
                {
                    // If checkbox is checked, delete the task
                    if (task.ScheduleCheckbox)
                    {
                        await con.ExecuteAsync("DELETE FROM Tasks WHERE sched_Id = @TaskId",
                            new { TaskId = task.TaskId });

                        tasks.Remove(task); // Remove from list so it won't display
                        continue;
                    }

                    // Determine task status based on the date
                    if (task.ScheduleDate.Date < philippineTime.Date)
                    {
                        task.ScheduleStatus = "Overdue";
                    }
                    else
                    {
                        task.ScheduleStatus = "Pending";
                    }

                    // Update the task status using sched_Id
                    await con.ExecuteAsync(@"
                    UPDATE Tasks
                    SET sched_status = @Status
                    WHERE sched_Id = @TaskId",
                        new
                        {
                            Status = task.ScheduleStatus,
                            TaskId = task.TaskId
                        });
                }

                return Ok(tasks);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }

    //eto pag dinoble click sa sched
    [HttpGet("GetTaskDetails")]
    public async Task<IActionResult> GetTaskDetails()
    {
        using (var con = new MySqlConnection(_connectionString))
        {
            await con.OpenAsync();
            try
            {
                // Get Philippine time
                DateTime philippineTime = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow,
                    TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time")
                );

                // Fetch all tasks
                var tasks = (await con.QueryAsync<DoubleClickTask>(@"
                SELECT 
                    sched_taskTitle AS ScheduleTaskTitle,
                    sched_date AS ScheduleDate,
                    sched_taskDescription AS ScheduleTaskDescription,
                    sched_status AS ScheduleStatus
                FROM Tasks
            ")).ToList();

                // Build the result list
                var result = new List<object>();

                foreach (var task in tasks)
                {
                    var dateOnly = task.ScheduleDate.Date;
                    var today = philippineTime.Date;

                    string dayOfWeek = task.ScheduleDate.ToString("ddd"); // Example: Mon, Tue

                    string statusMessage = "";

                    if (dateOnly > today)
                    {
                        int daysLeft = (dateOnly - today).Days;
                        statusMessage = $"{daysLeft} day(s) left until the task.";
                    }
                    else if (dateOnly < today)
                    {
                        int daysOverdue = (today - dateOnly).Days;
                        statusMessage = $"Task is overdue by {daysOverdue} day(s).";
                    }
                    else
                    {
                        statusMessage = "Task is scheduled for today!";
                    }

                    result.Add(new
                    {
                        StatusMessage = statusMessage,
                        TaskDetails = new
                        {
                            task.ScheduleTaskTitle,
                            ScheduleDate = $"{task.ScheduleDate:yyyy-MM-dd} ({dayOfWeek})", // Example: 2025-04-29 (Tue)
                            task.ScheduleTaskDescription,
                            task.ScheduleStatus
                        }
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }

    //eto sa daashboard to

    [HttpGet("ViewAllTasks")]
    public async Task<IActionResult> ViewAllTasks()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = @"SELECT 
                        sched_taskTitle AS ScheduleTaskTitle, 
                        sched_date AS ScheduleDate, 
                        sched_taskDescription AS ScheduleTaskDescription 
                     FROM Tasks 
                     ORDER BY sched_date ASC";

        var tasks = await con.QueryAsync<TaskAdding>(query);

        return Ok(tasks);
    }

    //daashboard to
    [HttpGet("ViewUpcomingTasks")]
    public async Task<IActionResult> ViewUpcomingTasks()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        // Philippine time CURDATE()
        string query = @"SELECT 
                        sched_taskTitle AS ScheduleTaskTitle, 
                        sched_date AS ScheduleDate, 
                        sched_taskDescription AS ScheduleTaskDescription 
                     FROM Tasks 
                     WHERE sched_date > CURDATE()
                     ORDER BY sched_date ASC";

        var tasks = await con.QueryAsync<TaskAdding>(query);

        return Ok(tasks);
    }
    //daashboard to
    [HttpGet("ViewDueTodayTasks")]
    public async Task<IActionResult> ViewDueTodayTasks()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = @"SELECT 
                     sched_taskTitle AS ScheduleTaskTitle, 
                     sched_date AS ScheduleDate, 
                     sched_taskDescription AS ScheduleTaskDescription 
                     FROM Tasks 
                     WHERE DATE(sched_date) = CURDATE()
                     ORDER BY sched_date ASC";

        var tasks = await con.QueryAsync<TaskAdding>(query);

        return Ok(tasks);
    }
    //daashboard to
    [HttpGet("ViewOverdueTasks")]
    public async Task<IActionResult> ViewOverdueTasks()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = @"SELECT 
                     sched_taskTitle AS ScheduleTaskTitle, 
                     sched_date AS ScheduleDate, 
                     sched_taskDescription AS ScheduleTaskDescription 
                     FROM Tasks 
                     WHERE sched_date < CURDATE()
                     ORDER BY sched_date ASC";

        var tasks = await con.QueryAsync<TaskAdding>(query);

        return Ok(tasks);
    }
    //daashboard to
    //gumagana lahat ng counting kuhanin mo to 
    [HttpGet("CountTasks")]
    public async Task<IActionResult> CountTasks()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT COUNT(*) FROM Tasks";
        int count = await con.ExecuteScalarAsync<int>(query);
        return Ok(count);
    }
    //counting daashboard to
    [HttpGet("UpcomingTasks")]
    public async Task<IActionResult> UpcomingTasks()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT COUNT(*) FROM Tasks WHERE sched_date > CURDATE()";
        int count = await con.ExecuteScalarAsync<int>(query);
        return Ok(count);
    }
    //counting daashboard to
    [HttpGet("DueTodayTasks")]
    public async Task<IActionResult> DueTodayTasks()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT COUNT(*) FROM Tasks WHERE sched_date = CURDATE()";
        int count = await con.ExecuteScalarAsync<int>(query);
        return Ok(count);
    }
    //counting daashboard to
    [HttpGet("OverDueTasks")]
    public async Task<IActionResult> OverDueTasks()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT COUNT(*) FROM Tasks WHERE sched_date < CURDATE()";
        int count = await con.ExecuteScalarAsync<int>(query);
        return Ok(count);
    }










































    //eto WALA NATO
    [HttpPut("UpdateTask/{scheduleId}")]
    public async Task<IActionResult> UpdateTask(int scheduleId, [FromBody] Tasksdto task)
    {
        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var username = HttpContext.Session.GetString("UserName");

                // Get Philippine time (UTC+8)
                DateTime philippineTime = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow,
                    TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time")
                );


                // Validate inputs
                if (task.ScheduleDate.Date < philippineTime.Date)
                {
                    return BadRequest("Cannot update a task with a past date. Please choose a future date.");
                }

                if (task.ScheduleNotify < 0)
                {
                    return BadRequest("Notification time cannot be negative.");
                }

                DateTime notificationDate = task.ScheduleDate.AddDays(-task.ScheduleNotify);
                if (notificationDate < philippineTime.Date)
                {
                    return BadRequest("The notification time cannot be set in the past. Please choose a valid time.");
                }

                if (task.ScheduleNotify > 30)
                {
                    return BadRequest("Notification time cannot exceed 30 days.");
                }

                // Check if task exists
                string checkQuery = "SELECT sched_Id FROM Tasks WHERE sched_Id = @ScheduleId";
                var existingTask = await connection.QueryFirstOrDefaultAsync<int>(checkQuery, new { ScheduleId = scheduleId });

                if (existingTask == 0)
                {
                    return NotFound($"Task with ID {scheduleId} not found");
                }

                // Get original task data for logging changes
                var originalTask = await connection.QueryFirstOrDefaultAsync<Tasksdto>(@"
            SELECT 
                sched_Id AS ScheduleId,
                sched_taskTitle AS ScheduleTaskTitle,
                sched_user AS ScheduleUser,
                sched_taskDescription AS ScheduleTaskDescription,
                sched_date AS ScheduleDate,
                sched_notify AS ScheduleNotify,
                sched_status AS ScheduleStatus,
                user_Id AS UserId
            FROM Tasks 
            WHERE sched_Id = @ScheduleId",
                    new { ScheduleId = scheduleId });

                if (originalTask == null)
                {
                    await Logger.LogAction(HttpContext,
                        action: "UPDATE_ERROR",
                        tableName: "Tasks",
                        recordId: scheduleId,
                        details: $"Original task {scheduleId} not found during update"
                    );
                    return NotFound($"Task {scheduleId} not found");
                }

                // Find user ID based on the provided username
                string userQuery = @"SELECT user_Id, user_Name FROM ManageUsers 
                               WHERE user_Name = @UserName";

                var userResult = await connection.QueryFirstOrDefaultAsync<dynamic>(
                    userQuery,
                    new { UserName = task.ScheduleUser }
                );

                if (userResult == null)
                {
                    return BadRequest("User not found. Please provide a valid username.");
                }

                int userId = userResult.user_Id;
                string userName = userResult.user_Name;

                // Check if a task with the same title, date, and user already exists (excluding the current task)
                string duplicateQuery = @"SELECT COUNT(*) FROM Tasks 
                                    WHERE sched_taskTitle = @TaskTitle 
                                    AND DATE(sched_date) = DATE(@Date)
                                    AND user_Id = @UserId
                                    AND sched_Id != @ScheduleId";

                var duplicateCount = await connection.ExecuteScalarAsync<int>(
                    duplicateQuery,
                    new
                    {
                        TaskTitle = task.ScheduleTaskTitle.Trim(),
                        Date = task.ScheduleDate,
                        UserId = userId,
                        ScheduleId = scheduleId
                    }
                );

                if (duplicateCount > 0)
                {
                    return Conflict($"A task with the title '{task.ScheduleTaskTitle}' already exists for this user on the selected date.");
                }

                // Convert boolean status to integer for database
                int statusValue = task.ScheduleStatus ? 1 : 0;

                // Updated query to include user_Id
                const string updateQuery = @"
            UPDATE Tasks 
            SET 
                sched_taskTitle = @ScheduleTaskTitle,
                sched_user = @ScheduleUser,
                sched_taskDescription = @ScheduleTaskDescription,
                sched_date = @ScheduleDate,
                sched_notify = @ScheduleNotify,
                sched_status = @ScheduleStatus,
                user_Id = @UserId
            WHERE sched_Id = @ScheduleId";

                await connection.ExecuteAsync(updateQuery, new
                {
                    task.ScheduleTaskTitle,
                    ScheduleUser = userName,  // Use the verified username
                    task.ScheduleTaskDescription,
                    task.ScheduleDate,
                    task.ScheduleNotify,
                    ScheduleStatus = statusValue,
                    UserId = userId,         // Add the user ID
                    ScheduleId = scheduleId
                });

                // Track changes for logging
                List<string> changes = new List<string>();

                if (!string.Equals(originalTask.ScheduleTaskTitle, task.ScheduleTaskTitle, StringComparison.Ordinal))
                {
                    changes.Add($"Task Title: \"{originalTask.ScheduleTaskTitle ?? "(empty)"}\" → \"{task.ScheduleTaskTitle ?? "(empty)"}\"");
                }

                if (!string.Equals(originalTask.ScheduleUser, userName, StringComparison.Ordinal))
                {
                    changes.Add($"User: \"{originalTask.ScheduleUser ?? "(empty)"}\" → \"{userName ?? "(empty)"}\"");
                }

                if (!string.Equals(originalTask.ScheduleTaskDescription, task.ScheduleTaskDescription, StringComparison.Ordinal))
                {
                    changes.Add($"Description: \"{originalTask.ScheduleTaskDescription ?? "(empty)"}\" → \"{task.ScheduleTaskDescription ?? "(empty)"}\"");
                }

                if (originalTask.ScheduleDate != task.ScheduleDate)
                {
                    changes.Add($"Date: \"{originalTask.ScheduleDate:g}\" → \"{task.ScheduleDate:g}\"");
                }

                if (originalTask.ScheduleNotify != task.ScheduleNotify)
                {
                    changes.Add($"Notification Time: \"{originalTask.ScheduleNotify}\" → \"{task.ScheduleNotify}\"");
                }

                if (originalTask.ScheduleStatus != task.ScheduleStatus)
                {
                    changes.Add($"Status: \"{(originalTask.ScheduleStatus ? "Finished" : "Pending")}\" → \"{(task.ScheduleStatus ? "Finished" : "Pending")}\"");
                }

                // Add user ID change tracking
                if (originalTask.UserId != userId)
                {
                    changes.Add($"User ID: \"{originalTask.UserId}\" → \"{userId}\"");
                }

                if (changes.Count > 0)
                {
                    await Logger.LogAction(HttpContext,
                        action: "UPDATE",
                        tableName: "Tasks",
                        recordId: scheduleId,
                        details: $"Updated task record (ID: {scheduleId}). Changes: {string.Join(", ", changes)}"
                    );
                }

                return Ok(new
                {
                    Message = $"Task entry with ID {scheduleId} updated successfully.",
                    Changes = changes,
                    UserId = userId,
                    UserName = userName
                });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Message = "An error occurred while updating the task.",
                ErrorDetails = ex.Message
            });
        }
    }


    // eto yung reset password pag nagrequest yung client

    [HttpPut("ResetPassword/{userId}")]
    public async Task<IActionResult> ResetPassword(int userId)
    {
        // Get user role from session
        var userRole = HttpContext.Session.GetString("UserRole")?.ToLowerInvariant();

        if (string.IsNullOrEmpty(userRole) || (userRole != "admin" && userRole != "chiefadmin"))
        {
            return StatusCode(403, "Only administrators can reset passwords.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();
        var username = HttpContext.Session.GetString("UserName");
        // Verify user exists
        var userExists = await con.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM ManageUsers WHERE user_Id = @UserId",
            new { UserId = userId });

        if (userExists == 0)
        {
            return NotFound("User not found.");
        }

        // Set default password "123"
        string defaultPassword = "123";
        string hashedPassword = PasswordHasher.HashPassword(defaultPassword);

        // Update password
        int rowsAffected = await con.ExecuteAsync(
            "UPDATE ManageUsers SET user_Pass = @Password WHERE user_Id = @UserId",
            new { Password = hashedPassword, UserId = userId });

        if (rowsAffected > 0)
        {
            // Log the reset action
            await Logger.LogAction(HttpContext,
                action: "PasswordReset",
                tableName: "ManageUsers",
                recordId: userId,
                details: $"Password reset to default by admin with role '{userRole}'.");

            return Ok("Password has been reset to the default.");
        }

        return BadRequest("Failed to reset password.");
    }


    //ETO YUNG DELETE TASK
    [HttpDelete("DeleteTasks/{id}")]
        public async Task<IActionResult> DeleteTasks(int id, [FromHeader(Name = "UserName")] string userName = "System")
        {
            if (id <= 0)
            {
                return BadRequest("Invalid task ID.");
            }

            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            var username = HttpContext.Session.GetString("UserName");
        try
            {
                // Fetch task details before deletion
                var existingTask = await connection.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT sched_taskTitle, sched_taskDescription, sched_date, sched_status FROM Tasks WHERE sched_Id = @ScheduleId",
                    new { ScheduleId = id });

                if (existingTask == null)
                {
                    return NotFound($"No task found with ID {id}.");
                }

                // Extract task details safely
                string taskTitle = existingTask?.sched_taskTitle ?? "N/A";
                string taskDescription = existingTask?.sched_taskDescription ?? "N/A";
                string taskDate = existingTask?.sched_date != null ? ((DateTime)existingTask.sched_date).ToString("yyyy-MM-dd") : "N/A";
                string taskStatus = existingTask?.sched_status ?? "N/A";

                // Delete task
                string deleteQuery = "DELETE FROM Tasks WHERE sched_Id = @ScheduleId";
                int rowsAffected = await connection.ExecuteAsync(deleteQuery, new { ScheduleId = id });

                if (rowsAffected > 0)
                {
                    Console.WriteLine($"Task ID {id} deleted successfully by {userName}.");

                    // Log deletion details similarly to update
                    List<string> changes = new List<string>
            {
                $"Title: \"{taskTitle}\"",
                $"Description: \"{taskDescription}\"",
                $"Date: \"{taskDate}\"",
                $"Status: \"{taskStatus}\""
            };

                    string logMessage = $"Deleted task record (ID: {id})";
                    string details = string.Join(", ", changes);
                    await Logger.LogAction(HttpContext, logMessage, "Tasks", id, details);

                    return Ok(new { Message = $"Task with ID {id} deleted successfully." });
                }

                return StatusCode(500, "An error occurred while deleting the task.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting task entry: {ex.Message}");
                return StatusCode(500, new { Message = "Error deleting task entry.", ErrorDetails = ex.Message });
            }
        }


        //WALA NATO
        [HttpGet("GetTasks")]
        public async Task<IActionResult> GetTasks()
        {
            try
            {
                using var con = new MySqlConnection(_connectionString);
                await con.OpenAsync();

                string query = @"
        SELECT 
            sched_taskTitle,
            sched_user,
            sched_taskDescription, 
            sched_date,  
            sched_status,
            sched_notify
        FROM Tasks";

                var tasks = await con.QueryAsync<taskDoubleClickDashboard>(@"
        SELECT 
            sched_taskTitle AS ScheduleTaskTitle,
            sched_user AS ScheduleUser,
            sched_taskDescription AS ScheduleTaskDescription, 
            sched_date AS ScheduleDate, 
            sched_status AS ScheduleStatus, 
            sched_notify AS ScheduleNotify
        FROM Tasks");

                return Ok(tasks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving tasks: {ex.Message}");
                return StatusCode(500, $"An error occurred while retrieving tasks: {ex.Message}");
            }
        }
   
    //Wala na to
    [HttpGet("GetTasksDashboard")]
    public async Task<IActionResult> GetTasksDashboard()
    {
        try
        {
            using var con = new MySqlConnection(_connectionString);
            await con.OpenAsync();

            string query = @"
        SELECT 
            sched_taskTitle,
            sched_user,
            sched_taskDescription, 
            sched_date, 
            sched_status 
        FROM Tasks";

            var tasks = await con.QueryAsync<taskDashboard>(@"
        SELECT 
            sched_taskTitle AS ScheduleTaskTitle,
            sched_user AS ScheduleUser,
            sched_taskDescription AS ScheduleTaskDescription, 
            sched_date AS ScheduleDate, 
            sched_status AS ScheduleStatus 
        FROM Tasks");

            return Ok(tasks);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving tasks: {ex.Message}");
            return StatusCode(500, $"An error occurred while retrieving tasks: {ex.Message}");
        }
    }


   

        //Combobox get users
        [HttpGet("GetUsers")]
        public async Task<IActionResult> GetUsers()
        {
            using var con = new MySqlConnection(_connectionString);
            await con.OpenAsync();

            string query = "SELECT user_Fname, user_Lname FROM ManageUsers";
            using var cmd = new MySqlCommand(query, con);
            using var reader = await cmd.ExecuteReaderAsync();

            var users = new List<FullnameDto>();
            while (await reader.ReadAsync())
            {
                users.Add(new FullnameDto
                {
                    Name = $"{reader["user_Fname"]?.ToString()} {reader["user_Lname"]?.ToString()}".Trim()
                });
            }

            return Ok(users);
        }

    //ETO SA PRINTING NG EXCEL
    [HttpGet("export-tasks")]
    public async Task<IActionResult> ExportTasksReport()
    {
        try
        {
            string query = @"
    SELECT 
        sched_taskTitle AS ScheduleTaskTitle,
        sched_date AS ScheduleDate,
        sched_taskDescription AS ScheduleTaskDescription,
        sched_status AS ScheduleStatus,
        sched_checkbox AS ScheduleCheckbox
    FROM Tasks
    ORDER BY sched_date DESC";

            using var connection = new MySqlConnection(_connectionString);
            var taskData = (await connection.QueryAsync<TaskDisplay>(query)).ToList();
            Console.WriteLine($"Retrieved {taskData.Count} task records.");

            // Check if taskData is empty and return an appropriate response
            if (!taskData.Any())
            {
                return BadRequest("Cannot export report. No task records found in the database.");
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Task Schedule");

            worksheet.Cell("A1").Value = "TASK SCHEDULE REPORT";
            worksheet.Range("A1:E1").Merge().Style.Font.SetBold(true).Font.SetFontSize(14).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            worksheet.Cell("A2").Value = $"Date Exported: {DateTime.Now:MM/dd/yyyy hh:mm tt}";
            worksheet.Range("A2:E2").Merge().Style.Font.SetItalic(true).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            var headers = new[] { "Task Title", "Task Date", "Description", "Status", "Completed" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(4, i + 1).Value = headers[i];
                worksheet.Cell(4, i + 1).Style.Font.SetBold(true).Fill.SetBackgroundColor(XLColor.LightGray).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }

            int row = 5;
            foreach (var record in taskData)
            {
                worksheet.Cell(row, 1).Value = record.ScheduleTaskTitle ?? "N/A";
                worksheet.Cell(row, 2).Value = record.ScheduleDate.ToString("yyyy-MM-dd HH:mm:ss");
                worksheet.Cell(row, 3).Value = record.ScheduleTaskDescription ?? "N/A";
                worksheet.Cell(row, 4).Value = record.ScheduleStatus ?? "N/A";
                worksheet.Cell(row, 5).Value = record.ScheduleCheckbox ? "Yes" : "No";
                row++;
            }

            worksheet.Columns().AdjustToContents();
            for (int i = 1; i <= headers.Length; i++)
            {
                if (worksheet.Column(i).Width < 15)
                    worksheet.Column(i).Width = 15;
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Task_Report_{DateTime.Now:yyyyMMdd}.xlsx");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting task data: {ex.Message}");
            return StatusCode(500, $"Error exporting task data: {ex.Message}");
        }
    }

    //ETO NAMAN SA PDF
    [HttpGet("export-tasks-pdf")]
    public async Task<IActionResult> ExportTasksPdf()
    {
        try
        {
            string query = @"
    SELECT 
        sched_taskTitle AS ScheduleTaskTitle,
        sched_date AS ScheduleDate,
        sched_taskDescription AS ScheduleTaskDescription,
        sched_status AS ScheduleStatus,
        sched_checkbox AS ScheduleCheckbox
    FROM Tasks
    ORDER BY sched_date DESC";

            using var connection = new MySqlConnection(_connectionString);
            var taskData = (await connection.QueryAsync<TaskDisplay>(query)).ToList();
            Console.WriteLine($"Retrieved {taskData.Count} task records.");

            // Check if taskData is empty and return an appropriate response
            if (!taskData.Any())
            {
                return BadRequest("Cannot export report. No task records found in the database.");
            }

            using var memoryStream = new MemoryStream();
            using (var writer = new PdfWriter(memoryStream))
            using (var pdf = new PdfDocument(writer))
            {
                var document = new Document(pdf);

                PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
                PdfFont boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                PdfFont italicFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_OBLIQUE);

                var title = new Paragraph("Task Schedule Report")
                    .SetFont(boldFont)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontSize(16);
                document.Add(title);

                var subtitle = new Paragraph($"Exported on {DateTime.Now:MMMM dd, yyyy hh:mm tt}")
                    .SetFont(italicFont)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontSize(10);
                document.Add(subtitle);

                document.Add(new Paragraph("\n"));

                // Use the fully qualified name to resolve ambiguity
                var table = new iText.Layout.Element.Table(5, true); // 5 columns
                string[] headers = { "Task Title", "Task Date", "Description", "Status", "Completed" };
                foreach (var header in headers)
                {
                    table.AddHeaderCell(new Cell().Add(new Paragraph(header).SetFont(boldFont)));
                }

                foreach (var record in taskData)
                {
                    table.AddCell(new Cell().Add(new Paragraph(record.ScheduleTaskTitle ?? "N/A").SetFont(font)));
                    table.AddCell(new Cell().Add(new Paragraph(record.ScheduleDate.ToString("yyyy-MM-dd HH:mm:ss")).SetFont(font)));
                    table.AddCell(new Cell().Add(new Paragraph(record.ScheduleTaskDescription ?? "N/A").SetFont(font)));
                    table.AddCell(new Cell().Add(new Paragraph(record.ScheduleStatus ?? "N/A").SetFont(font)));
                    table.AddCell(new Cell().Add(new Paragraph(record.ScheduleCheckbox ? "Yes" : "No").SetFont(font)));
                }

                document.Add(table);
                document.Close();
            }

            return File(memoryStream.ToArray(), "application/pdf", $"Task_Report_{DateTime.Now:yyyyMMdd}.pdf");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PDF Export Error: {ex.Message}");
            return StatusCode(500, "An error occurred while generating the PDF.");
        }
    }
}

