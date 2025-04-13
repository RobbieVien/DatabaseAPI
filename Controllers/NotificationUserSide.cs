using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Data;
using Dapper;
using System.Collections.Generic;
using System.Security.Claims;
using DatabaseAPI.Models;
using DatabaseAPI.Utilities;
using System.Text;

[Route("api/[controller]")]
[ApiController]
public class NotificationUserSideController : ControllerBase
{
    private readonly string _connectionString;

    public NotificationUserSideController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        Validation.Initialize(configuration);
    }

    //eto pag pinindot mo yung notifications Icon
    [HttpGet("NotificationsData")]
    public async Task<IActionResult> GetNotificationDetails()
    {
        var username = HttpContext.Session.GetString("UserName");

        Console.WriteLine($"🔍 Logged-in user from session: {username ?? "null"}");

        if (string.IsNullOrEmpty(username))
            return Unauthorized("User is not logged in.");

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = @"
    -- TASKS: Only show pending tasks for the logged-in user
    SELECT 
        sched_taskTitle AS Title,
        sched_taskDescription AS Description,
        CASE 
            WHEN sched_status = 0 THEN 'Pending'
            ELSE 'Finished'
        END AS Status,
        'Task' AS Type,
        DATE(sched_date) AS DueDate,
        TIME(sched_date) AS DueTime,
        sched_date AS SortDateTime,
        sched_user AS AssignedUser
    FROM Tasks 
    WHERE 
        sched_user = @SchedUser
        AND sched_status = 0

    UNION ALL

    -- HEARING: Only show pending hearings
    SELECT 
        TRIM(hearing_Case_Title) AS Title,
        TRIM(hearing_Case_Num) AS Description,
        CASE 
            WHEN hearing_case_status = 0 THEN 'Pending'
            ELSE 'Finished'
        END AS Status,
        'Hearing' AS Type,
        DATE(hearing_Case_Date) AS DueDate,
        TIME(hearing_Case_Time) AS DueTime,
        hearing_Case_Date AS SortDateTime,
        NULL AS AssignedUser
    FROM Hearing 
    WHERE 
        hearing_case_status = 0
        AND (
            hearing_Case_Date >= CURDATE()
            OR (hearing_Case_Date < CURDATE() AND hearing_notify > 0)
        )

    UNION ALL

    -- MARRIAGE: Only show pending marriages
    SELECT 
        CONCAT(marriage_brideFirstname, ' ', marriage_brideLastname, ' & ', marriage_groomFirstname, ' ', marriage_groomlastname) AS Title,
        CONCAT('Marriage scheduled for ', DATE_FORMAT(marriage_startin, '%Y-%m-%d')) AS Description,
        CASE 
            WHEN marriage_checkbox = 1 THEN 'Finished'
            ELSE 'Pending'
        END AS Status,
        'Marriage' AS Type,
        DATE(marriage_startin) AS DueDate,
        TIME(marriage_startin) AS DueTime,
        marriage_startin AS SortDateTime,
        NULL AS AssignedUser
    FROM Marriage
    WHERE 
        marriage_checkbox = 0

    ORDER BY SortDateTime ASC";

        using var cmd = new MySqlCommand(query, con);
        cmd.Parameters.AddWithValue("@SchedUser", username);

        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            var results = new List<object>();
            int rowCount = 0;

            if (!reader.HasRows)
            {
                Console.WriteLine("⚠️ No rows returned from the notification query.");
            }

            while (await reader.ReadAsync())
            {
                rowCount++;
                var type = reader["Type"].ToString();

                var item = new Dictionary<string, object>
                {
                    ["Title"] = reader["Title"].ToString(),
                    ["Description"] = reader["Description"].ToString(),
                    ["Status"] = reader["Status"] is byte[]? Encoding.UTF8.GetString((byte[])reader["Status"]) : reader["Status"].ToString(),
                    ["Type"] = type,
                    ["DueDate"] = Convert.ToDateTime(reader["DueDate"]).ToString("yyyy-MM-dd"),
                    ["DueTime"] = TimeSpan.TryParse(reader["DueTime"].ToString(), out var time) ? time.ToString(@"hh\:mm") : "00:00"
                };

                // Only include AssignedUser if it's a Task
                if (type == "Task")
                {
                    item["AssignedUser"] = reader["AssignedUser"]?.ToString();
                }

                Console.WriteLine($"✅ Found {type} record: {item["Title"]}");

                results.Add(item);
            }

            Console.WriteLine($"✅ Query completed. Rows returned: {rowCount}");
            return Ok(results);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error executing query: {ex.Message}");
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }









    [HttpGet("NotificationCounts")]
    public async Task<IActionResult> GetNotificationCount()
    {
        var username = HttpContext.Session.GetString("UserName");
        var role = HttpContext.Session.GetString("UserRole");

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(role))
            return Unauthorized("User is not logged in.");

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        // Updated query to count all pending notifications, regardless of the due date.
        string query = @"
    SELECT COUNT(*) FROM (
        -- Count pending tasks for the logged-in user
        SELECT sched_Id FROM Tasks
        WHERE 
            sched_user = @UserName
            AND sched_status = 0  -- Only Pending tasks
            AND (sched_date >= CURDATE() OR sched_status = 0)  -- Include past due if still pending

        UNION ALL

        -- Count pending hearings
        SELECT hearing_Id FROM Hearing
        WHERE 
            hearing_case_status = 0  -- Only Pending hearings
            AND (hearing_Case_Date >= CURDATE() OR hearing_case_status = 0)  -- Include past due if still pending

        UNION ALL

        -- Count pending marriages
        SELECT marriage_Id FROM Marriage
        WHERE 
            marriage_checkbox = 0  -- Only Pending marriages (not completed)
            AND (marriage_startin >= CURDATE() OR marriage_checkbox = 0)  -- Include past due if still pending
    ) AS CombinedCount";

        using var cmd = new MySqlCommand(query, con);
        cmd.Parameters.AddWithValue("@UserName", username); // 🔐 Filter tasks for this user only

        try
        {
            int notificationCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(notificationCount);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }


}
