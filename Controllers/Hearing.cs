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
using System.Configuration;

[Route("api/[controller]")]
[ApiController]
public class HearingController : ControllerBase
{
    private readonly string _connectionString;

    public HearingController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }

    private readonly IConfiguration _configuration;

    [HttpPost("AddHearing")]
    public async Task<ActionResult<string>> AddHearing([FromBody] Hearingdto hearing)
    {
        // Validate input
        if (hearing == null || string.IsNullOrWhiteSpace(hearing.HearingCaseTitle) ||
            string.IsNullOrWhiteSpace(hearing.HearingCaseNumber))
        {
            return BadRequest("Invalid Hearing data.");
        }

        // Get Philippine time zone
        TimeZoneInfo philippineTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
        DateTime philippineNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, philippineTimeZone);

        // Parse date and time
        if (!DateTime.TryParse(hearing.HearingCaseDate, out DateTime hearingDate) ||
            !TimeSpan.TryParse(hearing.HearingCaseTime, out TimeSpan hearingTime))
        {
            return BadRequest("Invalid date or time format.");
        }

        // Combine hearing date and time
        DateTime hearingDateTime = hearingDate.Date.Add(hearingTime);

        // Check if hearing is in the past with a buffer of 1 minute to account for processing time
        if (hearingDateTime < philippineNow.AddMinutes(1))
        {
            return BadRequest("Cannot add a hearing with a past or immediate date and time. Please choose a future time.");
        }

        string insertQuery = @"
            INSERT INTO Hearing 
            (hearing_Case_Title, hearing_Case_Num, hearing_Case_Date, hearing_Case_Time, 
             hearing_Case_Inputted, hearing_case_status)
            VALUES 
            (@CaseTitle, @CaseNumber, @CaseDate, @CaseTime, 
             CONVERT_TZ(NOW(), '+00:00', '+08:00'), @CaseStatus)";

        var parameters = new DynamicParameters();
        parameters.Add("@CaseTitle", hearing.HearingCaseTitle.Trim());
        parameters.Add("@CaseNumber", hearing.HearingCaseNumber.Trim());
        parameters.Add("@CaseDate", hearingDate);
        parameters.Add("@CaseTime", hearingTime);
        parameters.Add("@CaseStatus", hearing.HearingCaseStatus ? 1 : 0);

        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                await connection.ExecuteAsync(insertQuery, parameters);
            }

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
            return BadRequest("ID mismatch.");
        }

        // Time zone conversion for Philippine time
        TimeZoneInfo philippineTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
        DateTime philippineNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, philippineTimeZone);

        // Validate date and time
        if (!DateTime.TryParse(hearing.HearingCaseDate, out DateTime hearingDate) ||
            !TimeSpan.TryParse(hearing.HearingCaseTime, out TimeSpan hearingTime))
        {
            return BadRequest("Invalid date or time format.");
        }

        // Combine hearing date and time
        DateTime hearingDateTime = hearingDate.Date.Add(hearingTime);

        // Check if hearing is in the past
        if (hearingDateTime < philippineNow)
        {
            return BadRequest("Cannot update a hearing with a past date and time.");
        }

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            // Fetch old values from DB
            var existingHearing = await connection.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT hearing_Case_Title, hearing_Case_Num, hearing_Case_Date, hearing_Case_Time, hearing_case_status 
            FROM Hearing WHERE hearing_Id = @HearingId",
                new { HearingId = id });

            if (existingHearing == null)
            {
                return NotFound("Hearing not found.");
            }

            // Extract old values
            string oldCaseTitle = existingHearing?.hearing_Case_Title ?? "";
            string oldCaseNumber = existingHearing?.hearing_Case_Num ?? "";
            string oldCaseDate = existingHearing?.hearing_Case_Date != null ? Convert.ToDateTime(existingHearing.hearing_Case_Date).ToString("yyyy-MM-dd") : "";
            string oldCaseTime = existingHearing?.hearing_Case_Time != null
                ? ((TimeSpan)existingHearing.hearing_Case_Time).ToString(@"hh\:mm\:ss")
                : "";

            bool oldCaseStatus = existingHearing?.hearing_case_status == 1;

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
            if (!string.Equals(oldCaseDate, hearing.HearingCaseDate ?? "", StringComparison.Ordinal))
            {
                changes.Add($"Case Date: \"{oldCaseDate}\" → \"{hearing.HearingCaseDate}\"");
            }
            if (!string.Equals(oldCaseTime, hearing.HearingCaseTime ?? "", StringComparison.Ordinal))
            {
                changes.Add($"Case Time: \"{oldCaseTime}\" → \"{hearing.HearingCaseTime}\"");
            }
            if (oldCaseStatus != hearing.HearingCaseStatus)
            {
                changes.Add($"Case Status: \"{(oldCaseStatus ? "Active" : "Finished")}\" → \"{(hearing.HearingCaseStatus ? "Active" : "Finished")}\"");
            }

            // Perform update
            string query = @"UPDATE Hearing 
                   SET hearing_Case_Title = @CaseTitle, 
                       hearing_Case_Num = @CaseNumber, 
                       hearing_Case_Date = @CaseDate,
                       hearing_Case_Time = @CaseTime,
                       hearing_case_status = @CaseStatus 
                   WHERE hearing_Id = @HearingId";

            var result = await connection.ExecuteAsync(query, new
            {
                CaseTitle = hearing.HearingCaseTitle ?? oldCaseTitle,
                CaseNumber = hearing.HearingCaseNumber ?? oldCaseNumber,
                CaseDate = hearing.HearingCaseDate ?? oldCaseDate,
                CaseTime = hearing.HearingCaseTime ?? oldCaseTime,
                CaseStatus = hearing.HearingCaseStatus ? 1 : 0,
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
        Console.WriteLine($"DeleteHearing called with ID: {id} by {userName}");

        if (id <= 0)
        {
            return BadRequest("Invalid hearing ID.");
        }

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            // Fetch old values before deletion
            var existingHearing = await connection.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT hearing_Case_Title, hearing_Case_Num, hearing_case_status, hearing_Case_Date, hearing_Case_Time " +
                "FROM Hearing WHERE hearing_Id = @HearingId",
                new { HearingId = id });

            if (existingHearing == null)
            {
                Console.WriteLine($"No hearing found for ID: {id}");
                return NotFound($"No hearing found with the ID {id}.");
            }

            // Extract old values
            string oldCaseTitle = existingHearing?.hearing_Case_Title ?? "";
            string oldCaseNumber = existingHearing?.hearing_Case_Num ?? "";
            string oldCaseStatus = existingHearing?.hearing_case_status ?? "";

            // Handle date and time separately to avoid type conversion issues
            string oldCaseDate = existingHearing?.hearing_Case_Date != null ? ((DateTime)existingHearing.hearing_Case_Date).ToString("yyyy-MM-dd") : "N/A";
            string oldCaseTime = existingHearing?.hearing_Case_Time != null ? ((TimeSpan)existingHearing.hearing_Case_Time).ToString(@"hh\:mm\:ss") : "N/A";

            // Delete the hearing entry
            string deleteQuery = "DELETE FROM Hearing WHERE hearing_Id = @HearingId";
            int rowsAffected = await connection.ExecuteAsync(deleteQuery, new { HearingId = id });

            if (rowsAffected > 0)
            {
                Console.WriteLine($"Hearing ID {id} deleted successfully by {userName}.");

                // Log deletion details similar to update
                List<string> changes = new List<string>
            {
                $"Date: \"{oldCaseDate}\"",
                $"Time: \"{oldCaseTime}\"",
                $"Case Title: \"{oldCaseTitle}\"",
                $"Case Number: \"{oldCaseNumber}\"",
                $"Case Status: \"{oldCaseStatus}\""
            };

                string logMessage = $"Deleted hearing record (ID: {id})";
                string details = string.Join(", ", changes);
                await Logger.LogAction(logMessage, "HEARING", id, userName, details);

                return Ok(new { Message = $"Hearing entry with ID {id} deleted successfully." });
            }

            return StatusCode(500, "No changes were made.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting hearing entry: {ex.Message}");
            return StatusCode(500, new { Message = "Error deleting hearing entry.", ErrorDetails = ex.Message });
        }
    }




    [HttpGet("GetHearing")]
    public async Task<IActionResult> GetHearing()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = @"SELECT hearing_Id, 
                            hearing_Case_Title, 
                            hearing_Case_Num, 
                            DATE_FORMAT(hearing_Case_Date, '%Y-%m-%d') AS hearing_Case_Date, 
                            TIME_FORMAT(hearing_Case_Time, '%H:%i:%s') AS hearing_Case_Time, 
                            DATE_FORMAT(hearing_Case_Inputted, '%Y-%m-%d %H:%i:%s') AS hearing_Case_Inputted, 
                            hearing_case_status 
                     FROM Hearing";

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
                HearingCaseInputted = reader["hearing_Case_Inputted"]?.ToString() ?? string.Empty,
                HearingCaseStatus = Convert.ToBoolean(reader["hearing_case_status"]) // Convert BIT to bool
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

    //This is for comboBox in Hearing 
    [HttpGet("GetCaseTitle")]
    public async Task<ActionResult<IEnumerable<CaseTitleReportdto>>> GetCaseTitle()
    {
        string modifiedConnectionString = _connectionString;

        if (!modifiedConnectionString.Contains("Allow Zero Datetime=true"))
        {
            var connBuilder = new MySqlConnectionStringBuilder(modifiedConnectionString)
            {
                AllowZeroDateTime = true,
                ConvertZeroDateTime = true
            };
            modifiedConnectionString = connBuilder.ConnectionString;
        }

        await using var con = new MySqlConnection(modifiedConnectionString);
        await con.OpenAsync();

        string query = "SELECT rec_Case_Title FROM COURTRECORD";

        await using var cmd = new MySqlCommand(query, con);

        try
        {
            var results = new List<CaseTitleReportdto>();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                results.Add(new CaseTitleReportdto
                {
                    RecordCaseTitle = reader.IsDBNull(reader.GetOrdinal("rec_Case_Title"))
                        ? string.Empty
                        : reader.GetString(reader.GetOrdinal("rec_Case_Title"))
                });
            }

            return Ok(results);
        }
        catch (Exception ex)
        {
            string innerExceptionMessage = ex.InnerException != null ? ex.InnerException.Message : "No inner exception";
            return StatusCode(500, new
            {
                Message = "Error retrieving records",
                ErrorDetails = ex.Message,
                InnerException = innerExceptionMessage,
                StackTrace = ex.StackTrace
            });
        }
    }


}
