﻿using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Dapper;
using System.Collections.Generic;
using DatabaseAPI.Models;
using DatabaseAPI.Utilities;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Office2010.Excel;

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
                // Check for existing task
                string checkQuery = @"SELECT COUNT(*) FROM Tasks 
                        WHERE sched_taskTitle = @TaskTitle 
                        AND DATE(sched_date) = DATE(@Date)";
                var existingCount = await con.ExecuteScalarAsync<int>(
                    checkQuery,
                    new
                    {
                        TaskTitle = tasks.ScheduleTaskTitle.Trim(),
                        Date = tasks.ScheduleDate
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
                sched_status,
                sched_notify
            ) VALUES (
                @TaskTitle,
                @TaskUser,
                @TaskDescription, 
                @Date, 
                @InputtedTime, 
                @Status,
                @Notify
            )";

                // Ensure that tasks are created as "Pending" (status 0) by default
                const int defaultStatusValue = 0; // Always create tasks as pending

                int rowsAffected = await con.ExecuteAsync(
                    insertQuery,
                    new
                    {
                        TaskTitle = tasks.ScheduleTaskTitle.Trim(),
                        TaskUser = tasks.ScheduleUser,
                        TaskDescription = tasks.ScheduleTaskDescription,
                        Date = tasks.ScheduleDate,
                        InputtedTime = philippineTime,
                        Status = defaultStatusValue,  // Force status to be 0
                        Notify = tasks.ScheduleNotify
                    }
                );

                if (rowsAffected == 0)
                {
                    return StatusCode(500, "Task insertion failed.");
                }

                // Log the action
                await Logger.LogAction(HttpContext,
                    action: "INSERT",
                    tableName: "Tasks",
                    recordId: 0,
                    details: $"Task '{tasks.ScheduleTaskTitle}' added successfully with Status: {defaultStatusValue}."
                );

                return Ok(new { message = "Task added successfully." });
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
    sched_notify = @ScheduleNotify,
    sched_status = @ScheduleStatus
WHERE sched_Id = @ScheduleId";

        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                DateTime philippineTime = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow,
                    TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time")
                );

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

                var checkQuery = "SELECT sched_Id FROM Tasks WHERE sched_Id = @ScheduleId";
                var existingTask = await connection.QueryFirstOrDefaultAsync<int>(checkQuery, new { ScheduleId = scheduleId });

                if (existingTask == 0)
                {
                    return NotFound($"Task with ID {scheduleId} not found");
                }

                var originalTask = await connection.QueryFirstOrDefaultAsync<Tasksdto>(@"
                SELECT 
                    sched_Id AS ScheduleId,
                    sched_taskTitle AS ScheduleTaskTitle,
                    sched_user AS ScheduleUser,
                    sched_taskDescription AS ScheduleTaskDescription,
                    sched_date AS ScheduleDate,
                    sched_notify AS ScheduleNotify,
                    sched_status AS ScheduleStatus
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

                int statusValue = task.ScheduleStatus ? 1 : 0;

                await connection.ExecuteAsync(updateQuery, new
                {
                    task.ScheduleTaskTitle,
                    task.ScheduleUser,
                    task.ScheduleTaskDescription,
                    task.ScheduleDate,
                    task.ScheduleNotify,
                    ScheduleStatus = statusValue, // ✅ Fixed name
                    ScheduleId = scheduleId
                });

                List<string> changes = new List<string>();

                if (!string.Equals(originalTask.ScheduleTaskTitle, task.ScheduleTaskTitle, StringComparison.Ordinal))
                {
                    changes.Add($"Task Title: \"{originalTask.ScheduleTaskTitle ?? "(empty)"}\" → \"{task.ScheduleTaskTitle ?? "(empty)"}\"");
                }

                if (!string.Equals(originalTask.ScheduleUser, task.ScheduleUser, StringComparison.Ordinal))
                {
                    changes.Add($"User: \"{originalTask.ScheduleUser ?? "(empty)"}\" → \"{task.ScheduleUser ?? "(empty)"}\"");
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

                if (changes.Count > 0)
                {
                    await Logger.LogAction(HttpContext,
                        action: "UPDATE",
                        tableName: "Tasks",
                        recordId: scheduleId,
                        details: $"Updated task record (ID: {scheduleId}). Changes: {string.Join(", ", changes)}"
                    );
                }

                return Ok(new { Message = $"Task entry with ID {scheduleId} updated successfully.", Changes = changes });
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


        [HttpGet("export-tasks")]
        public async Task<IActionResult> ExportTasksReport()
        {
            try
            {
                string query = @"
                SELECT 
                    sched_id AS ScheduleId,
                    sched_taskTitle AS ScheduleTaskTitle,
                    sched_user AS ScheduleUser,
                    sched_taskDescription AS ScheduleTaskDescription,
                    DATE_FORMAT(sched_date, '%Y-%m-%d %H:%i:%s') AS ScheduleDate,
                    DATE_FORMAT(sched_inputted, '%Y-%m-%d %H:%i:%s') AS ScheduleInputted,
                    sched_status AS ScheduleStatus,
                    CASE 
                        WHEN sched_date >= CONVERT_TZ(NOW(), 'UTC', 'Asia/Manila') THEN 0
                        ELSE 1
                    END AS is_past
                FROM Tasks
                ORDER BY is_past ASC, 
                         CASE WHEN is_past = 0 THEN sched_date END ASC,
                         CASE WHEN is_past = 1 THEN sched_date END DESC,
                         ScheduleInputted DESC";

                using var connection = new MySqlConnection(_connectionString);
                var taskData = (await connection.QueryAsync<Tasksdto>(query)).ToList();

                Console.WriteLine($"Retrieved {taskData.Count} task records.");

                if (!taskData.Any())
                {
                    return NotFound("No task records found.");
                }

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Task Schedule");
                worksheet.Cell("A1").Value = "TASK SCHEDULE REPORT";
                worksheet.Range("A1:F1").Merge().Style.Font.SetBold(true).Font.SetFontSize(14).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                worksheet.Cell("A2").Value = $"Date Exported: {DateTime.Now:MM/dd/yyyy hh:mm tt}";
                worksheet.Range("A2:F2").Merge().Style.Font.SetItalic(true).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                var headers = new[] { "Task Title", "User", "Description", "Task Date", "Date Inputted", "Status" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(4, i + 1).Value = headers[i];
                    worksheet.Cell(4, i + 1).Style.Font.SetBold(true).Fill.SetBackgroundColor(XLColor.LightGray).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                }

                int row = 5;
                foreach (var record in taskData)
                {
                    worksheet.Cell(row, 1).Value = record.ScheduleTaskTitle ?? "N/A";
                    worksheet.Cell(row, 2).Value = record.ScheduleUser ?? "N/A";
                    worksheet.Cell(row, 3).Value = record.ScheduleTaskDescription ?? "N/A";
                    worksheet.Cell(row, 4).Value = record.ScheduleDate != null ? record.ScheduleDate.ToString("yyyy-MM-dd HH:mm:ss") : "N/A";
                    worksheet.Cell(row, 5).Value = record.ScheduleInputted.ToString("yyyy-MM-dd HH:mm:ss");
                    worksheet.Cell(row, 6).Value = record.ScheduleStatus ? "Completed" : "Pending";
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


    }

