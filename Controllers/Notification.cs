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

[Route("api/[controller]")]
[ApiController]
public class NotificationController : ControllerBase
{
    private readonly string _connectionString;

    public NotificationController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }

    [HttpGet("NotificationsData")]
    public async Task<IActionResult> GetNotificationDetails()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = @"SELECT 
                        sched_taskTitle AS Title,
                        sched_taskDescription AS Description,
                        TRIM(sched_status) AS Status
                    FROM Tasks 
                    WHERE CAST(sched_date AS DATE) = CAST(CURDATE() AS DATE)
                    AND TRIM(sched_status) = 'Pending'

                    UNION ALL

                    SELECT 
                        TRIM(hearing_Case_Title) AS Title,
                        TRIM(hearing_Case_Num) AS Description,
                        TRIM(hearing_case_status) AS Status
                    FROM Hearing 
                    WHERE CAST(hearing_Case_Date AS DATE) = CAST(CURDATE() AS DATE)
                    AND TRIM(hearing_case_status) = 'Pending'";

        using var cmd = new MySqlCommand(query, con);

        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            var results = new List<object>();

            while (await reader.ReadAsync())
            {
                results.Add(new
                {
                    Title = reader["Title"].ToString(),
                    Description = reader["Description"].ToString(),
                    Status = reader["Status"].ToString()
                });
            }

            return Ok(results);
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
