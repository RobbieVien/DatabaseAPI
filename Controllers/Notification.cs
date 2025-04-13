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
        var username = HttpContext.Session.GetString("UserName");
        var userRole = HttpContext.Session.GetString("UserRole");

        if (string.IsNullOrEmpty(username))
            return Unauthorized("User is not logged in.");

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string taskQuery;

        // Handle unknown roles gracefully
        userRole = userRole?.ToLowerInvariant();

        if (userRole == "admin")
        {
            // Admin sees all tasks EXCEPT assigned to chiefadmin
            taskQuery = @"
        SELECT 
            t.sched_taskTitle AS Title,
            t.sched_taskDescription AS Description,
            CASE 
                WHEN t.sched_status = 0 THEN 'Pending'
                ELSE 'Finished'
            END AS Status,
            'Task' AS Type,
            DATE(t.sched_date) AS DueDate,
            TIME(t.sched_date) AS DueTime,
            t.sched_date AS SortDateTime,
            t.sched_user AS AssignedUser
        FROM Tasks t
        LEFT JOIN ManageUsers u ON t.sched_user = u.username
        WHERE 
            t.sched_status = 0
            AND (u.role IS NULL OR u.role != 'chiefadmin')
            AND (
                (t.sched_date >= CURDATE())
                OR
                (t.sched_date < CURDATE() AND t.sched_notify > 0)
            )";
        }
        else
        {
            // Regular users only see their tasks
            taskQuery = @"
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
            sched_status = 0
            AND sched_user = @SchedUser
            AND (
                (sched_date >= CURDATE())
                OR
                (sched_date < CURDATE() AND sched_notify > 0)
            )";
        }

        string hearingQuery = @"
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
            (hearing_Case_Date >= CURDATE())
            OR
            (hearing_Case_Date < CURDATE() AND hearing_notify > 0)
        )";

        string marriageQuery = @"
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
        marriage_notifyme = 1
        AND marriage_checkbox = 0";

        // Combine
        string combinedQuery = $@"
    {taskQuery}
    UNION ALL
    {hearingQuery}
    UNION ALL
    {marriageQuery}
    ORDER BY SortDateTime ASC";

        try
        {
            using var cmd = new MySqlCommand(combinedQuery, con);

            // Add parameter if not admin
            if (userRole != "admin")
            {
                cmd.Parameters.AddWithValue("@SchedUser", username);
            }

            Console.WriteLine("Executing combined notification query:");
            Console.WriteLine(combinedQuery);
            Console.WriteLine("User Role: " + userRole + " | Username: " + username);

            using var reader = await cmd.ExecuteReaderAsync();
            var results = new List<object>();
            int rowCount = 0;

            while (await reader.ReadAsync())
            {
                try
                {
                    string dueTimeStr = reader["DueTime"].ToString();
                    string formattedTime = "00:00";

                    if (!string.IsNullOrEmpty(dueTimeStr) && TimeSpan.TryParse(dueTimeStr, out TimeSpan dueTime))
                    {
                        formattedTime = dueTime.ToString(@"hh\:mm");
                    }

                    results.Add(new
                    {
                        Title = reader["Title"].ToString(),
                        Description = reader["Description"].ToString(),
                        Status = reader["Status"] is byte[]? Encoding.UTF8.GetString((byte[])reader["Status"])
                            : reader["Status"].ToString(),
                        Type = reader["Type"].ToString(),
                        DueDate = Convert.ToDateTime(reader["DueDate"]).ToString("yyyy-MM-dd"),
                        DueTime = formattedTime,
                        AssignedUser = reader["AssignedUser"] != DBNull.Value ? reader["AssignedUser"].ToString() : "N/A"
                    });

                    rowCount++;
                }
                catch (Exception exRow)
                {
                    Console.WriteLine("Error reading row: " + exRow.Message);
                }
            }

            Console.WriteLine($"Query returned {rowCount} rows.");
            return Ok(results);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error executing query: " + ex.Message);
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
