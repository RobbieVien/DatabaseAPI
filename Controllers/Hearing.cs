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
using System.Drawing;
using ClosedXML.Excel;

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
    public async Task<IActionResult> AddHearing([FromBody] Hearingdto hearing)
    {
        if (hearing == null)
        {
            return BadRequest("Invalid hearing data.");
        }

        // Time zone conversion for Philippine time
        TimeZoneInfo philippineTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
        DateTime philippineNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, philippineTimeZone);

        // Set the hearing date and time to the current Philippine time if not provided
        if (hearing.HearingCaseDate == DateOnly.MinValue || hearing.HearingCaseTime == TimeOnly.MinValue)
        {
            hearing.HearingCaseDate = DateOnly.FromDateTime(philippineNow);  // Current date
            hearing.HearingCaseTime = TimeOnly.FromDateTime(philippineNow);  // Current time
        }

        // Automatically set the HearingCaseInputted to the current Philippine date and time
        string hearingCaseInputted = philippineNow.ToString("yyyy-MM-dd HH:mm:ss"); // Format as DATETIME string

        // Convert DateOnly to DateTime
        DateTime hearingDateTime = hearing.HearingCaseDate.ToDateTime(hearing.HearingCaseTime); // combine DateOnly and TimeOnly

        // Validate that the hearing date and time are not in the past
        if (hearingDateTime < philippineNow)
        {
            return BadRequest("Cannot add a hearing with a past date and time.");
        }

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            // Prepare the insert query
            string query = @"
        INSERT INTO Hearing 
        (hearing_Case_Title, hearing_Case_Num, hearing_Case_Date, hearing_Case_Time, hearing_case_status, 
        hearing_Judge, hearing_trial_prosecutor, hearing_branch_clerk, hearing_public_attorney, 
        hearing_court_interpreter, hearing_court_stenographer, hearing_notify, hearing_Case_Inputted)
        VALUES 
        (@CaseTitle, @CaseNumber, @CaseDate, @CaseTime, @CaseStatus, 
        @Judge, @Prosecutor, @BranchClerk, @PublicAttorney, 
        @CourtInterpreter, @CourtStenographer, @HearingNotify, @CaseInputted);
        SELECT LAST_INSERT_ID();"; // Return the ID of the newly inserted record

            // Convert DateOnly to DateTime for database compatibility
            DateTime caseDateAsDateTime = new DateTime(hearing.HearingCaseDate.Year,
                                                       hearing.HearingCaseDate.Month,
                                                       hearing.HearingCaseDate.Day);

            // Convert TimeOnly to TimeSpan for database compatibility
            TimeSpan caseTimeAsTimeSpan = new TimeSpan(
                hearing.HearingCaseTime.Hour,
                hearing.HearingCaseTime.Minute,
                hearing.HearingCaseTime.Second);

            // Execute the insert query and get the new ID
            var newId = await connection.ExecuteScalarAsync<int>(query, new
            {
                CaseTitle = hearing.HearingCaseTitle ?? string.Empty,
                CaseNumber = hearing.HearingCaseNumber ?? string.Empty,
                CaseDate = caseDateAsDateTime, // Use converted DateTime
                CaseTime = caseTimeAsTimeSpan, // Use converted TimeSpan
                CaseStatus = hearing.HearingCaseStatus ? 1 : 0,
                Judge = hearing.HearingJudge ?? string.Empty,
                Prosecutor = hearing.HearingTrialProsecutor ?? string.Empty,
                BranchClerk = hearing.HearingBranchClerk ?? string.Empty,
                PublicAttorney = hearing.HearingPublicAttorney ?? string.Empty,
                CourtInterpreter = hearing.HearingCourtInterpreter ?? string.Empty,
                CourtStenographer = hearing.HearingCourtStenographer ?? string.Empty,
                HearingNotify = hearing.HearingNotify,
                CaseInputted = hearingCaseInputted // Use the auto-generated time for HearingCaseInputted
            });

            await Logger.LogActionAdd(HttpContext, $"Hearing {hearing.HearingCaseTitle} has been added.", "Hearing");
            return Ok(new { message = "Hearing added successfully." });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return StatusCode(500, new { message = "An error occurred while adding the hearing." });
        }
    }








    [HttpPut("UpdateCourtHearing/{id}")]
    public async Task<IActionResult> UpdateCourtHearing(int id, [FromBody] Hearingdto hearing)
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
        if (!DateTime.TryParse(hearing.HearingCaseDate.ToString("yyyy-MM-dd"), out DateTime hearingDate) ||
            !TimeSpan.TryParse(hearing.HearingCaseTime.ToString("HH:mm:ss"), out TimeSpan hearingTime))
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
                @"SELECT hearing_Case_Title, hearing_Case_Num, hearing_Case_Date, hearing_Case_Time, hearing_case_status, 
                  hearing_Judge, hearing_trial_prosecutor, hearing_branch_clerk, hearing_public_attorney,
                  hearing_court_interpreter, hearing_court_stenographer, hearing_notify
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

            // Convert the database integer value to boolean (1 = true, 0 = false)
            bool oldCaseStatus = Convert.ToInt32(existingHearing.hearing_case_status) == 1;
            int oldNotifyValue = Convert.ToInt32(existingHearing.hearing_notify);

            // New values (from the hearing DTO)
            string newCaseTitle = hearing.HearingCaseTitle ?? "";
            string newCaseNumber = hearing.HearingCaseNumber ?? "";
            string newCaseDate = hearing.HearingCaseDate.ToString("yyyy-MM-dd");
            string newCaseTime = hearing.HearingCaseTime.ToString("HH:mm:ss");
            bool newCaseStatus = hearing.HearingCaseStatus;
            int newNotifyValue = hearing.HearingNotify;

            // Compare old and new values to track changes
            List<string> changes = new List<string>();

            if (!string.Equals(oldCaseTitle, newCaseTitle, StringComparison.Ordinal))
            {
                changes.Add($"Case Title: \"{oldCaseTitle}\" → \"{newCaseTitle}\"");
            }
            if (!string.Equals(oldCaseNumber, newCaseNumber, StringComparison.Ordinal))
            {
                changes.Add($"Case Number: \"{oldCaseNumber}\" → \"{newCaseNumber}\"");
            }
            if (!string.Equals(oldCaseDate, newCaseDate, StringComparison.Ordinal))
            {
                changes.Add($"Case Date: \"{oldCaseDate}\" → \"{newCaseDate}\"");
            }
            if (!string.Equals(oldCaseTime, newCaseTime, StringComparison.Ordinal))
            {
                changes.Add($"Case Time: \"{oldCaseTime}\" → \"{newCaseTime}\"");
            }

            if (oldCaseStatus != newCaseStatus)
            {
                changes.Add($"Case Status: \"{(oldCaseStatus ? "Active" : "Finished")}\" → \"{(newCaseStatus ? "Active" : "Finished")}\"");
            }

            if (oldNotifyValue != newNotifyValue)
            {
                changes.Add($"Notify Status: \"{oldNotifyValue}\" → \"{newNotifyValue}\"");
            }

            // Add other fields like HearingJudge, Prosecutor, etc.
            if (existingHearing.hearing_Judge?.ToString() != hearing.HearingJudge)
            {
                changes.Add($"Judge: \"{existingHearing.hearing_Judge}\" → \"{hearing.HearingJudge}\"");
            }
            if (existingHearing.hearing_trial_prosecutor?.ToString() != hearing.HearingTrialProsecutor)
            {
                changes.Add($"Trial Prosecutor: \"{existingHearing.hearing_trial_prosecutor}\" → \"{hearing.HearingTrialProsecutor}\"");
            }
            if (existingHearing.hearing_branch_clerk?.ToString() != hearing.HearingBranchClerk)
            {
                changes.Add($"Branch Clerk: \"{existingHearing.hearing_branch_clerk}\" → \"{hearing.HearingBranchClerk}\"");
            }
            if (existingHearing.hearing_public_attorney?.ToString() != hearing.HearingPublicAttorney)
            {
                changes.Add($"Public Attorney: \"{existingHearing.hearing_public_attorney}\" → \"{hearing.HearingPublicAttorney}\"");
            }
            if (existingHearing.hearing_court_interpreter?.ToString() != hearing.HearingCourtInterpreter)
            {
                changes.Add($"Court Interpreter: \"{existingHearing.hearing_court_interpreter}\" → \"{hearing.HearingCourtInterpreter}\"");
            }
            if (existingHearing.hearing_court_stenographer?.ToString() != hearing.HearingCourtStenographer)
            {
                changes.Add($"Court Stenographer: \"{existingHearing.hearing_court_stenographer}\" → \"{hearing.HearingCourtStenographer}\"");
            }

            // Perform update, excluding hearing_Case_Inputted
            string query = @"UPDATE Hearing 
        SET hearing_Case_Title = @CaseTitle, 
            hearing_Case_Num = @CaseNumber, 
            hearing_Case_Date = @CaseDate,
            hearing_Case_Time = @CaseTime,
            hearing_case_status = @CaseStatus,
            hearing_Judge = @HearingJudge,
            hearing_trial_prosecutor = @TrialProsecutor,
            hearing_branch_clerk = @BranchClerk,
            hearing_public_attorney = @PublicAttorney,
            hearing_court_interpreter = @CourtInterpreter,
            hearing_court_stenographer = @CourtStenographer,
            hearing_notify = @HearingNotify
        WHERE hearing_Id = @HearingId";

            // Convert DateOnly to DateTime for database compatibility
            DateTime caseDateAsDateTime = new DateTime(hearing.HearingCaseDate.Year,
                                                     hearing.HearingCaseDate.Month,
                                                     hearing.HearingCaseDate.Day);

            // Convert TimeOnly to TimeSpan for database compatibility
            TimeSpan caseTimeAsTimeSpan = new TimeSpan(
                hearing.HearingCaseTime.Hour,
                hearing.HearingCaseTime.Minute,
                hearing.HearingCaseTime.Second);

            var result = await connection.ExecuteAsync(query, new
            {
                CaseTitle = hearing.HearingCaseTitle ?? oldCaseTitle,
                CaseNumber = hearing.HearingCaseNumber ?? oldCaseNumber,
                CaseDate = caseDateAsDateTime,  // Use converted DateTime instead of DateOnly
                CaseTime = caseTimeAsTimeSpan,  // Use converted TimeSpan instead of TimeOnly
                CaseStatus = hearing.HearingCaseStatus ? 1 : 0,
                HearingId = id,
                HearingJudge = hearing.HearingJudge ?? existingHearing.hearing_Judge?.ToString(),
                TrialProsecutor = hearing.HearingTrialProsecutor ?? existingHearing.hearing_trial_prosecutor?.ToString(),
                BranchClerk = hearing.HearingBranchClerk ?? existingHearing.hearing_branch_clerk?.ToString(),
                PublicAttorney = hearing.HearingPublicAttorney ?? existingHearing.hearing_public_attorney?.ToString(),
                CourtInterpreter = hearing.HearingCourtInterpreter ?? existingHearing.hearing_court_interpreter?.ToString(),
                CourtStenographer = hearing.HearingCourtStenographer ?? existingHearing.hearing_court_stenographer?.ToString(),
                HearingNotify = hearing.HearingNotify // Using int directly now
            });

            if (result > 0)
            {
                // Log changes if any
                if (changes.Count > 0)
                {
                    string logMessage = $"Updated hearing record (ID: {id})";
                    string details = string.Join(", ", changes);
                    // This is where we log the details to the Logger
                    await Logger.LogAction(HttpContext, logMessage, "HEARING", id, details);
                }

                return Ok(new { message = "Hearing updated successfully." });
            }
            else
            {
                return StatusCode(500, "Error updating hearing.");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error updating hearing: {ex.Message}");
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
                await Logger.LogAction(HttpContext, logMessage, "HEARING", id, details);

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
                         hearing_Case_Time, 
                         hearing_case_status,
                         hearing_Judge,
                         hearing_trial_prosecutor,
                         hearing_branch_clerk,
                         hearing_public_attorney,
                         hearing_court_interpreter,
                         hearing_court_stenographer
                  FROM Hearing";

        using var cmd = new MySqlCommand(query, con);
        using var reader = await cmd.ExecuteReaderAsync();

        var hearings = new List<HearingData>();
        while (await reader.ReadAsync())
        {
            hearings.Add(new HearingData
            {
               
                HearingCaseTitle = reader["hearing_Case_Title"]?.ToString(),
                HearingCaseNumber = reader["hearing_Case_Num"]?.ToString(),
                HearingCaseDate = reader["hearing_Case_Date"] != DBNull.Value ? DateOnly.Parse(reader["hearing_Case_Date"].ToString()) : DateOnly.MinValue,
                HearingCaseTime = reader["hearing_Case_Time"] != DBNull.Value
                    ? TimeOnly.FromTimeSpan((TimeSpan)reader["hearing_Case_Time"])
                    : TimeOnly.MinValue,
                HearingCaseStatus = Convert.ToBoolean(reader["hearing_case_status"]),
                HearingJudge = reader["hearing_Judge"]?.ToString(),
                HearingTrialProsecutor = reader["hearing_trial_prosecutor"]?.ToString(),
                HearingBranchClerk = reader["hearing_branch_clerk"]?.ToString(),
                HearingPublicAttorney = reader["hearing_public_attorney"]?.ToString(),
                HearingCourtInterpreter = reader["hearing_court_interpreter"]?.ToString(),
                HearingCourtStenographer = reader["hearing_court_stenographer"]?.ToString()
            });
        }

        return Ok(hearings);
    }









    //eto sa counting Pa test nga neto deneey, kung kaya mag combine yung tatlong HTTPGET CountHearings, FilterHearing, FilterDateHearings
    [HttpGet("CountHearings")]
    public async Task<IActionResult> CountHearings([FromQuery] string filter)
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string filterLower = filter?.Trim().ToLower();
        string query;

        switch (filterLower)
        {
            case "today":
                query = @"
                SELECT COUNT(*) 
                FROM Hearing 
                WHERE DATE(CONVERT_TZ(hearing_Case_Date, '+00:00', '+08:00')) = DATE(CONVERT_TZ(NOW(), '+00:00', '+08:00'))
                  AND hearing_case_status = 0";
                break;

            case "tomorrow":
            case "next day":
                query = @"
                SELECT COUNT(*) 
                FROM Hearing 
                WHERE DATE(CONVERT_TZ(hearing_Case_Date, '+00:00', '+08:00')) = DATE(DATE_ADD(CONVERT_TZ(NOW(), '+00:00', '+08:00'), INTERVAL 1 DAY))
                  AND hearing_case_status = 0";
                break;

            case "this week":
                query = @"
                SELECT COUNT(*) 
                FROM Hearing 
                WHERE YEARWEEK(CONVERT_TZ(hearing_Case_Date, '+00:00', '+08:00'), 1) = YEARWEEK(CONVERT_TZ(NOW(), '+00:00', '+08:00'), 1)
                  AND hearing_case_status = 0";
                break;

            case "next week":
                query = @"
                SELECT COUNT(*) 
                FROM Hearing 
                WHERE YEARWEEK(CONVERT_TZ(hearing_Case_Date, '+00:00', '+08:00'), 1) = YEARWEEK(CONVERT_TZ(NOW(), '+00:00', '+08:00'), 1) + 1
                  AND hearing_case_status = 0";
                break;

            default:
                query = "SELECT COUNT(*) FROM Hearing WHERE hearing_case_status = 0";
                break;
        }

        try
        {
            using var cmd = new MySqlCommand(query, con);
            int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(count);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }



    //sa filter to sa dashboard view details
    [HttpGet("FilterHearings")]
    public async Task<IActionResult> FilterHearings(string All)
    {
        string query;

        // Construct the SQL query based on the selected filter.
        switch (All.ToLower())
        {
            case "today":
                query = @"SELECT hearing_Case_Title AS HearingCaseTitle, 
                             hearing_Case_Num AS HearingCaseNumber, 
                             hearing_Case_Date AS HearingCaseDate, 
                             hearing_Case_Time AS HearingCaseTime, 
                             hearing_case_status AS HearingCaseStatus
                      FROM Hearing 
                      WHERE DATE(hearing_Case_Date) = CURDATE() AND hearing_case_status != 1";
                break;
            case "this week":
                query = @"SELECT hearing_Case_Title AS HearingCaseTitle, 
                             hearing_Case_Num AS HearingCaseNumber, 
                             hearing_Case_Date AS HearingCaseDate, 
                             hearing_Case_Time AS HearingCaseTime, 
                             hearing_case_status AS HearingCaseStatus
                      FROM Hearing 
                      WHERE YEARWEEK(hearing_Case_Date, 1) = YEARWEEK(CURDATE(), 1) AND hearing_case_status != 1";
                break;
            case "next week":
                query = @"SELECT hearing_Case_Title AS HearingCaseTitle, 
                             hearing_Case_Num AS HearingCaseNumber, 
                             hearing_Case_Date AS HearingCaseDate, 
                             hearing_Case_Time AS HearingCaseTime, 
                             hearing_case_status AS HearingCaseStatus
                      FROM Hearing 
                      WHERE YEARWEEK(hearing_Case_Date, 1) = YEARWEEK(CURDATE(), 1) + 1 AND hearing_case_status != 1";
                break;
            default:
                query = @"SELECT hearing_Case_Title AS HearingCaseTitle, 
                             hearing_Case_Num AS HearingCaseNumber, 
                             hearing_Case_Date AS HearingCaseDate, 
                             hearing_Case_Time AS HearingCaseTime, 
                             hearing_case_status AS HearingCaseStatus
                      FROM Hearing WHERE hearing_case_status != 1";
                break;
        }

        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                var hearings = await connection.QueryAsync(query);
                var filteredHearings = hearings.Select(item =>
                {
                    var dict = (IDictionary<string, object>)item;

                    // Manually parse Date and Time from database
                    var hearingCaseDateValue = dict["HearingCaseDate"];
                    var hearingCaseTimeValue = dict["HearingCaseTime"];

                    // Initialize the nullable DateOnly and TimeOnly
                    DateOnly? hearingCaseDate = null;
                    TimeOnly? hearingCaseTime = null;

                    // Parse the DateTime to DateOnly and TimeOnly
                    if (hearingCaseDateValue != DBNull.Value && hearingCaseDateValue is DateTime)
                    {
                        var dateTimeValue = (DateTime)hearingCaseDateValue;
                        hearingCaseDate = DateOnly.FromDateTime(dateTimeValue);
                    }

                    if (hearingCaseTimeValue != DBNull.Value && hearingCaseTimeValue is TimeSpan)
                    {
                        var timeSpanValue = (TimeSpan)hearingCaseTimeValue;
                        hearingCaseTime = TimeOnly.FromTimeSpan(timeSpanValue);
                    }

                    // Return the result object
                    return new
                    {
                        HearingCaseTitle = dict["HearingCaseTitle"]?.ToString(),
                        HearingCaseNumber = dict["HearingCaseNumber"]?.ToString(),
                        HearingCaseDate = hearingCaseDate, // nullable DateOnly
                        HearingCaseTime = hearingCaseTime, // nullable TimeOnly
                        HearingCaseStatus = dict["HearingCaseStatus"] != null && Convert.ToInt32(dict["HearingCaseStatus"]) == 1 ? "Finished" : "Pending"
                    };
                });

                return Ok(filteredHearings);
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error filtering hearings: {ex.Message}" });
        }
    }








    [HttpGet("FilterHearingsByDate")]
    public async Task<IActionResult> FilterHearingsByDate([FromQuery] string date)
    {
        // Try to parse the incoming date string to a DateTime object
        if (!DateTime.TryParse(date, out var parsedDate))
        {
            return BadRequest("Invalid date format. Use yyyy-MM-dd.");
        }

        // SQL query to filter by date and ensure it handles time zone conversion correctly
        string query = @"
    SELECT 
        hearing_Case_Title AS HearingCaseTitle, 
        hearing_Case_Num AS HearingCaseNumber, 
        hearing_Case_Date AS HearingCaseDate, 
        hearing_Case_Time AS HearingCaseTime, 
        hearing_case_status AS HearingCaseStatus
    FROM Hearing
    WHERE DATE(CONVERT_TZ(hearing_Case_Date, '+00:00', '+08:00')) = @TargetDate
    AND hearing_case_status = 0
    ORDER BY hearing_Case_Date ASC, hearing_Case_Time ASC";

        try
        {
            using var connection = new MySqlConnection(_connectionString);

            // Query the database and map the results to your DTO
            var results = await connection.QueryAsync(query, new { TargetDate = parsedDate.Date });

            // Process the results, checking for nulls and formatting correctly
            var processed = results.Select(h => new
            {
                HearingCaseTitle = h.HearingCaseTitle,
                HearingCaseNumber = h.HearingCaseNumber,
                HearingCaseDate = h.HearingCaseDate != null ? ((DateTime)h.HearingCaseDate).ToString("yyyy-MM-dd") : null,
                HearingCaseTime = h.HearingCaseTime != null ? ((TimeSpan)h.HearingCaseTime).ToString(@"hh\:mm\:ss") : null,
                // If the status is false, it will be "Pending", if true, it will be "Finished"
                HearingCaseStatus = h.HearingCaseStatus ? "Finished" : "Pending"
            });

            return Ok(processed);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error filtering hearings by date: {ex.Message}" });
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
    // sa schedule export
    [HttpGet("export-hearing")]
    public async Task<IActionResult> ExportHearingReport()
    {
        try
        {
            // Use Philippine time zone
            TimeZoneInfo philippineTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
            DateTime philippineNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, philippineTimeZone);

            string query = @"
    SELECT 
        hearing_Case_Title AS HearingCaseTitle, 
        hearing_Case_Num AS HearingCaseNumber, 
        hearing_Judge AS HearingJudge,
        hearing_Trial_Prosecutor AS HearingTrialProsecutor,
        hearing_Branch_Clerk AS HearingBranchClerk,
        hearing_Public_Attorney AS HearingPublicAttorney,
        hearing_Court_Interpreter AS HearingCourtInterpreter,
        hearing_Court_Stenographer AS HearingCourtStenographer,
        hearing_case_status AS HearingCaseStatus,
        hearing_Case_Date AS HearingCaseDate,  -- Returning as DateTime
        hearing_Case_Time AS HearingCaseTime  -- Returning as TimeOnly (TimeSpan in C#)
    FROM Hearing
    ORDER BY hearing_Case_Date ASC, hearing_Case_Time ASC";

            using var connection = new MySqlConnection(_connectionString);
            var hearingData = (await connection.QueryAsync<Hearingexportdto>(query)).ToList();

            // Check if hearingData is empty and return an appropriate response
            if (!hearingData.Any())
            {
                return BadRequest("Cannot export report. No hearing records found in the database.");
            }

            // Create Excel workbook
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Hearing Schedule");

            // Header styling
            worksheet.Cell("A1").Value = "HEARING SCHEDULE REPORT";
            worksheet.Range("A1:F1").Merge().Style.Font.SetBold(true).Font.SetFontSize(14)
                      .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            worksheet.Cell("A2").Value = $"Date Exported: {philippineNow:MM/dd/yyyy hh:mm tt}";
            worksheet.Range("A2:F2").Merge().Style.Font.SetItalic(true)
                      .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            var headers = new[] { "Case Title", "Case Number", "Judge", "Trial Prosecutor", "Branch Clerk", "Public Attorney", "Court Interpreter", "Court Stenographer", "Status", "Hearing Date", "Hearing Time" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(4, i + 1).Value = headers[i];
                worksheet.Cell(4, i + 1).Style.Font.SetBold(true)
                     .Fill.SetBackgroundColor(XLColor.LightGray)
                     .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }

            int row = 5;
            foreach (var record in hearingData)
            {
                worksheet.Cell(row, 1).Value = record.HearingCaseTitle ?? "N/A";
                worksheet.Cell(row, 2).Value = record.HearingCaseNumber ?? "N/A";
                worksheet.Cell(row, 3).Value = record.HearingJudge ?? "N/A";
                worksheet.Cell(row, 4).Value = record.HearingTrialProsecutor ?? "N/A";
                worksheet.Cell(row, 5).Value = record.HearingBranchClerk ?? "N/A";
                worksheet.Cell(row, 6).Value = record.HearingPublicAttorney ?? "N/A";
                worksheet.Cell(row, 7).Value = record.HearingCourtInterpreter ?? "N/A";
                worksheet.Cell(row, 8).Value = record.HearingCourtStenographer ?? "N/A";
                worksheet.Cell(row, 9).Value = record.HearingCaseStatus ? "Completed" : "Pending";
                worksheet.Cell(row, 10).Value = record.HearingCaseDate.ToString("yyyy-MM-dd");  // Format DateTime properly
                worksheet.Cell(row, 11).Value = record.HearingCaseTime.ToString(@"hh\:mm\:ss");  // Format TimeSpan properly
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

            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Hearing_Report_{DateTime.Now:yyyyMMdd}.xlsx");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting hearing data: {ex.Message}");
            return StatusCode(500, $"Error exporting hearing data: {ex.Message}");
        }
    }




}
