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
public class MarriageController : ControllerBase
{
    private readonly string _connectionString;

    public MarriageController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }
    //add
    [HttpPost("AddMarriage")]
    public async Task<IActionResult> AddMarriage([FromBody] Marriagedto marriage)
    {
        if (marriage == null)
            return BadRequest("Invalid marriage data.");

        DateTime philippineTime = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time")
        );


        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();
        var username = HttpContext.Session.GetString("UserName");
        try
        {
            string insertQuery = @"
        INSERT INTO Marriage (
            marriage_OCC,
            marriage_branch,
            marriage_brideLastname,
            marriage_brideFirstname,
            marriage_brideMiddlename,
            marriage_groomlastname,
            marriage_groomFirstname,
            marriage_groomMiddlename,
            marriage_judge,
            marriage_inputted,
            marriage_startIn,
            marriage_notifyme,
            marriage_checkbox
        ) VALUES (
            @OCC,
            @Branch,
            @BrideLastName,
            @BrideFirstName,
            @BrideMiddleName,
            @GroomLastName,
            @GroomFirstName,
            @GroomMiddleName,
            @Judge,
            @Inputted,
            @StartIn,
            @NotifyMe,
            @Checkbox
        )";

            int rowsAffected = await con.ExecuteAsync(insertQuery, new
            {
                OCC = marriage.MarriageOCC,
                Branch = marriage.MarriageBranch,
                BrideLastName = marriage.MarriageBrideLastName?.Trim(),
                BrideFirstName = marriage.MarriageBrideFirstName?.Trim(),
                BrideMiddleName = marriage.MarriageBrideMiddleName?.Trim(),
                GroomLastName = marriage.MarriageGroomLastName?.Trim(),
                GroomFirstName = marriage.MarriageGroomFirstName?.Trim(),
                GroomMiddleName = marriage.MarriageGroomMiddleName?.Trim(),
                Judge = marriage.MarriageJudge?.Trim(),
                Inputted = philippineTime,
                StartIn = marriage.MarriageStartIn,
                NotifyMe = marriage.NotifyMe ? 1 : 0,
                Checkbox = marriage.Checkbox ? 1 : 0,
            });

            if (rowsAffected == 0)
            {
                return StatusCode(500, "Marriage record insertion failed.");
            }

            await Logger.LogAction(HttpContext, "INSERT", "Marriage", 0,
                $"Marriage record for {marriage.MarriageBrideFirstName} and {marriage.MarriageGroomFirstName} added.");

            return Ok(new { message = "Marriage record added successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    //update
    [HttpPut("UpdateMarriage/{marriageId}")]
    public async Task<IActionResult> UpdateMarriage(int marriageId, [FromBody] Marriagedto marriage)
    {
        const string updateQuery = @"
            UPDATE Marriage
            SET
                marriage_OCC = @OCC,
                marriage_branch = @Branch,
                marriage_brideLastname = @BrideLastName,
                marriage_brideFirstname = @BrideFirstName,
                marriage_brideMiddlename = @BrideMiddleName,
                marriage_groomlastname = @GroomLastName,
                marriage_groomFirstname = @GroomFirstName,
                marriage_groomMiddlename = @GroomMiddleName,
                marriage_judge = @Judge,
                marriage_startIn = @StartIn,
                marriage_notifyme = @NotifyMe,
                marriage_checkbox = @Checkbox
            WHERE marriage_Id = @MarriageId";

        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var username = HttpContext.Session.GetString("UserName");

                // Check if marriage exists
                var checkQuery = "SELECT marriage_Id FROM Marriage WHERE marriage_Id = @MarriageId";
                var existingMarriage = await connection.QueryFirstOrDefaultAsync<int>(checkQuery, new { MarriageId = marriageId });

                if (existingMarriage == 0)
                {
                    return NotFound($"Marriage record with ID {marriageId} not found.");
                }

                // Get Philippine time (UTC+8)
                DateTime philippineTime = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow,
                    TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time")
                );

                // Get current values with proper column mapping
                var originalMarriage = await connection.QueryFirstOrDefaultAsync<Marriagedto>(
                    @"SELECT 
            marriage_Id AS MarriageId,
            marriage_OCC AS MarriageOCC,
            marriage_branch AS MarriageBranch,
            marriage_brideLastname AS MarriageBrideLastName,
            marriage_brideFirstname AS MarriageBrideFirstName,
            marriage_brideMiddlename AS MarriageBrideMiddleName,
            marriage_groomlastname AS MarriageGroomLastName,
            marriage_groomFirstname AS MarriageGroomFirstName,
            marriage_groomMiddlename AS MarriageGroomMiddleName,
            marriage_judge AS MarriageJudge,
            marriage_startIn AS MarriageStartIn,
            marriage_notifyme AS NotifyMe,
            marriage_checkbox AS Checkbox
        FROM Marriage 
        WHERE marriage_Id = @MarriageId",
                    new { MarriageId = marriageId });

                if (originalMarriage == null)
                {
                    await Logger.LogAction(HttpContext,
                        action: "UPDATE_ERROR",
                        tableName: "Marriage",
                        recordId: marriageId,
                        details: $"Original marriage record {marriageId} not found during update"
                    );
                    return NotFound($"Marriage record {marriageId} not found.");
                }

                // Update marriage
                await connection.ExecuteAsync(updateQuery, new
                {
                    OCC = marriage.MarriageOCC,
                    Branch = marriage.MarriageBranch,
                    BrideLastName = marriage.MarriageBrideLastName?.Trim(),
                    BrideFirstName = marriage.MarriageBrideFirstName?.Trim(),
                    BrideMiddleName = marriage.MarriageBrideMiddleName?.Trim(),
                    GroomLastName = marriage.MarriageGroomLastName?.Trim(),
                    GroomFirstName = marriage.MarriageGroomFirstName?.Trim(),
                    GroomMiddleName = marriage.MarriageGroomMiddleName?.Trim(),
                    Judge = marriage.MarriageJudge?.Trim(),
                    StartIn = marriage.MarriageStartIn,
                    NotifyMe = marriage.NotifyMe,
                    Checkbox = marriage.Checkbox,
                    MarriageId = marriageId
                });

                // Track changes
                List<string> changes = new List<string>();

                if (originalMarriage.Checkbox != marriage.Checkbox)
                {
                    if (marriage.Checkbox)
                    {
                        changes.Add("Checkbox: It's finished");
                    }
                    else
                    {
                        changes.Add("Checkbox: It's changed to active");
                    }
                }

                if (originalMarriage.NotifyMe != marriage.NotifyMe)
                {
                    if (marriage.NotifyMe)
                    {
                        changes.Add("Notification enabled");
                    }
                    else
                    {
                        changes.Add("Notification disabled");
                    }
                }

                if (originalMarriage.MarriageStartIn != marriage.MarriageStartIn)
                {
                    changes.Add($"Start In: \"{originalMarriage.MarriageStartIn:yyyy-MM-dd HH:mm:ss}\" → \"{marriage.MarriageStartIn:yyyy-MM-dd HH:mm:ss}\"");
                }

                if (changes.Count > 0)
                {
                    await Logger.LogAction(HttpContext,
                        action: "UPDATE",
                        tableName: "Marriage",
                        recordId: marriageId,
                        details: $"Updated marriage record (ID: {marriageId}). Changes: {string.Join(", ", changes)}"
                    );
                }

                return Ok(new { Message = $"Marriage record with ID {marriageId} updated successfully.", Changes = changes });
            }
        }
        catch (Exception ex)
        {
            

            return StatusCode(500, new
            {
                Message = "An error occurred while updating the marriage record.",
                ErrorDetails = ex.Message
            });
        }
    }


    //eto naman is para sa notifications to pag pinidot
    [HttpPut("UpdateMarriageCheckbox/{marriageId}")]
    public async Task<IActionResult> UpdateMarriageCheckbox(int marriageId, [FromBody] bool checkboxStatus)
    {
        const string updateQuery = @"
        UPDATE Marriage
        SET marriage_checkbox = @Checkbox
        WHERE marriage_Id = @MarriageId";

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            // Check if marriage exists
            var checkQuery = "SELECT marriage_Id FROM Marriage WHERE marriage_Id = @MarriageId";
            var existingMarriage = await connection.QueryFirstOrDefaultAsync<int>(checkQuery, new { MarriageId = marriageId });

            if (existingMarriage == 0)
            {
                return NotFound($"Marriage record with ID {marriageId} not found.");
            }

            // Update marriage checkbox (only checkbox field)
            var rowsAffected = await connection.ExecuteAsync(updateQuery, new
            {
                Checkbox = checkboxStatus ? 1 : 0, // Convert boolean to 1 or 0
                MarriageId = marriageId
            });

            if (rowsAffected == 0)
            {
                return StatusCode(500, "Marriage checkbox update failed.");
            }

            // Log the action
            string status = checkboxStatus ? "Finished" : "Active";
            await Logger.LogAction(HttpContext,
                action: "UPDATE",
                tableName: "Marriage",
                recordId: marriageId,
                details: $"Marriage record (ID: {marriageId}) checkbox updated to '{status}'"
            );

            return Ok(new { Message = $"Marriage checkbox status updated to '{status}' for record ID {marriageId}." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "An error occurred while updating the marriage checkbox.", ErrorDetails = ex.Message });
        }
    }


    //eto sa dashboard ata
    [HttpGet("CountMarriage")]
    public async Task<IActionResult> CountTasks()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT COUNT(*) FROM Marriage";
        int count = await con.ExecuteScalarAsync<int>(query);
        return Ok(count);
    }


    //eto sa datagridview
    [HttpGet("GetAllMarriages")]
    public async Task<IActionResult> GetAllMarriages()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();
        string query = @"
        SELECT 
            marriage_OCC AS MarriageOCC,
            marriage_branch AS MarriageBranch,
            marriage_brideLastname AS MarriageBrideLastName,
            marriage_brideFirstname AS MarriageBrideFirstName,
            marriage_brideMiddlename AS MarriageBrideMiddleName,
            marriage_groomlastname AS MarriageGroomLastName,
            marriage_groomFirstname AS MarriageGroomFirstName,
            marriage_groomMiddlename AS MarriageGroomMiddleName,
            marriage_judge AS MarriageJudge,
            marriage_startIn AS MarriageStartIn,
            marriage_notifyme AS NotifyMe,
            marriage_checkbox AS Checkbox
        FROM Marriage";

        using var cmd = new MySqlCommand(query, con);
        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            var marriages = new List<object>();  // Use anonymous object instead of Marriagedto
            while (await reader.ReadAsync())
            {
                marriages.Add(new
                {
                    MarriageOCC = Convert.ToDateTime(reader["MarriageOCC"]),
                    MarriageBranch = Convert.ToDateTime(reader["MarriageBranch"]),
                    MarriageBrideLastName = reader["MarriageBrideLastName"].ToString(),
                    MarriageBrideFirstName = reader["MarriageBrideFirstName"].ToString(),
                    MarriageBrideMiddleName = reader["MarriageBrideMiddleName"].ToString(),
                    MarriageGroomLastName = reader["MarriageGroomLastName"].ToString(),
                    MarriageGroomFirstName = reader["MarriageGroomFirstName"].ToString(),
                    MarriageGroomMiddleName = reader["MarriageGroomMiddleName"].ToString(),
                    MarriageJudge = reader["MarriageJudge"].ToString(),
                    MarriageStartIn = Convert.ToDateTime(reader["MarriageStartIn"]),
                    NotifyMe = Convert.ToBoolean(reader["NotifyMe"]),
                    Status = Convert.ToBoolean(reader["Checkbox"]) ? "Finished" : "Active"  // Modify Checkbox to display status
                });
            }
            return Ok(marriages);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }



}
