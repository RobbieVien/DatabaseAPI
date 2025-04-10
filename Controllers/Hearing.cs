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

            await Logger.LogAction(HttpContext, $"Hearing {hearing.HearingCaseTitle} has been added.", "Hearing", 0);
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
                         DATE_FORMAT(hearing_Case_Inputted, '%Y-%m-%d %H:%i:%s') AS hearing_Case_Inputted, 
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

        var hearings = new List<Hearingdto>();
        while (await reader.ReadAsync())
        {
            hearings.Add(new Hearingdto
            {
                HearingId = Convert.ToInt32(reader["hearing_Id"]),
                HearingCaseTitle = reader["hearing_Case_Title"]?.ToString(),
                HearingCaseNumber = reader["hearing_Case_Num"]?.ToString(),
                HearingCaseDate = reader["hearing_Case_Date"] != DBNull.Value ? DateOnly.Parse(reader["hearing_Case_Date"].ToString()) : DateOnly.MinValue,
                HearingCaseTime = reader["hearing_Case_Time"] != DBNull.Value
                    ? TimeOnly.FromTimeSpan((TimeSpan)reader["hearing_Case_Time"])
                    : TimeOnly.MinValue,
                HearingCaseInputted = reader["hearing_Case_Inputted"]?.ToString() ?? string.Empty,
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
    /*
    [HttpGet("export-hearing")]
    public async Task<IActionResult> ExportHearingReport()
    {
        try
        {
            string query = @" 
                                     SELECT 
                hearing_Id AS HearingId, 
                hearing_Case_Title AS HearingCaseTitle, 
                hearing_Case_Num AS HearingCaseNumber, 
                DATE_FORMAT(hearing_Case_Date, '%Y-%m-%d') AS HearingCaseDate, 
                TIME_FORMAT(hearing_Case_Time, '%H:%i:%s') AS HearingCaseTime, 
                DATE_FORMAT(hearing_Case_Inputted, '%Y-%m-%d %H:%i:%s') AS HearingCaseInputted, 
                hearing_case_status AS HearingCaseStatus,
                CASE 
                    WHEN CONCAT(hearing_Case_Date, ' ', hearing_Case_Time) >= CONVERT_TZ(NOW(), 'UTC', 'Asia/Manila') THEN 0
                    ELSE 1
                END AS is_past
            FROM Hearing
            ORDER BY is_past ASC, 
                     CASE WHEN is_past = 0 THEN hearing_Case_Date END ASC, 
                     CASE WHEN is_past = 0 THEN hearing_Case_Time END ASC,
                     CASE WHEN is_past = 1 THEN hearing_Case_Date END DESC, 
                     CASE WHEN is_past = 1 THEN hearing_Case_Time END DESC";


            using var connection = new MySqlConnection(_connectionString);

            // After your database connection but before mapping to DTO
            var rawData = await connection.QueryAsync(query);
            Console.WriteLine("Raw database results:");
            foreach (var item in rawData)
            {
                // Cast to IDictionary to safely access the properties
                var rowDict = item as IDictionary<string, object>;
                if (rowDict != null)
                {
                    Console.WriteLine("Row data:");
                    foreach (var kvp in rowDict)
                    {
                        Console.WriteLine($"  {kvp.Key}: {kvp.Value ?? "null"}");
                    }
                }
                else
                {
                    Console.WriteLine("Could not convert row to dictionary");
                }
            }
            var hearingData = (await connection.QueryAsync<Hearingdto>(query)).ToList();

            Console.WriteLine($"Retrieved {hearingData.Count} hearing records.");

            if (!hearingData.Any())
            {
                return NotFound("No hearing records found.");
            }

            foreach (var record in hearingData)
            {
                Console.WriteLine($"Case Title: {record.HearingCaseTitle}, Case Number: {record.HearingCaseNumber}, " +
                                  $"Date: {record.HearingCaseDate}, Time: {record.HearingCaseTime}, " +
                                  $"Status: {record.HearingCaseStatus}, Inputted: {record.HearingCaseInputted}");
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Hearing Schedule");
            worksheet.Cell("A1").Value = "HEARING SCHEDULE REPORT";
            worksheet.Range("A1:F1").Merge().Style.Font.SetBold(true).Font.SetFontSize(14).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            worksheet.Cell("A2").Value = $"Date Exported: {DateTime.Now:MM/dd/yyyy hh:mm tt}";
            worksheet.Range("A2:F2").Merge().Style.Font.SetItalic(true).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            var headers = new[] { "Case Title", "Case Number", "Hearing Date", "Hearing Time", "Status", "Date Inputted" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(4, i + 1).Value = headers[i];
                worksheet.Cell(4, i + 1).Style.Font.SetBold(true).Fill.SetBackgroundColor(XLColor.LightGray).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }

            int row = 5;
            foreach (var record in hearingData)
            {
                worksheet.Cell(row, 1).Value = record.HearingCaseTitle ?? "N/A";
                worksheet.Cell(row, 2).Value = record.HearingCaseNumber ?? "N/A";
                worksheet.Cell(row, 3).Value = !string.IsNullOrEmpty(record.HearingCaseDate) ? record.HearingCaseDate : "N/A";
                worksheet.Cell(row, 4).Value = !string.IsNullOrEmpty(record.HearingCaseTime) ? record.HearingCaseTime : "N/A";
                worksheet.Cell(row, 5).Value = record.HearingCaseStatus ? "Completed" : "Pending";
                worksheet.Cell(row, 6).Value = !string.IsNullOrEmpty(record.HearingCaseInputted) ? record.HearingCaseInputted : "N/A";
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
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Hearing_Report_{DateTime.Now:yyyyMMdd}.xlsx");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting hearing data: {ex.Message}");
            return StatusCode(500, $"Error exporting hearing data: {ex.Message}");
        }
    }
    */

}
