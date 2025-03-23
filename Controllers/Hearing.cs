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
public class HearingController : ControllerBase
{
    private readonly string _connectionString;

    public HearingController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }

    [HttpPost("AddHearing")]
    public async Task<IActionResult> AddHearing([FromBody] Hearingdto hearing)
    {
        if (hearing == null || string.IsNullOrWhiteSpace(hearing.HearingCaseTitle) ||
            string.IsNullOrWhiteSpace(hearing.HearingCaseNumber))
        {
            return BadRequest("Invalid Hearing data.");
        }

        hearing.HearingCaseDate = DateTime.Now.ToString("yyyy-MM-dd");
        hearing.HearingCaseTime = DateTime.Now.ToString("HH:mm:ss");

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        try
        {
            string insertQuery = @"INSERT INTO Hearing (hearing_Case_Title, hearing_Case_Num, hearing_Case_Date, hearing_Case_Time, hearing_case_status)
                           VALUES (@CaseTitle, @CaseNumber, @CaseDate, @CaseTime, @CaseStatus)";

            using var insertCmd = new MySqlCommand(insertQuery, con);
            insertCmd.Parameters.AddWithValue("@CaseTitle", hearing.HearingCaseTitle.Trim());
            insertCmd.Parameters.AddWithValue("@CaseNumber", hearing.HearingCaseNumber.Trim());
            insertCmd.Parameters.AddWithValue("@CaseDate", hearing.HearingCaseDate);
            insertCmd.Parameters.AddWithValue("@CaseTime", hearing.HearingCaseTime);
            insertCmd.Parameters.AddWithValue("@CaseStatus", hearing.HearingCaseStatus);

            await insertCmd.ExecuteNonQueryAsync();

            // Log the action
            await Logger.LogAction($"Hearing {hearing.HearingCaseTitle} has been added.", "Hearing", 0);

            return Ok("Hearing added successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return StatusCode(500, "An error occurred while adding the hearing.");
        }
    }

    [HttpPut("UpdateCourtHearing/{id}")]
    public async Task<IActionResult> UpdateCourtHearing(int id, [FromBody] Hearingdto hearing, [FromHeader(Name = "UserName")] string userName = "System")
    {
        Console.WriteLine($"Incoming ID from URL: {id}");
        Console.WriteLine($"Incoming Hearing ID: {hearing?.HearingId}");

        if (hearing == null)
        {
            return BadRequest("Invalid hearing data.");
        }

        if (hearing.HearingId == 0)
        {
            hearing.HearingId = id;
        }

        if (id != hearing.HearingId)
        {
            Console.WriteLine("ID mismatch detected.");
            return BadRequest("ID mismatch.");
        }

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            // Fetch old values from DB
            var existingHearing = await connection.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT hearing_Case_Title, hearing_Case_Num, hearing_case_status FROM Hearing WHERE hearing_Id = @HearingId",
                new { HearingId = id });

            if (existingHearing == null)
            {
                return NotFound("Hearing not found.");
            }

            // Extract old values
            string oldCaseTitle = existingHearing?.hearing_Case_Title ?? "";
            string oldCaseNumber = existingHearing?.hearing_Case_Num ?? "";
            string oldCaseStatus = existingHearing?.hearing_case_status ?? "";

            // Compare old and new values to track changes
            List<string> changes = new List<string>();

            if (!string.Equals(oldCaseTitle, hearing.HearingCaseTitle ?? "", StringComparison.Ordinal))
            {
                changes.Add($"Case Title: \"{oldCaseTitle}\" → \"{hearing.HearingCaseTitle}\"");
            }
            if (!string.Equals(oldCaseNumber, hearing.HearingCaseNumber ?? "", StringComparison.Ordinal))
            {
                changes.Add($"Case Number: \"{oldCaseNumber}\" → \"{hearing.HearingCaseNumber}\"");
            }
            if (!string.Equals(oldCaseStatus, hearing.HearingCaseStatus ?? "", StringComparison.Ordinal))
            {
                changes.Add($"Case Status: \"{oldCaseStatus}\" → \"{hearing.HearingCaseStatus}\"");
            }

            // Perform update
            string query = @"UPDATE Hearing 
                   SET hearing_Case_Title = @CaseTitle, 
                       hearing_Case_Num = @CaseNumber, 
                       hearing_case_status = @CaseStatus 
                   WHERE hearing_Id = @HearingId";

            var result = await connection.ExecuteAsync(query, new
            {
                CaseTitle = hearing.HearingCaseTitle ?? oldCaseTitle,
                CaseNumber = hearing.HearingCaseNumber ?? oldCaseNumber,
                CaseStatus = hearing.HearingCaseStatus ?? oldCaseStatus,
                HearingId = id
            });

            if (result > 0)
            {
                // Log changes if any
                if (changes.Count > 0)
                {
                    string logMessage = $"Updated hearing record (ID: {id})";
                    string details = string.Join(", ", changes);
                    await Logger.LogAction(logMessage, "HEARING", id, userName, details);
                }

                return Ok(new { Message = $"Hearing entry with ID {id} updated successfully." });
            }

            return StatusCode(500, "No changes were made.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return StatusCode(500, new { message = $"Error updating hearing: {ex.Message}" });
        }
    }

    [HttpDelete("DeleteHearing/{id}")]
    public async Task<IActionResult> DeleteHearing(int id, [FromHeader(Name = "UserName")] string userName = "System")
    {
        Console.WriteLine($"DeleteHearing called with ID: {id}"); // Log when the route is hit

        if (id <= 0)
        {
            return BadRequest("Invalid hearing ID.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        try
        {
            // Retrieve the hearing details before deletion
            string selectQuery = "SELECT * FROM Hearing WHERE hearing_Id = @HearingId";
            var hearing = await con.QueryFirstOrDefaultAsync(selectQuery, new { HearingId = id });

            if (hearing == null)
            {
                return NotFound("No hearing found with the selected ID.");
            }

            // Delete the hearing record
            string deleteQuery = "DELETE FROM Hearing WHERE hearing_Id = @HearingId";
            int rowsAffected = await con.ExecuteAsync(deleteQuery, new { HearingId = id });

            if (rowsAffected > 0)
            {
                // Log the deleted hearing details
                string details = $"Deleted Hearing: ID={hearing.hearing_Id}, Date={hearing.hearing_Date}, Time={hearing.hearing_Time}, Case ID={hearing.case_Id}";
                await Logger.LogAction("Delete", "Hearing", id, userName, details);

                return Ok("Hearing has been deleted successfully.");
            }
            else
            {
                return NotFound("No hearing found with the selected ID.");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred while deleting the hearing: {ex.Message}");
        }
    }

    [HttpGet("GetHearing")]
    public async Task<IActionResult> GetHearing()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT hearing_Id, hearing_Case_Title, hearing_Case_Num, hearing_Case_Date, hearing_Case_Time, hearing_case_status FROM Hearing";
        using var cmd = new MySqlCommand(query, con);

        using var reader = await cmd.ExecuteReaderAsync();

        var hearings = new List<Hearingdto>();
        while (await reader.ReadAsync())
        {
            hearings.Add(new Hearingdto
            {
                HearingId = Convert.ToInt32(reader["hearing_Id"]),
                HearingCaseTitle = reader["hearing_Case_Title"]?.ToString(),
                HearingCaseNumber = reader["hearing_Case_Num"]?.ToString(),
                HearingCaseDate = reader["hearing_Case_Date"]?.ToString() ?? string.Empty,
                HearingCaseTime = reader["hearing_Case_Time"]?.ToString() ?? string.Empty,
                HearingCaseStatus = reader["hearing_case_status"]?.ToString()
            });
        }

        return Ok(hearings);
    }

    [HttpGet("CountHearings")]
    public async Task<IActionResult> CountHearings()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT COUNT(*) FROM Hearing";
        using var cmd = new MySqlCommand(query, con);

        try
        {
            int userCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(userCount);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    [HttpGet("FilterHearings")]
    public async Task<IActionResult> FilterHearings(string All)
    {
        string query;

        switch (All)
        {
            case "Today":
                query = @"SELECT hearing_Id AS HearingId, hearing_Case_Title AS HearingCaseTitle, 
                         hearing_Case_Num AS HearingCaseNumber, hearing_Case_Date AS HearingCaseDate, 
                         TIME_FORMAT(hearing_Case_Time, '%H:%i:%s') AS HearingCaseTime, hearing_case_status AS HearingCaseStatus
                  FROM Hearing 
                  WHERE hearing_Case_Date = CURDATE()";
                break;
            case "This Week":
                query = @"SELECT hearing_Id AS HearingId, hearing_Case_Title AS HearingCaseTitle, 
                         hearing_Case_Num AS HearingCaseNumber, hearing_Case_Date AS HearingCaseDate, 
                         TIME_FORMAT(hearing_Case_Time, '%H:%i:%s') AS HearingCaseTime, hearing_case_status AS HearingCaseStatus
                  FROM Hearing 
                  WHERE YEARWEEK(hearing_Case_Date, 1) = YEARWEEK(CURDATE(), 1)";
                break;
            default:
                query = @"SELECT hearing_Id AS HearingId, hearing_Case_Title AS HearingCaseTitle, 
                         hearing_Case_Num AS HearingCaseNumber, hearing_Case_Date AS HearingCaseDate, 
                         TIME_FORMAT(hearing_Case_Time, '%H:%i:%s') AS HearingCaseTime, hearing_case_status AS HearingCaseStatus
                  FROM Hearing";
                break;
        }

        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                var hearings = await connection.QueryAsync<Hearingdto>(query);
                return Ok(hearings);
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error filtering hearings: {ex.Message}" });
        }
    }
}
