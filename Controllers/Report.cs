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
public class ReportController : ControllerBase
{
    private readonly string _connectionString;

    public ReportController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }

    //Adding Nature of Case Column
    [HttpPost("AddNatureCaseColumn")]
    public async Task<IActionResult> AddNatureCaseColumn()
    {
        string addColumnQuery = @"
        ALTER TABLE Report 
        ADD COLUMN IF NOT EXISTS Report_NatureCase NCHAR(50) NULL;";
        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.ExecuteAsync(addColumnQuery);
                return Ok("Column added successfully");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }


    //Adding Required Columns, CHECK nlang
    [HttpPost("AddRequiredColumns")]
    public async Task<IActionResult> AddRequiredColumns()
    {
        string addColumnsQuery = @"
        ALTER TABLE Report 
        ADD COLUMN IF NOT EXISTS CourtRecord_LinkId INT NULL,
        ADD COLUMN IF NOT EXISTS CaseCount INT NOT NULL DEFAULT 1;";

        string addForeignKeyQuery = @"
        ALTER TABLE Report 
        ADD CONSTRAINT FK_Report_CourtRecord
        FOREIGN KEY (CourtRecord_LinkId)
        REFERENCES COURTRECORD (courtRecord_Id);";

        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                // Add columns
                await connection.ExecuteAsync(addColumnsQuery);

                // Check if foreign key already exists
                var checkForeignKeyQuery = @"
                SELECT CONSTRAINT_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
                WHERE TABLE_NAME = 'Report' AND CONSTRAINT_NAME = 'FK_Report_CourtRecord';";
                var foreignKeyExists = await connection.QueryFirstOrDefaultAsync<string>(checkForeignKeyQuery);

                // Add foreign key if it doesn't exist
                if (foreignKeyExists == null)
                {
                    await connection.ExecuteAsync(addForeignKeyQuery);
                }

                return Ok("Required columns and constraints added successfully");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    //for Deleting Report
    [HttpDelete("DeleteReport/{reportId}")]
    public async Task<IActionResult> DeleteReport(int reportId)
    {
        const string deleteQuery = @"
    DELETE FROM Report 
    WHERE Report_Id = @ReportId";

        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                var affectedRows = await connection.ExecuteAsync(deleteQuery, new { ReportId = reportId });

                if (affectedRows == 0)
                {
                    return NotFound($"No report found with ID {reportId}");
                }

                // Log the deletion
                await Logger.LogAction(
                    action: "DELETE",
                    tableName: "Report",
                    recordId: reportId,
                    userName: User.Identity?.Name ?? "System", // Replace with actual user context
                    details: $"Deleted report with ID {reportId}"
                );

                return NoContent();
            }
        }
        catch (Exception ex)
        {
            // Log the error
            await Logger.LogAction(
                action: "DELETE_ERROR",
                tableName: "Report",
                recordId: reportId,
                userName: User.Identity?.Name ?? "System",
                details: $"Error deleting report: {ex.Message}"
            );

            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

   //This is for DataGridview
    [HttpGet("GetAllReports")]
    public async Task<IActionResult> GetAllReports()
    {
        const string query = @"
    SELECT 
        Report_Id,
        Report_NatureCase,
        CourtRecord_LinkId,
        CaseCount
    FROM Report";

        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                var reports = await connection.QueryAsync<ReportDto>(query);

                return Ok(reports);
            }
        }
        catch (Exception ex)
        {
            // Log the error
            await Logger.LogAction(
                action: "SELECT_ERROR",
                tableName: "Report",
                recordId: -1,
                userName: User.Identity?.Name ?? "System",
                details: $"Error retrieving reports: {ex.Message}"
            );

            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }
}
