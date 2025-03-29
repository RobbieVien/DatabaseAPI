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
using ClosedXML.Excel;

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
        CaseCount
    FROM Report";

        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                var reports = await connection.QueryAsync<GetReportDto>(query);

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

    [HttpGet("export-reports")]
    public async Task<IActionResult> ExportReports()
    {
        try
        {
            string query = @"
        SELECT 
            Report_Id AS Report_Id,
            Report_NatureCase AS Report_NatureCase,
            CaseCount
        FROM Report
        ORDER BY Report_Id DESC";

            using var connection = new MySqlConnection(_connectionString);
            var reportData = (await connection.QueryAsync<ReportDto>(query)).ToList();

            Console.WriteLine($"Retrieved {reportData.Count} report records.");

            if (!reportData.Any())
            {
                return NotFound("No report records found.");
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Report Data");

            // Header formatting
            worksheet.Cell("A1").Value = "REPORT DATA EXPORT";
            worksheet.Range("A1:B1").Merge().Style.Font.SetBold(true).Font.SetFontSize(14).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            // Properly formatted date exported
            worksheet.Cell("A2").Value = $"Date Exported: {DateTime.Now:MMMM dd, yyyy hh:mm tt}";
            worksheet.Range("A2:B2").Merge().Style.Font.SetItalic(true).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            // Column headers
            var headers = new[] { "Nature of Case", "Case Count" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(4, i + 1).Value = headers[i];
                worksheet.Cell(4, i + 1).Style.Font.SetBold(true).Fill.SetBackgroundColor(XLColor.LightGray).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }

            // Data population (excluding hidden columns)
            int row = 5;
            foreach (var record in reportData)
            {
                worksheet.Cell(row, 1).Value = record.Report_NatureCase ?? "N/A";
                worksheet.Cell(row, 2).Value = record.CaseCount;
                row++;
            }

            // Column adjustments
            worksheet.Columns().AdjustToContents();
            for (int i = 1; i <= headers.Length; i++)
            {
                if (worksheet.Column(i).Width < 15)
                    worksheet.Column(i).Width = 15;
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Report_Data_{DateTime.Now:yyyyMMdd}.xlsx");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting report data: {ex.Message}");
            return StatusCode(500, $"Error exporting report data: {ex.Message}");
        }
    }






}
