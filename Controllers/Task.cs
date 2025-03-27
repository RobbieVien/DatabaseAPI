using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Dapper;
using System.Collections.Generic;
using DatabaseAPI.Models;
using DatabaseAPI.Utilities;

[Route("api/[controller]")]
[ApiController]
public class TaskController : ControllerBase
{
    private readonly string _connectionString;

    public TaskController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }

    //Add Button
    [HttpPost("AddTasks")]
    public async Task<IActionResult> AddTasks([FromBody] Tasksdto tasks)
    {
        if (tasks == null || string.IsNullOrWhiteSpace(tasks.ScheduleTaskTitle))
        {
            return BadRequest("Invalid Task data.");
        }

        // Get Philippine time (UTC+8)
        DateTime philippineTime = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time")
        );

        using (var con = new MySqlConnection(_connectionString))
        {
            await con.OpenAsync(); // Ensure connection is opened before executing queries.

            try
            {
                // Check for existing task
                string checkQuery = @"SELECT COUNT(*) FROM Tasks 
                                  WHERE sched_taskTitle = @TaskTitle 
                                  AND DATE(sched_date) = DATE(@Date)";

                var existingCount = await con.ExecuteScalarAsync<int>(
                    checkQuery,
                    new
                    {
                        TaskTitle = tasks.ScheduleTaskTitle.Trim(),
                        Date = tasks.ScheduleDate // Use manually inputted date
                    }
                );

                if (existingCount > 0)
                {
                    return Conflict("A task with the same title and date already exists.");
                }

                // Insert new task
                string insertQuery = @"INSERT INTO Tasks (
                sched_taskTitle,
                sched_user,
                sched_taskDescription, 
                sched_date, 
                sched_inputted, 
                sched_status
            ) VALUES (
                @TaskTitle,
                @TaskUser,
                @TaskDescription, 
                @Date, 
                @InputtedTime, 
                @Status
            )";

                int rowsAffected = await con.ExecuteAsync(
                    insertQuery,
                    new
                    {
                        TaskTitle = tasks.ScheduleTaskTitle.Trim(),
                        TaskUser = tasks.ScheduleUser,
                        TaskDescription = tasks.ScheduleTaskDescription,
                        Date = tasks.ScheduleDate, // Keep manually inputted date
                        InputtedTime = philippineTime, // Always use Philippine time for inputted time
                        Status = tasks.ScheduleStatus ? 1 : 0 // Convert bool to bit
                    }
                );

                if (rowsAffected == 0)
                {
                    return StatusCode(500, "Task insertion failed.");
                }

                // Log the action
                await Logger.LogAction(
                    action: "INSERT",
                    tableName: "Tasks",
                    recordId: 0, // Assuming no auto-generated ID return
                    userName: "Admin",
                    details: $"Task '{tasks.ScheduleTaskTitle}' added successfully."
                );

                return Ok("Task added successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }




    [HttpPut("UpdateTask/{scheduleId}")]
    public async Task<IActionResult> UpdateTask(int scheduleId, [FromBody] Tasksdto task)
    {
        const string updateQuery = @"
        UPDATE Tasks 
        SET 
            sched_taskTitle = @ScheduleTaskTitle,
            sched_user = @ScheduleUser,
            sched_taskDescription = @ScheduleTaskDescription,
            sched_date = @ScheduleDate,
            sched_status = @ScheduleStatus
        WHERE sched_Id = @ScheduleId";

        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                // Check if task exists
                var checkQuery = "SELECT sched_Id FROM Tasks WHERE sched_Id = @ScheduleId";
                var existingTask = await connection.QueryFirstOrDefaultAsync<int>(checkQuery, new { ScheduleId = scheduleId });

                if (existingTask == 0)
                {
                    return NotFound($"Task with ID {scheduleId} not found");
                }

                // Get current values with proper column mapping
                var originalTask = await connection.QueryFirstOrDefaultAsync<Tasksdto>(
                    @"SELECT 
                    sched_Id AS ScheduleId,
                    sched_taskTitle AS ScheduleTaskTitle,
                    sched_user AS ScheduleUser,
                    sched_taskDescription AS ScheduleTaskDescription,
                    sched_date AS ScheduleDate,
                    sched_status AS ScheduleStatus
                FROM Tasks 
                WHERE sched_Id = @ScheduleId",
                    new { ScheduleId = scheduleId });

                if (originalTask == null)
                {
                    await Logger.LogAction(
                        action: "UPDATE_ERROR",
                        tableName: "Tasks",
                        recordId: scheduleId,
                        userName: User.Identity?.Name ?? "System",
                        details: $"Original task {scheduleId} not found during update"
                    );
                    return NotFound($"Task {scheduleId} not found");
                }

                // Validate date range with buffer
                var utcNow = DateTime.UtcNow;
                var taskDateUtc = task.ScheduleDate.ToUniversalTime();
                var cutoffTime = utcNow.AddMinutes(-5);

                if (taskDateUtc < cutoffTime)
                {
                    return BadRequest(new
                    {
                        Error = "Schedule date too far in the past",
                        CurrentTime = utcNow,
                        ProvidedDate = taskDateUtc,
                        AllowedBufferMinutes = 5,
                        Message = "Dates within last 5 minutes are allowed"
                    });
                }

                // Update task first
                await connection.ExecuteAsync(updateQuery, new
                {
                    task.ScheduleTaskTitle,
                    task.ScheduleUser,
                    task.ScheduleTaskDescription,
                    task.ScheduleDate,
                    task.ScheduleStatus,
                    ScheduleId = scheduleId
                });

                // Track changes after update
                var changes = new List<string>();

                // Title comparison with null handling
                if (originalTask.ScheduleTaskTitle != task.ScheduleTaskTitle)
                {
                    changes.Add($"Title changed from '{originalTask.ScheduleTaskTitle ?? "(empty)"}' to '{task.ScheduleTaskTitle ?? "(empty)"}'");
                }

                if (originalTask.ScheduleUser != task.ScheduleUser)
                {
                    changes.Add($"Title changed from '{originalTask.ScheduleUser ?? "(empty)"}' to '{task.ScheduleUser ?? "(empty)"}'");
                }

                // Description comparison
                if (originalTask.ScheduleTaskDescription != task.ScheduleTaskDescription)
                {
                    changes.Add($"Description changed from '{originalTask.ScheduleTaskDescription ?? "(empty)"}' to '{task.ScheduleTaskDescription ?? "(empty)"}'");
                }

                // Date comparison
                if (originalTask.ScheduleDate != task.ScheduleDate)
                {
                    changes.Add($"Date changed from '{originalTask.ScheduleDate.ToString("g")}' to '{task.ScheduleDate.ToString("g")}'");
                }

                // Status comparison
                if (originalTask.ScheduleStatus != task.ScheduleStatus)
                {
                    changes.Add($"Status changed from '{originalTask.ScheduleStatus}' to '{task.ScheduleStatus}'");
                }

                // Log changes
                if (changes.Count > 0)
                {
                    await Logger.LogAction(
                        action: "UPDATE",
                        tableName: "Tasks",
                        recordId: scheduleId,
                        userName: User.Identity?.Name ?? "System",
                        details: $"Updated task {scheduleId}: {string.Join(", ", changes)}"
                    );
                }
                else
                {
                    await Logger.LogAction(
                        action: "UPDATE_NO_CHANGES",
                        tableName: "Tasks",
                        recordId: scheduleId,
                        userName: User.Identity?.Name ?? "System",
                        details: $"No changes detected for task {scheduleId}"
                    );
                }

                return Ok("Task updated successfully");
            }
        }
        catch (Exception ex)
        {
            await Logger.LogAction(
                action: "UPDATE_ERROR",
                tableName: "Tasks",
                recordId: scheduleId,
                userName: User.Identity?.Name ?? "System",
                details: $"Error: {ex.Message} | Stack: {ex.StackTrace?.Substring(0, 200)}"
            );

            return StatusCode(500, new
            {
                Error = "Update failed",
                Message = ex.Message,
                Reference = $"Error-{Guid.NewGuid()}"
            });
        }
    }







    //button
    [HttpDelete("DeleteTasks/{id}")]
    public async Task<IActionResult> DeleteTasks(int id, [FromHeader(Name = "UserName")] string userName = "System")
    {
        if (id <= 0)
        {
            return BadRequest("Invalid task ID.");
        }

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

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
                await Logger.LogAction(logMessage, "Tasks", id, userName, details);

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


    //for Datagridview
    [HttpGet("GetTasks")]
    public async Task<IActionResult> GetTasks()
    {
        try
        {
            using var con = new MySqlConnection(_connectionString);
            await con.OpenAsync();

            string query = @"
        SELECT 
            sched_Id, 
            sched_taskTitle,
            sched_user,
            sched_taskDescription, 
            sched_date, 
            sched_inputted, 
            sched_status 
        FROM Tasks";

            var tasks = await con.QueryAsync<Tasksdto>(@"
        SELECT 
            sched_Id AS ScheduleId, 
            sched_taskTitle AS ScheduleTaskTitle,
            sched_user AS ScheduleUser,
            sched_taskDescription AS ScheduleTaskDescription, 
            sched_date AS ScheduleDate, 
            sched_inputted AS ScheduleInputted, 
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



    [HttpGet("CountTasks")]
    public async Task<IActionResult> CountTasks()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT COUNT(*) FROM Tasks";
        int count = await con.ExecuteScalarAsync<int>(query);
        return Ok(count);
    }

    [HttpGet("UpcomingTasks")]
    public async Task<IActionResult> UpcomingTasks()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT COUNT(*) FROM Tasks WHERE sched_date > CURDATE()";
        int count = await con.ExecuteScalarAsync<int>(query);
        return Ok(count);
    }

    [HttpGet("DueTodayTasks")]
    public async Task<IActionResult> DueTodayTasks()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT COUNT(*) FROM Tasks WHERE sched_date = CURDATE()";
        int count = await con.ExecuteScalarAsync<int>(query);
        return Ok(count);
    }

    [HttpGet("OverDueTasks")]
    public async Task<IActionResult> OverDueTasks()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT COUNT(*) FROM Tasks WHERE sched_date < CURDATE()";
        int count = await con.ExecuteScalarAsync<int>(query);
        return Ok(count);
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

}
