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
public class NotificationController : ControllerBase
{
    private readonly string _connectionString;

    public NotificationController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        Validation.Initialize(configuration);
    }
    [HttpGet("NotificationsData")]
    public async Task<IActionResult> GetNotificationDetails()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = @"
SELECT 
    sched_taskTitle AS Title,
    sched_taskDescription AS Description,
    CASE 
        WHEN sched_status = 0 THEN 'Pending'
        ELSE 'Finished'
    END AS Status,
    'Task' AS Type,
    DATE(sched_date) AS DueDate,
    TIME(sched_date) AS DueTime
FROM Tasks 
WHERE 
    sched_status = 0 -- Only show pending tasks
    AND (
        (sched_date >= CURDATE()) -- Show tasks with future due dates
        OR
        (sched_date < CURDATE() AND DATEDIFF(CURDATE(), sched_date) <= sched_notify) -- Show past tasks within the notification range
    )

UNION ALL

SELECT 
    TRIM(hearing_Case_Title) AS Title,
    TRIM(hearing_Case_Num) AS Description,
    TRIM(hearing_case_status) AS Status,
    'Hearing' AS Type,
    DATE(hearing_Case_Date) AS DueDate,
    TIME(hearing_Case_Date) AS DueTime
FROM Hearing 
WHERE 
    DATEDIFF(hearing_Case_Date, CURDATE()) >= 0 -- Only show future hearings
    AND TRIM(hearing_case_status) = 'Pending'
    AND hearing_case_status != 'Checked'

UNION ALL

SELECT 
    CONCAT(marriage_brideFirstname, ' ', marriage_brideLastname, ' & ', marriage_groomFirstname, ' ', marriage_groomlastname) AS Title,
    CONCAT('Marriage scheduled for ', DATE_FORMAT(marriage_startin, '%Y-%m-%d')) AS Description,
    CASE 
        WHEN marriage_checkbox = 1 THEN 'Finished'
        ELSE 'Pending'
    END AS Status,
    'Marriage' AS Type,
    DATE(marriage_startin) AS DueDate,
    TIME(marriage_startin) AS DueTime
FROM Marriage
WHERE 
    (DATEDIFF(marriage_startin, CURDATE()) >= 0 -- Only show future marriages
    OR (DATEDIFF(CURDATE(), marriage_startin) <= marriage_notifyme AND marriage_checkbox = 0)) -- Show past marriages within the notification range
    AND marriage_notifyme = 1
    AND marriage_checkbox = 0;";

        using var cmd = new MySqlCommand(query, con);
        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            var results = new List<object>();
            int rowCount = 0;

            while (await reader.ReadAsync())
            {
                rowCount++;
                results.Add(new
                {
                    Title = reader["Title"].ToString(),
                    Description = reader["Description"].ToString(),
                    Status = reader["Status"] is byte[]? Encoding.UTF8.GetString((byte[])reader["Status"]) : reader["Status"].ToString(),
                    Type = reader["Type"].ToString(),
                    DueDate = Convert.ToDateTime(reader["DueDate"]).ToString("yyyy-MM-dd"),
                    DueTime = TimeSpan.TryParse(reader["DueTime"].ToString(), out var time) ? time.ToString(@"hh\:mm") : "00:00"
                });
            }

            Console.WriteLine($"Query returned {rowCount} rows");

            return Ok(results);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error executing query: {ex.Message}");
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }






    [HttpGet("TestNotifications")]
    public async Task<IActionResult> TestNotifications()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        // Very simple query that should return all tasks
        string query = "SELECT * FROM Tasks";

        using var cmd = new MySqlCommand(query, con);
        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            var results = new List<object>();

            while (await reader.ReadAsync())
            {
                var rowData = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    rowData[reader.GetName(i)] = reader[i];
                }
                results.Add(rowData);
            }

            return Ok(new
            {
                Message = "Raw tasks data",
                Count = results.Count,
                Data = results
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    [HttpGet("NotificationCounts")]
    public async Task<IActionResult> GetNotificationCount()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();
        string query = @"SELECT COUNT(*) FROM (
                    SELECT sched_Id FROM Tasks
                    WHERE CAST(sched_date AS DATE) = CAST(CURDATE() AS DATE)
                    AND TRIM(sched_status) = 'Pending'
                    UNION ALL
                    SELECT hearing_Id FROM Hearing
                    WHERE CAST(hearing_Case_Date AS DATE) = CAST(CURDATE() AS DATE)
                    AND TRIM(hearing_case_status) = 'Pending'
                    UNION ALL
                    SELECT marriage_Id FROM Marriage
                    WHERE CAST(marriage_OCC AS DATE) = CAST(CURDATE() AS DATE)
                    AND  marriage_notifyme = 1
                    AND (marriage_checkbox = 0 OR marriage_checkbox IS NULL)
                ) AS CombinedCount";
        using var cmd = new MySqlCommand(query, con);
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
