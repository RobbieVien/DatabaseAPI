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
public class TaskUserSideController : ControllerBase
{
    private readonly string _connectionString;

    public TaskUserSideController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }


    //for Datagridview
    [HttpGet("GetTasks")]
    public async Task<IActionResult> GetTasks()
    {
        try
        {
            var username = HttpContext.Session.GetString("UserName");

            if (string.IsNullOrEmpty(username))
                return Unauthorized("User is not logged in.");

            using var con = new MySqlConnection(_connectionString);
            await con.OpenAsync();

            var query = @"
        SELECT 
            sched_taskTitle AS ScheduleTaskTitle,
            sched_user AS ScheduleUser,
            sched_taskDescription AS ScheduleTaskDescription, 
            sched_date AS ScheduleDate, 
            sched_status AS ScheduleStatus 
        FROM Tasks
        WHERE sched_user = @UserName AND sched_status = 0"; // pending only

            var tasks = await con.QueryAsync<taskDashboard>(query, new { UserName = username });

            return Ok(tasks);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving tasks: {ex.Message}");
            return StatusCode(500, $"An error occurred while retrieving tasks: {ex.Message}");
        }
    }

    //eto pag dinouble click na
    [HttpGet("UserDoubleClickGetTasks")]
    public async Task<IActionResult> UserDoubleClickGetTasks()
    {
        try
        {
            var username = HttpContext.Session.GetString("UserName");
            if (string.IsNullOrEmpty(username))
                return Unauthorized("User is not logged in.");

            using var con = new MySqlConnection(_connectionString);
            await con.OpenAsync();

            var query = @"
    SELECT 
        sched_taskTitle AS ScheduleTaskTitle,
        sched_user AS ScheduleUser,
        sched_taskDescription AS ScheduleTaskDescription, 
        sched_date AS ScheduleDate, 
        sched_status AS ScheduleStatus,
        sched_notify AS ScheduleNotify
    FROM Tasks
    WHERE sched_user = @UserName AND sched_status = 0"; // pending only

            var tasks = await con.QueryAsync<taskDoubleClickDashboard>(query, new { UserName = username });
            return Ok(tasks);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving tasks: {ex.Message}");
            return StatusCode(500, $"An error occurred while retrieving tasks: {ex.Message}");
        }
    }


    [HttpPut("UserUpdateTask/{scheduleId}")]
    public async Task<IActionResult> UserUpdateTask(int scheduleId, [FromBody] UserTaskUpdateDto task)
    {
        try
        {
            // Get username from session
            var username = HttpContext.Session.GetString("UserName");
            if (string.IsNullOrEmpty(username))

                return Unauthorized("User is not logged in.");

            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Get Philippine time (UTC+8)
                DateTime philippineTime = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow,
                    TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time")
                );

                // Get original task data to verify ownership and for retrieving original values
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
                        details: $"Task {scheduleId} not found during update"
                    );
                    return NotFound($"Task with ID {scheduleId} not found");
                }

                // Verify that the current user owns this task
                if (originalTask.ScheduleUser != username)
                {
                    await Logger.LogAction(HttpContext,
                        action: "UNAUTHORIZED_UPDATE_ATTEMPT",
                        tableName: "Tasks",
                        recordId: scheduleId,
                        details: $"User {username} attempted to update task belonging to {originalTask.ScheduleUser}"
                    );
                    return Unauthorized("You can only update your own tasks.");
                }

                // Validate notification time
                if (task.ScheduleNotify < 0)
                {
                    return BadRequest("Notification time cannot be negative.");
                }

                if (task.ScheduleNotify > 30)
                {
                    return BadRequest("Notification time cannot exceed 30 days.");
                }

                // Calculate notification date based on the original schedule date
                DateTime notificationDate = originalTask.ScheduleDate.AddDays(-task.ScheduleNotify);
                if (notificationDate < philippineTime.Date)
                {
                    return BadRequest("The notification time cannot be set in the past. Please choose a valid time.");
                }

                // Convert boolean status to integer for database
                int statusValue = task.ScheduleStatus ? 1 : 0;

                // Only update the status and notification fields
                const string updateQuery = @"
                UPDATE Tasks 
                SET 
                    sched_notify = @ScheduleNotify,
                    sched_status = @ScheduleStatus
                WHERE sched_Id = @ScheduleId";

                await connection.ExecuteAsync(updateQuery, new
                {
                    task.ScheduleNotify,
                    ScheduleStatus = statusValue,
                    ScheduleId = scheduleId
                });

                // Track changes for logging
                List<string> changes = new List<string>();

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

                return Ok(new
                {
                    Message = $"Task entry with ID {scheduleId} updated successfully.",
                    Changes = changes
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


    [HttpGet("UserCountPendingTasks")]
    public async Task<IActionResult> UserCountPendingTasks()
    {
        var username = HttpContext.Session.GetString("UserName");
        if (string.IsNullOrEmpty(username))
            return Unauthorized("User is not logged in.");

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT COUNT(*) FROM Tasks WHERE sched_user = @UserName AND sched_status = 0";

        using var cmd = new MySqlCommand(query, con);
        cmd.Parameters.AddWithValue("@UserName", username);

        try
        {
            int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(count);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }



    [HttpGet("UserUpcomingPendingTasks")]
    public async Task<IActionResult> UserUpcomingPendingTasks()
    {
        var username = HttpContext.Session.GetString("UserName");
        if (string.IsNullOrEmpty(username))
            return Unauthorized("User is not logged in.");

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = @"
        SELECT COUNT(*) FROM Tasks
        WHERE sched_user = @UserName
        AND sched_status = 0
        AND sched_date > CURDATE()";

        using var cmd = new MySqlCommand(query, con);
        cmd.Parameters.AddWithValue("@UserName", username);

        try
        {
            int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(count);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }


    [HttpGet("UserDueTodayPendingTasks")]
    public async Task<IActionResult> UserDueTodayPendingTasks()
    {
        var username = HttpContext.Session.GetString("UserName");
        if (string.IsNullOrEmpty(username))
            return Unauthorized("User is not logged in.");

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = @"
        SELECT COUNT(*) FROM Tasks
        WHERE sched_user = @UserName
        AND sched_status = 0
        AND sched_date = CURDATE()";

        using var cmd = new MySqlCommand(query, con);
        cmd.Parameters.AddWithValue("@UserName", username);

        try
        {
            int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(count);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }


    [HttpGet("UserOverduePendingTasks")]
    public async Task<IActionResult> UserOverduePendingTasks()
    {
        var username = HttpContext.Session.GetString("UserName");
        if (string.IsNullOrEmpty(username))
            return Unauthorized("User is not logged in.");

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = @"
        SELECT COUNT(*) FROM Tasks
        WHERE sched_user = @UserName
        AND sched_status = 0
        AND sched_date < CURDATE()";

        using var cmd = new MySqlCommand(query, con);
        cmd.Parameters.AddWithValue("@UserName", username);

        try
        {
            int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(count);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }




    [HttpGet("export-user-pending-tasks")]
    public async Task<IActionResult> ExportUserPendingTasksReport()
    {
        var username = HttpContext.Session.GetString("UserName");
        if (string.IsNullOrEmpty(username))
            return Unauthorized("User is not logged in.");

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
        WHERE sched_user = @UserName AND sched_status = 0
        ORDER BY is_past ASC, 
                 CASE WHEN is_past = 0 THEN sched_date END ASC,
                 CASE WHEN is_past = 1 THEN sched_date END DESC,
                 ScheduleInputted DESC";

            using var connection = new MySqlConnection(_connectionString);
            var taskData = (await connection.QueryAsync<Tasksdto>(query, new { UserName = username })).ToList();

            if (!taskData.Any())
                return NotFound("No pending task records found for the logged-in user.");

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Pending Tasks");
            worksheet.Cell("A1").Value = "USER PENDING TASK REPORT";
            worksheet.Range("A1:F1").Merge().Style.Font.SetBold(true).Font.SetFontSize(14).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            worksheet.Cell("A2").Value = $"Date Exported: {DateTime.Now:MM/dd/yyyy hh:mm tt}";
            worksheet.Range("A2:F2").Merge().Style.Font.SetItalic(true).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            var headers = new[] { "Task Title", "User", "Description", "Task Date", "Date Inputted", "Status" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(4, i + 1).Value = headers[i];
                worksheet.Cell(4, i + 1).Style.Font.SetBold(true).Fill.SetBackgroundColor(XLColor.LightGray).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }

            int row = 4;
            foreach (var record in taskData)
            {
                worksheet.Cell(row, 1).Value = record.ScheduleTaskTitle ?? "N/A";
                worksheet.Cell(row, 2).Value = record.ScheduleUser ?? "N/A";
                worksheet.Cell(row, 3).Value = record.ScheduleTaskDescription ?? "N/A";
                worksheet.Cell(row, 4).Value = record.ScheduleDate.ToString("yyyy-MM-dd HH:mm:ss");
                worksheet.Cell(row, 5).Value = record.ScheduleStatus ? "Completed" : "Pending";
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
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"PendingTasks_{username}_{DateTime.Now:yyyyMMdd}.xlsx");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting task data: {ex.Message}");
            return StatusCode(500, $"Error exporting task data: {ex.Message}");
        }
    }


}

