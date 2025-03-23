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

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        try
        {
            Console.WriteLine($"Incoming Task: Title={tasks.ScheduleTaskTitle}, Date={tasks.ScheduleDate:yyyy-MM-dd}, Time={tasks.ScheduleDate:HH:mm:ss}");

            // Direct check for task with same title AND date - simplified query
            string checkQuery = @"SELECT COUNT(*) FROM Tasks 
                          WHERE sched_taskTitle = @TaskTitle 
                          AND DATE(sched_date) = DATE(@Date)";

            using var checkCmd = new MySqlCommand(checkQuery, con);
            checkCmd.Parameters.AddWithValue("@TaskTitle", tasks.ScheduleTaskTitle.Trim());
            checkCmd.Parameters.AddWithValue("@Date", tasks.ScheduleDate);

            var existingCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
            Console.WriteLine($"Existing Task Count for same title and date: {existingCount}");

            if (existingCount > 0)
            {
                return Conflict("A task with the same title and date already exists.");
            }

            // Insert new task
            string insertQuery = @"INSERT INTO Tasks (sched_taskTitle, sched_taskDescription, sched_date, sched_status)
                        VALUES (@TaskTitle, @TaskDescription, @Date, @Status)";

            using var insertCmd = new MySqlCommand(insertQuery, con);
            insertCmd.Parameters.AddWithValue("@TaskTitle", tasks.ScheduleTaskTitle.Trim());
            insertCmd.Parameters.AddWithValue("@TaskDescription", tasks.ScheduleTaskDescription);
            insertCmd.Parameters.AddWithValue("@Date", tasks.ScheduleDate);
            insertCmd.Parameters.AddWithValue("@Status", tasks.ScheduleStatus);

            await insertCmd.ExecuteNonQueryAsync();

            // Log the action
            await Logger.LogAction($"Task {tasks.ScheduleTaskTitle} has been added.", "Tasks", 0, "Admin");

            return Ok("Task added successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return StatusCode(500, "An error occurred while adding the task.");
        }
    }



    [HttpPut("UpdateTask/{scheduleId}")]
    public async Task<IActionResult> UpdateTask(int scheduleId, [FromBody] Tasksdto task)
    {
        const string updateQuery = @"
    UPDATE Tasks 
    SET 
        sched_taskTitle = @ScheduleTaskTitle,
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

                // Get current values for change tracking
                var originalTask = await connection.QueryFirstOrDefaultAsync<Tasksdto>(
                    "SELECT * FROM Tasks WHERE sched_Id = @ScheduleId",
                    new { ScheduleId = scheduleId });

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

                // Update task
                var affectedRows = await connection.ExecuteAsync(updateQuery, new
                {
                    ScheduleId = scheduleId,
                    task.ScheduleTaskTitle,
                    task.ScheduleTaskDescription,
                    ScheduleDate = taskDateUtc,
                    task.ScheduleStatus
                });

                if (affectedRows == 0)
                {
                    return StatusCode(500, "Update failed unexpectedly");
                }

                // Build detailed change log
                var changes = new List<string>();
                if (originalTask.ScheduleTaskTitle != task.ScheduleTaskTitle)
                    changes.Add($"Title changed from '{originalTask.ScheduleTaskTitle}' to '{task.ScheduleTaskTitle}'");
                if (originalTask.ScheduleTaskDescription != task.ScheduleTaskDescription)
                    changes.Add($"Description changed from '{originalTask.ScheduleTaskDescription}' to '{task.ScheduleTaskDescription}'");
                if (originalTask.ScheduleDate != task.ScheduleDate)
                    changes.Add($"Date changed from '{originalTask.ScheduleDate}' to '{task.ScheduleDate}'");
                if (originalTask.ScheduleStatus != task.ScheduleStatus)
                    changes.Add($"Status changed from '{originalTask.ScheduleStatus}' to '{task.ScheduleStatus}'");

                // Log detailed changes
                await Logger.LogAction(
                    action: "UPDATE",
                    tableName: "Tasks",
                    recordId: scheduleId,
                    userName: User.Identity?.Name ?? "System",
                    details: $"Updated task {scheduleId} with changes: {string.Join(", ", changes)}"
                );

                return Ok("Task updated successfully");
            }
        }
        catch (Exception ex)
        {
            // Log error with technical details
            await Logger.LogAction(
                action: "UPDATE_ERROR",
                tableName: "Tasks",
                recordId: scheduleId,
                userName: User.Identity?.Name ?? "System",
                details: $"Error updating task: {ex.Message} | StackTrace: {ex.StackTrace}"
            );

            return StatusCode(500, $"An error occurred: {ex.Message}");
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
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT sched_Id, sched_taskTitle, sched_taskDescription, sched_date, sched_status FROM Tasks";
        using var cmd = new MySqlCommand(query, con);

        using var reader = await cmd.ExecuteReaderAsync();

        var categories = new List<Tasksdto>();
        while (await reader.ReadAsync())
        {
            categories.Add(new Tasksdto
            {
                ScheduleId = Convert.ToInt32(reader["sched_Id"]), // Make sure user_Id is included
                ScheduleTaskTitle = reader["sched_taskTitle"]?.ToString(),
                ScheduleTaskDescription = reader["sched_taskDescription"]?.ToString(),
                ScheduleDate = reader["sched_date"] == DBNull.Value ? default : Convert.ToDateTime(reader["sched_date"]),
                ScheduleStatus = reader["sched_status"]?.ToString()
            });
        }

        return Ok(categories);
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
}
