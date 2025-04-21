using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Dapper;
using System.Collections.Generic;
using DatabaseAPI.Models;
using DatabaseAPI.Utilities;
using System.Data;

[Route("api/[controller]")]
[ApiController]
public class CourtRecordController : ControllerBase
{
    private readonly string _connectionString;

    public CourtRecordController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }

    [HttpPost("AddCourtRecord")]
    public async Task<IActionResult> AddCourtRecord(
     [FromBody] NewAddCourtRecorddto courtrecord,
     [FromHeader(Name = "UserName")] string userName = "System")
    {
        if (courtrecord == null || string.IsNullOrWhiteSpace(courtrecord.RecordCaseNumber))
        {
            return BadRequest("Invalid court record data.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        try
        {
            // Get current Philippine time
            var philippinesTime = TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"));
            var currentYear = philippinesTime.Year;
            var dateInputted = philippinesTime; // rec_DateTime_Inputted

            // Append " -M-YYYY" to the case number
            string modifiedCaseNumber = $"{courtrecord.RecordCaseNumber.Trim()} -M-{currentYear}";

            // Check if case number already exists
            var duplicateCount = await con.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM COURTRECORD WHERE rec_Case_Number = @CaseNumber",
                new { CaseNumber = modifiedCaseNumber });

            if (duplicateCount > 0)
            {
                return Conflict("A court record with the same case number already exists.");
            }

            // Prepare query
            string insertQuery = @"
            INSERT INTO COURTRECORD (
                rec_Case_Number,
                rec_Case_Title,
                rec_DateTime_Inputted,
                rec_Date_Filed_Occ,
                rec_Date_Filed_Received,
                rec_Case_Status,
                rec_Republic_Act,
                rec_Nature_Descrip
            )
            VALUES (
                @CaseNumber,
                @CaseTitle,
                @RecordDateInputted,
                @RecordDateFiledOcc,
                @RecordDateFiledReceived,
                @RecordCaseStatus,
                @RecordRepublicAct,
                @RecordNatureDescription
            );
            SELECT LAST_INSERT_ID();";

            int newRecordId = await con.ExecuteScalarAsync<int>(insertQuery, new
            {
                CaseNumber = modifiedCaseNumber,
                CaseTitle = courtrecord.RecordCaseTitle,
                RecordDateInputted = dateInputted,
                RecordDateFiledOcc = courtrecord.RecordDateFiledOcc?.Date,           // DATE only
                RecordDateFiledReceived = courtrecord.RecordDateFiledReceived?.Date, // DATE only
                RecordCaseStatus = "Active",                                          // default status
                RecordRepublicAct = courtrecord.RecordRepublicAct,
                RecordNatureDescription = courtrecord.RecordNatureDescription
            });

            if (newRecordId > 0)
            {
                await Logger.LogAction(HttpContext, $"Added {courtrecord.RecordCaseTitle} successfully.", "COURTRECORD", newRecordId);
                return Ok(new { Message = $"Added {courtrecord.RecordCaseTitle} successfully.", RecordId = newRecordId });
            }
            else
            {
                return StatusCode(500, "Failed to add the court record.");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Message = "An error occurred while adding the court record.",
                ErrorDetails = ex.Message
            });
        }
    }









    [HttpPut("UpdateCourtRecord/{id}")]
    public async Task<IActionResult> UpdateCourtRecord(int id, [FromBody] UpdateCourtRecorddto courtrecord, [FromQuery] string editedBy)
    {
        if (id <= 0 || courtrecord == null || string.IsNullOrWhiteSpace(courtrecord.RecordCaseNumber) || string.IsNullOrWhiteSpace(editedBy))
        {
            return BadRequest("Invalid court record data, ID, or missing editor information.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();
        var username = HttpContext.Session.GetString("UserName");

        try
        {
            Console.WriteLine($"User '{editedBy}' is updating court record with ID: {id}");

            var existingRecord = await con.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT rec_Case_Number, rec_Case_Title, rec_Case_Status 
              FROM COURTRECORD WHERE courtRecord_Id = @Id",
                new { Id = id });

            if (existingRecord == null)
            {
                return NotFound("Court record not found.");
            }

            string oldCaseNumber = existingRecord?.rec_Case_Number ?? "";
            string oldCaseTitle = existingRecord?.rec_Case_Title ?? "";
            string oldCaseStatus = existingRecord?.rec_Case_Status ?? "";

            var duplicateCount = await con.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM COURTRECORD WHERE rec_Case_Number = @CaseNumber AND courtRecord_Id != @Id",
                new { CaseNumber = courtrecord.RecordCaseNumber.Trim(), Id = id });

            if (duplicateCount > 0)
            {
                return Conflict("Another court record with the same case number already exists.");
            }

            // Get the latest hearing case date where hearing_Case_Num matches rec_Case_Number
            DateTime? nextHearingDate = await con.QueryFirstOrDefaultAsync<DateTime?>(
                "SELECT hearing_Case_Date FROM Hearing WHERE hearing_Case_Num = @CaseNumber ORDER BY hearing_Case_Date DESC LIMIT 1",
                new { CaseNumber = courtrecord.RecordCaseNumber.Trim() });

            string updateQuery = @"UPDATE COURTRECORD 
                               SET rec_Case_Number = @CaseNumber,
                                   rec_Case_Title = @CaseTitle,
                                   rec_Case_Status = @RecordCaseStatus,
                                   rec_Republic_Act = @RecordRepublicAct,
                                   rec_Nature_Descrip = @RecordNatureDescription,
                                   rec_Transferred = @RecordTransfer,
                                   rec_Date_Filed_Occ = @RecordDateFiledOCC,
                                   rec_Date_Filed_Received = @RecordDateFiledReceived,
                                   rec_Case_Stage = @CaseStage,
                                   rec_Date_Diposal = @RecordDateDisposal,
                                   rec_Date_Archival = @RecordDateArchival,
                                   rec_Date_Revival = @RecordDateRevival,
                                   rec_Next_Hearing = @RecordNextHearing
                               WHERE courtRecord_Id = @Id";

            int rowsAffected = await con.ExecuteAsync(updateQuery, new
            {
                CaseNumber = courtrecord.RecordCaseNumber.Trim(),
                CaseTitle = courtrecord.RecordCaseTitle,
                RecordCaseStatus = courtrecord.RecordCaseStatus,
                RecordRepublicAct = courtrecord.RecordRepublicAct,
                RecordNatureDescription = courtrecord.RecordNatureDescription,
                RecordTransfer = courtrecord.RecordTransfer,

                // Ensure these dates are nullable DateTime? and converted correctly.
                RecordDateFiledOCC = courtrecord.RecordDateFiledOCC ?? (DateTime?)null,
                RecordDateFiledReceived = courtrecord.RecordDateFiledReceived ?? (DateTime?)null,
                CaseStage = courtrecord.CaseStage,

                RecordDateDisposal = courtrecord.RecordDateDisposal ?? (DateTime?)null,
                RecordDateArchival = courtrecord.RecordDateArchival ?? (DateTime?)null,
                RecordDateRevival = courtrecord.RecordDateRevival ?? (DateTime?)null,

                // Handle nullable DateTime for the next hearing
                RecordNextHearing = nextHearingDate ?? (DateTime?)null,

                Id = id
            });

            List<string> changes = new();
            if (!string.Equals(oldCaseNumber, courtrecord.RecordCaseNumber, StringComparison.Ordinal))
                changes.Add($"Case Number changed from '{oldCaseNumber}' to '{courtrecord.RecordCaseNumber}'");

            if (!string.Equals(oldCaseTitle, courtrecord.RecordCaseTitle, StringComparison.Ordinal))
                changes.Add($"Case Title changed from '{oldCaseTitle}' to '{courtrecord.RecordCaseTitle}'");

            if (!string.Equals(oldCaseStatus, courtrecord.RecordCaseStatus, StringComparison.Ordinal))
                changes.Add($"Case Status changed from '{oldCaseStatus}' to '{courtrecord.RecordCaseStatus}'");

            string details = changes.Count > 0 ? string.Join("; ", changes) : "No significant changes";

            if (rowsAffected > 0)
            {
                await Logger.LogAction(HttpContext, "Updated Court Record", "COURTRECORD", id, details);

                return Ok(new { Message = $"Court record with ID {id} updated successfully by {editedBy}.", Details = details });
            }
            else
            {
                return StatusCode(500, "Failed to update the court record.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating court record: {ex.Message}");
            return StatusCode(500, new { Message = "An error occurred while updating the court record.", ErrorDetails = ex.Message });
        }
    }






    [HttpDelete("DeleteCourtRecord/{id}")]
    public async Task<IActionResult> DeleteCourtRecord(
    int id,
    [FromHeader(Name = "UserName")] string userName)
    {
        if (id <= 0)
        {
            return BadRequest("Invalid court record ID.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();
        var username = HttpContext.Session.GetString("UserName");

        try
        {
            // Retrieve the court record details before deletion
            string selectQuery = @"SELECT 
            courtRecord_Id AS CourtRecordId,
            rec_Case_Number AS RecordCaseNumber,
            rec_Case_Title AS RecordCaseTitle,
            rec_Nature_Case AS RecordNatureCase,
            rec_Case_Status AS RecordCaseStatus
        FROM COURTRECORD WHERE courtRecord_Id = @Id";

            var courtRecord = await con.QueryFirstOrDefaultAsync<CourtRecorddto>(selectQuery, new { Id = id });

            if (courtRecord == null)
            {
                Console.WriteLine($"No court record found for ID: {id}");
                return NotFound($"Court record with ID {id} not found.");
            }

            // Delete the court record
            string deleteQuery = "DELETE FROM COURTRECORD WHERE courtRecord_Id = @Id";
            int rowsAffected = await con.ExecuteAsync(deleteQuery, new { Id = id });

            if (rowsAffected > 0)
            {
                Console.WriteLine($"Court record {id} deleted successfully by {userName}.");

                // Collect deletion details
                string details = $"Deleted court record (ID: {id}): " +
                                 $"Case Number: \"{courtRecord.RecordCaseNumber}\", " +
                                 $"Title: \"{courtRecord.RecordCaseTitle}\", " +
                                 $"Nature Case: \"{courtRecord.RecordNatureCase}\", " +
                                 $"Status: \"{courtRecord.RecordCaseStatus}\"";

                await Logger.LogAction(HttpContext, "Deleted Court Record", "CourtRecord", id, details);

                return Ok(new
                {
                    Message = "Court record deleted successfully.",
                    DeletedData = courtRecord
                });
            }

            return StatusCode(500, "An error occurred while deleting the court record.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting court record: {ex.Message}");
            return StatusCode(500, new { Message = "An error occurred while deleting the court record.", ErrorDetails = ex.Message });
        }
    }


    //COUNT-ACTIVE-CASE-RECORDS
    [HttpGet("CountCaseRecordsActive")]
    public async Task<IActionResult> CountCaseRecords()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT COUNT(*) FROM COURTRECORD WHERE rec_Case_Status IN ('Active', 'Archived')";
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

    //COUNT-DISPOSED-CASE-RECORDS
    [HttpGet("CountCaseRecordsDisposed")]
    public async Task<IActionResult> CountCaseRecordsDisposed()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT COUNT(*) FROM COURTRECORD WHERE rec_Case_Status IN ('Disposed')";
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

    //COUNT-ARCHIVED-CASE-RECORDS
    [HttpGet("CountCaseRecordsArchived")]
    public async Task<IActionResult> CountCaseRecordsArchived()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT COUNT(*) FROM COURTRECORD WHERE rec_Case_Status IN ('Archived')";
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

    //for Datagridview
    [HttpGet("GetAllRecords")]
    public async Task<ActionResult<IEnumerable<GetAllCourtRecorddto>>> GetAllRecords()
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

        string query = @"
    SELECT 
        rec_Case_Number, 
        rec_Case_Title, 
        rec_DateTime_Inputted,
        rec_Date_Filed_Received,
        rec_Case_Status,
        rec_Republic_Act,
        rec_Nature_Descrip
    FROM COURTRECORD";

        await using var cmd = new MySqlCommand(query, con);

        try
        {
            var results = new List<GetAllCourtRecorddto>();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var dto = new GetAllCourtRecorddto
                {
                    RecordCaseNumber = reader.IsDBNull("rec_Case_Number") ? string.Empty : reader.GetString("rec_Case_Number"),
                    RecordCaseTitle = reader.IsDBNull("rec_Case_Title") ? string.Empty : reader.GetString("rec_Case_Title"),
                    RecordCaseStatus = reader.IsDBNull("rec_Case_Status") ? string.Empty : reader.GetString("rec_Case_Status"),
                    RecordRepublicAct = reader.IsDBNull("rec_Republic_Act") ? string.Empty : reader.GetString("rec_Republic_Act"),
                    RecordNatureDescription = reader.IsDBNull("rec_Nature_Descrip") ? string.Empty : reader.GetString("rec_Nature_Descrip"),
                    RecordDateInputted = reader.IsDBNull("rec_DateTime_Inputted")
                        ? string.Empty
                        : Convert.ToDateTime(reader["rec_DateTime_Inputted"]).ToString("yyyy-MM-dd"),
                    RecordDateFiledReceived = reader.IsDBNull("rec_Date_Filed_Received")
                        ? null
                        : Convert.ToDateTime(reader["rec_Date_Filed_Received"]).ToString("yyyy-MM-dd")
                };

                results.Add(dto);
            }

            return Ok(results);
        }
        catch (Exception ex)
        {
            string innerExceptionMessage = ex.InnerException?.Message ?? "No inner exception";
            return StatusCode(500, new
            {
                Message = "Error retrieving records",
                ErrorDetails = ex.Message,
                InnerException = innerExceptionMessage,
                StackTrace = ex.StackTrace
            });
        }
    }
    //  --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    //eto kailangan to para sa disposed at mga iba pa
    private async Task<ActionResult<IEnumerable<GetAllCourtRecorddto>>> GetRecordsByStatus(string caseStatus)
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


        string query = @"
    SELECT 
        rec_Case_Number, 
        rec_Case_Title, 
        rec_DateTime_Inputted,
        rec_Date_Filed_Received,
        rec_Case_Status,
        rec_Republic_Act,
        rec_Nature_Descrip
    FROM COURTRECORD
    WHERE rec_Case_Status = @caseStatus";

        await using var cmd = new MySqlCommand(query, con);
        cmd.Parameters.AddWithValue("@caseStatus", caseStatus);

        try
        {
            var results = new List<GetAllCourtRecorddto>();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var dto = new GetAllCourtRecorddto
                {
                    RecordCaseNumber = reader.IsDBNull(reader.GetOrdinal("rec_Case_Number")) ? string.Empty : reader.GetString(reader.GetOrdinal("rec_Case_Number")),
                    RecordCaseTitle = reader.IsDBNull(reader.GetOrdinal("rec_Case_Title")) ? string.Empty : reader.GetString(reader.GetOrdinal("rec_Case_Title")),
                    RecordCaseStatus = reader.IsDBNull(reader.GetOrdinal("rec_Case_Status")) ? string.Empty : reader.GetString(reader.GetOrdinal("rec_Case_Status")),
                    RecordRepublicAct = reader.IsDBNull(reader.GetOrdinal("rec_Republic_Act")) ? string.Empty : reader.GetString(reader.GetOrdinal("rec_Republic_Act")),
                    RecordNatureDescription = reader.IsDBNull(reader.GetOrdinal("rec_Nature_Descrip")) ? string.Empty : reader.GetString(reader.GetOrdinal("rec_Nature_Descrip")),
                    // Handle rec_DateTime_Inputted
                    RecordDateInputted = reader.IsDBNull(reader.GetOrdinal("rec_DateTime_Inputted"))
                        ? string.Empty
                        : Convert.ToDateTime(reader["rec_DateTime_Inputted"]).ToString("yyyy-MM-dd"),
                    // Handle rec_Date_Filed_Received
                    RecordDateFiledReceived = reader.IsDBNull(reader.GetOrdinal("rec_Date_Filed_Received"))
                        ? null
                        : Convert.ToDateTime(reader["rec_Date_Filed_Received"]).ToString("yyyy-MM-dd")
                };

                results.Add(dto);
            }

            return Ok(results);
        }
        catch (Exception ex)
        {
            string innerExceptionMessage = ex.InnerException?.Message ?? "No inner exception";
            return StatusCode(500, new
            {
                Message = "Error retrieving records by case status",
                ErrorDetails = ex.Message,
                InnerException = innerExceptionMessage,
                StackTrace = ex.StackTrace
            });
        }
    }





    [HttpGet("GetActiveRecords")]
    public async Task<ActionResult<IEnumerable<GetAllCourtRecorddto>>> GetActiveRecords()
    {
        return await GetRecordsByStatus("ACTIVE");
    }

    [HttpGet("GetArchivedRecords")]
    public async Task<ActionResult<IEnumerable<GetAllCourtRecorddto>>> GetArchivedRecords()
    {
        return await GetRecordsByStatus("ARCHIVED");
    }

    [HttpGet("GetDecidedRecords")]
    public async Task<ActionResult<IEnumerable<GetAllCourtRecorddto>>> GetDecidedRecords()
    {
        return await GetRecordsByStatus("DECIDED");
    }

    [HttpGet("GetDisposedRecords")]
    public async Task<ActionResult<IEnumerable<GetAllCourtRecorddto>>> GetDisposedRecords()
    {
        return await GetRecordsByStatus("DISPOSED");
    }

    [HttpGet("GetReviveRecords")]
    public async Task<ActionResult<IEnumerable<GetAllCourtRecorddto>>> GetReviveRecords()
    {
        return await GetRecordsByStatus("REVIVE");
    }

   














    //========================================================================
    //This is for combo box in CourtRecord
    [HttpGet("ComboBoxCategories")]
    public async Task<IActionResult> ComboBoxCategories()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT cat_republicAct FROM Category";
        using var cmd = new MySqlCommand(query, con);

        using var reader = await cmd.ExecuteReaderAsync();

        var categories = new List<CategoryRepublicActDto>();
        while (await reader.ReadAsync())
        {
            categories.Add(new CategoryRepublicActDto
            {
             
                CategoryRepublicAct = reader["cat_republicAct"]?.ToString(),
            });
        }

        return Ok(categories);
    }
}



