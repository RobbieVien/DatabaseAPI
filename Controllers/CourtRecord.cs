using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Dapper;
using System.Collections.Generic;
using DatabaseAPI.Models;
using DatabaseAPI.Utilities;

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
    public async Task<IActionResult> AddCourtRecord([FromBody] CourtRecorddto courtrecord, [FromHeader(Name = "UserName")] string userName = "System")
    {
        if (courtrecord == null || string.IsNullOrWhiteSpace(courtrecord.RecordCaseNumber))
        {
            return BadRequest("Invalid court record data.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        try
        {
            var duplicateCount = await con.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM COURTRECORD WHERE rec_Case_Number = @CaseNumber",
                new { CaseNumber = courtrecord.RecordCaseNumber.Trim() });

            if (duplicateCount > 0)
            {
                return Conflict("A court record with the same case number already exists.");
            }

            string insertQuery = @"INSERT INTO COURTRECORD (
                        rec_Case_Number,
                        rec_Case_Title,
                        rec_Date_Filed_Occ,
                        rec_Date_Filed_Received,
                        rec_Transferred,
                        rec_Case_Status,
                        rec_Nature_Case,
                        rec_Nature_Descrip,
                        rec_Time_Inputted,
                        rec_Date_Inputted)
                    VALUES (
                        @CaseNumber,
                        @CaseTitle,
                        @RecordDateFiledOcc,
                        @RecordDateFiledReceived,
                        @RecordTransferred,
                        @RecordCaseStatus,
                        @RecordNatureCase,
                        @RecordNatureDescription,
                        CURRENT_TIME(),
                        CURRENT_DATE());
                    SELECT LAST_INSERT_ID();";

            int newRecordId = await con.ExecuteScalarAsync<int>(insertQuery, new
            {
                CaseNumber = courtrecord.RecordCaseNumber.Trim(),
                CaseTitle = courtrecord.RecordCaseTitle,
                RecordDateFiledOcc = courtrecord.RecordDateFiledOCC,
                RecordDateFiledReceived = courtrecord.RecordDateFiledReceived,
                RecordTransferred = courtrecord.RecordTransfer,
                RecordCaseStatus = courtrecord.RecordCaseStatus,
                RecordNatureCase = courtrecord.RecordNatureCase,
                RecordNatureDescription = courtrecord.RecordNatureDescription
            });

            if (newRecordId > 0)
            {
                await Logger.LogAction($"Added {courtrecord.RecordCaseTitle} successfully.", "COURTRECORD", newRecordId, userName);
                return Ok(new { Message = $"Added {courtrecord.RecordCaseTitle} successfully.", RecordId = newRecordId });
            }
            else
            {
                return StatusCode(500, "Failed to add the court record.");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "An error occurred while adding the court record.", ErrorDetails = ex.Message });
        }
    }


    [HttpPut("UpdateCourtRecord/{id}")]
    public async Task<IActionResult> UpdateCourtRecord(int id, [FromBody] CourtRecorddto courtrecord, [FromQuery] string editedBy)
    {
        if (id <= 0 || courtrecord == null || string.IsNullOrWhiteSpace(courtrecord.RecordCaseNumber) || string.IsNullOrWhiteSpace(editedBy))
        {
            return BadRequest("Invalid court record data, ID, or missing editor information.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        try
        {
            Console.WriteLine($"User '{editedBy}' is updating court record with ID: {id}");

            // Fetch old values
            var existingRecord = await con.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT rec_Case_Number, rec_Case_Title, rec_Nature_Case, rec_Case_Status 
              FROM COURTRECORD WHERE courtRecord_Id = @Id",
                new { Id = id });

            if (existingRecord == null)
            {
                return NotFound("Court record not found.");
            }

            string oldCaseNumber = existingRecord?.rec_Case_Number ?? "";
            string oldCaseTitle = existingRecord?.rec_Case_Title ?? "";
            string oldNatureCase = existingRecord?.rec_Nature_Case ?? "";
            string oldCaseStatus = existingRecord?.rec_Case_Status ?? "";

            // Check for duplicate Case Number
            var duplicateCount = await con.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM COURTRECORD WHERE rec_Case_Number = @CaseNumber AND courtRecord_Id != @Id",
                new { CaseNumber = courtrecord.RecordCaseNumber.Trim(), Id = id });

            if (duplicateCount > 0)
            {
                return Conflict("Another court record with the same case number already exists.");
            }

            // Compare old and new values and build change details
            List<string> changes = new();
            if (!string.Equals(oldCaseNumber, courtrecord.RecordCaseNumber, StringComparison.Ordinal))
                changes.Add($"Case Number changed from '{oldCaseNumber}' to '{courtrecord.RecordCaseNumber}'");

            if (!string.Equals(oldCaseTitle, courtrecord.RecordCaseTitle, StringComparison.Ordinal))
                changes.Add($"Case Title changed from '{oldCaseTitle}' to '{courtrecord.RecordCaseTitle}'");

            if (!string.Equals(oldNatureCase, courtrecord.RecordNatureCase, StringComparison.Ordinal))
                changes.Add($"Nature Case changed from '{oldNatureCase}' to '{courtrecord.RecordNatureCase}'");

            if (!string.Equals(oldCaseStatus, courtrecord.RecordCaseStatus, StringComparison.Ordinal))
                changes.Add($"Case Status changed from '{oldCaseStatus}' to '{courtrecord.RecordCaseStatus}'");

            string details = changes.Count > 0 ? string.Join("; ", changes) : "No significant changes";

            // Update query (removed rec_LastEditedBy)
            string updateQuery = @"UPDATE COURTRECORD 
                               SET rec_Case_Number = @CaseNumber,
                                   rec_Case_Title = @CaseTitle,
                                   rec_Nature_Case = @RecordNatureCase,
                                   rec_Case_Status = @RecordCaseStatus
                               WHERE courtRecord_Id = @Id";

            int rowsAffected = await con.ExecuteAsync(updateQuery, new
            {
                CaseNumber = courtrecord.RecordCaseNumber.Trim(),
                CaseTitle = courtrecord.RecordCaseTitle,
                RecordNatureCase = courtrecord.RecordNatureCase,
                RecordCaseStatus = courtrecord.RecordCaseStatus,
                Id = id
            });

            if (rowsAffected > 0)
            {
                // Log changes in the Logs table
                await Logger.LogAction("Updated Court Record", "COURTRECORD", id, editedBy, details);

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

                await Logger.LogAction("Deleted Court Record", "CourtRecord", id, userName, details);

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

    //this is filter for datagridview
    [HttpGet("FilterRecords")]
    public async Task<ActionResult<IEnumerable<CourtRecorddto>>> GetFilteredRecords([FromQuery] string selectedFilter = "All")
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

        string query = selectedFilter switch
        {
            "Today" => @"
    SELECT 
        courtRecord_Id,
        rec_Case_Number, 
        rec_Case_Title, 
        rec_Date_Inputted,
        rec_Time_Inputted,
        rec_Date_Filed_Occ,
        rec_Date_Filed_Received,
        rec_Transferred,
        rec_Case_Status,
        rec_Nature_Case,
        rec_Nature_Descrip
    FROM COURTRECORD
    WHERE DATE(rec_Date_Inputted) = CURDATE()",
            _ => @"
    SELECT 
        courtRecord_Id,
        rec_Case_Number, 
        rec_Case_Title, 
        rec_Date_Inputted,
        rec_Time_Inputted,
        rec_Date_Filed_Occ,
        rec_Date_Filed_Received,
        rec_Transferred,
        rec_Case_Status,
        rec_Nature_Case,
        rec_Nature_Descrip
    FROM COURTRECORD"
        };

        await using var cmd = new MySqlCommand(query, con);

        try
        {
            var results = new List<CourtRecorddto>();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var courtRecord = new CourtRecorddto
                {
                    CourtRecordId = reader.IsDBNull(reader.GetOrdinal("courtRecord_Id")) ? 0 : reader.GetInt32(reader.GetOrdinal("courtRecord_Id")),
                    RecordCaseNumber = reader.IsDBNull(reader.GetOrdinal("rec_Case_Number")) ? string.Empty : reader.GetString(reader.GetOrdinal("rec_Case_Number")),
                    RecordCaseTitle = reader.IsDBNull(reader.GetOrdinal("rec_Case_Title")) ? string.Empty : reader.GetString(reader.GetOrdinal("rec_Case_Title")),
                    RecordTransfer = reader.IsDBNull(reader.GetOrdinal("rec_Transferred")) ? string.Empty : reader.GetString(reader.GetOrdinal("rec_Transferred")),
                    RecordCaseStatus = reader.IsDBNull(reader.GetOrdinal("rec_Case_Status")) ? string.Empty : reader.GetString(reader.GetOrdinal("rec_Case_Status")),
                    RecordNatureCase = reader.IsDBNull(reader.GetOrdinal("rec_Nature_Case")) ? string.Empty : reader.GetString(reader.GetOrdinal("rec_Nature_Case")),
                    RecordNatureDescription = reader.IsDBNull(reader.GetOrdinal("rec_Nature_Descrip")) ? string.Empty : reader.GetString(reader.GetOrdinal("rec_Nature_Descrip"))
                };

                // Handle rec_Date_Inputted as string
                try
                {
                    var dateInputted = reader.GetValue(reader.GetOrdinal("rec_Date_Inputted"));
                    if (dateInputted != DBNull.Value && dateInputted != null)
                    {
                        DateTime dt = Convert.ToDateTime(dateInputted);
                        courtRecord.RecordDateInputted = dt.ToString("yyyy-MM-dd");
                    }
                    else
                    {
                        courtRecord.RecordDateInputted = string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Date Parsing Error: {ex.Message}");
                    courtRecord.RecordDateInputted = string.Empty;
                }

                // Handle rec_Time_Inputted as string
                try
                {
                    var timeInputted = reader.GetValue(reader.GetOrdinal("rec_Time_Inputted"));
                    if (timeInputted != DBNull.Value && timeInputted != null)
                    {
                        if (timeInputted is TimeSpan timeSpan)
                        {
                            courtRecord.RecordTimeInputted = timeSpan.ToString(@"hh\:mm\:ss");
                        }
                        else if (timeInputted is DateTime dateTime)
                        {
                            courtRecord.RecordTimeInputted = dateTime.ToString("HH:mm:ss");
                        }
                        else
                        {
                            courtRecord.RecordTimeInputted = timeInputted.ToString();
                        }
                    }
                    else
                    {
                        courtRecord.RecordTimeInputted = string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Time Parsing Error: {ex.Message}");
                    courtRecord.RecordTimeInputted = string.Empty;
                }

                // Handle rec_Date_Filed_Occ as string
                try
                {
                    var dateFiledOCC = reader.GetValue(reader.GetOrdinal("rec_Date_Filed_Occ"));
                    if (dateFiledOCC != DBNull.Value && dateFiledOCC != null)
                    {
                        DateTime dt = Convert.ToDateTime(dateFiledOCC);
                        courtRecord.RecordDateFiledOCC = dt.ToString("yyyy-MM-dd");
                    }
                    else
                    {
                        courtRecord.RecordDateFiledOCC = null;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error Parsing rec_Date_Filed_Occ: {ex.Message}");
                    courtRecord.RecordDateFiledOCC = null;
                }

                // Handle rec_Date_Filed_Received as string
                try
                {
                    var dateFiledReceived = reader.GetValue(reader.GetOrdinal("rec_Date_Filed_Received"));
                    if (dateFiledReceived != DBNull.Value && dateFiledReceived != null)
                    {
                        DateTime dt = Convert.ToDateTime(dateFiledReceived);
                        courtRecord.RecordDateFiledReceived = dt.ToString("yyyy-MM-dd");
                    }
                    else
                    {
                        courtRecord.RecordDateFiledReceived = null;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error Parsing rec_Date_Filed_Received: {ex.Message}");
                    courtRecord.RecordDateFiledReceived = null;
                }

                results.Add(courtRecord);
            }

            return Ok(results);
        }
        catch (Exception ex)
        {
            string innerExceptionMessage = ex.InnerException != null ? ex.InnerException.Message : "No inner exception";
            return StatusCode(500, new
            {
                Message = "Error filtering records",
                ErrorDetails = ex.Message,
                InnerException = innerExceptionMessage,
                StackTrace = ex.StackTrace
            });
        }
    }
    //for Datagridview
    [HttpGet("GetAllRecords")]
    public async Task<ActionResult<IEnumerable<CourtRecorddto>>> GetAllRecords()
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
            courtRecord_Id,
            rec_Case_Number, 
            rec_Case_Title, 
            rec_Date_Inputted,
            rec_Time_Inputted,
            rec_Date_Filed_Occ,
            rec_Date_Filed_Received,
            rec_Transferred,
            rec_Case_Status,
            rec_Nature_Case,
            rec_Nature_Descrip
        FROM COURTRECORD";

        await using var cmd = new MySqlCommand(query, con);

        try
        {
            var results = new List<CourtRecorddto>();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var courtRecord = new CourtRecorddto
                {
                    CourtRecordId = reader.IsDBNull(reader.GetOrdinal("courtRecord_Id")) ? 0 : reader.GetInt32(reader.GetOrdinal("courtRecord_Id")),
                    RecordCaseNumber = reader.IsDBNull(reader.GetOrdinal("rec_Case_Number")) ? string.Empty : reader.GetString(reader.GetOrdinal("rec_Case_Number")),
                    RecordCaseTitle = reader.IsDBNull(reader.GetOrdinal("rec_Case_Title")) ? string.Empty : reader.GetString(reader.GetOrdinal("rec_Case_Title")),
                    RecordTransfer = reader.IsDBNull(reader.GetOrdinal("rec_Transferred")) ? string.Empty : reader.GetString(reader.GetOrdinal("rec_Transferred")),
                    RecordCaseStatus = reader.IsDBNull(reader.GetOrdinal("rec_Case_Status")) ? string.Empty : reader.GetString(reader.GetOrdinal("rec_Case_Status")),
                    RecordNatureCase = reader.IsDBNull(reader.GetOrdinal("rec_Nature_Case")) ? string.Empty : reader.GetString(reader.GetOrdinal("rec_Nature_Case")),
                    RecordNatureDescription = reader.IsDBNull(reader.GetOrdinal("rec_Nature_Descrip")) ? string.Empty : reader.GetString(reader.GetOrdinal("rec_Nature_Descrip"))
                };

                // Handle rec_Date_Inputted as string
                courtRecord.RecordDateInputted = reader.IsDBNull(reader.GetOrdinal("rec_Date_Inputted"))
                    ? string.Empty
                    : Convert.ToDateTime(reader.GetValue(reader.GetOrdinal("rec_Date_Inputted"))).ToString("yyyy-MM-dd");

                // Handle rec_Time_Inputted as string
                var timeInputted = reader.GetValue(reader.GetOrdinal("rec_Time_Inputted"));
                courtRecord.RecordTimeInputted = timeInputted switch
                {
                    TimeSpan timeSpan => timeSpan.ToString(@"hh\:mm\:ss"),
                    DateTime dateTime => dateTime.ToString("HH:mm:ss"),
                    _ => timeInputted != DBNull.Value ? timeInputted.ToString() : string.Empty
                };

                // Handle rec_Date_Filed_Occ as string
                courtRecord.RecordDateFiledOCC = reader.IsDBNull(reader.GetOrdinal("rec_Date_Filed_Occ"))
                    ? null
                    : Convert.ToDateTime(reader.GetValue(reader.GetOrdinal("rec_Date_Filed_Occ"))).ToString("yyyy-MM-dd");

                // Handle rec_Date_Filed_Received as string
                courtRecord.RecordDateFiledReceived = reader.IsDBNull(reader.GetOrdinal("rec_Date_Filed_Received"))
                    ? null
                    : Convert.ToDateTime(reader.GetValue(reader.GetOrdinal("rec_Date_Filed_Received"))).ToString("yyyy-MM-dd");

                results.Add(courtRecord);
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



