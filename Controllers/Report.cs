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

//ITEXT
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.IO.Font.Constants;
using iText.IO.Font;
using iText.Kernel.Font;
using iText.Kernel.Exceptions;

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
    [HttpGet("GenerateCaseSummaryReport")]
    public async Task<IActionResult> GenerateCaseSummaryReport()
    {
        string query = @"
    SELECT 
        c.cat_NatureCase AS NatureOfCases,
        SUM(CASE WHEN cr.rec_Case_Status = 'ACTIVE' THEN 1 ELSE 0 END) AS Active,
        SUM(CASE WHEN cr.rec_Case_Status = 'DISPOSED' THEN 1 ELSE 0 END) AS Disposed,
        SUM(CASE WHEN cr.rec_Case_Status = 'ARCHIVED' THEN 1 ELSE 0 END) AS Archived,
        SUM(CASE WHEN cr.rec_Case_Status = 'DECIDED' THEN 1 ELSE 0 END) AS Decided,
        COUNT(*) AS TotalCase
    FROM COURTRECORD cr
    JOIN Category c ON cr.rec_Nature_Descrip = c.cat_NatureCase
    GROUP BY c.cat_NatureCase
    ORDER BY c.cat_NatureCase;";

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            var report = await connection.QueryAsync(query);
            return Ok(report);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Message = "Failed to generate report.",
                Error = ex.Message
            });
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

                await Logger.LogAction(
                       HttpContext,
                       action: "DELETE",
                       tableName: "Report",
                       recordId: reportId,
                       details: $"Deleted report with ID {reportId}"
                   );

                return NoContent();
            }
        }
        catch (Exception ex)
        {
            // Log the error
            await Logger.LogAction(
                HttpContext,
                action: "DELETE_ERROR",
                tableName: "Report",
                recordId: reportId,
                details: $"Error deleting report: {ex.Message}"
            );

            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

   //This is for WAG Muna Kunin to! wala lang to
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
                HttpContext,
                action: "SELECT_ERROR",
                tableName: "Report",
                recordId: -1,
                details: $"Error retrieving reports: {ex.Message}"
            );

            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    [HttpGet("CriminalCaseGenerateCaseSummaryReport")]
    public async Task<IActionResult> CriminalCaseGenerateCaseSummaryReport()
    {
        string query = @"
        SELECT 
            c.cat_NatureCase AS NatureOfCases,
            SUM(CASE WHEN cr.rec_Case_Status = 'ACTIVE' THEN 1 ELSE 0 END) AS Active,
            SUM(CASE WHEN cr.rec_Case_Status = 'DISPOSED' THEN 1 ELSE 0 END) AS Disposed,
            SUM(CASE WHEN cr.rec_Case_Status = 'ARCHIVED' THEN 1 ELSE 0 END) AS Archived,
            SUM(CASE WHEN cr.rec_Case_Status = 'DECIDED' THEN 1 ELSE 0 END) AS Decided,
            COUNT(*) AS TotalCase
        FROM COURTRECORD cr
        JOIN Category c ON cr.rec_Nature_Descrip = c.cat_NatureCase
        WHERE c.cat_LegalCase = 'Criminal Case'
        GROUP BY c.cat_NatureCase
        ORDER BY c.cat_NatureCase;";

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            var report = await connection.QueryAsync(query);
            return Ok(report);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Message = "Failed to generate criminal case report.",
                Error = ex.Message
            });
        }
    }

    [HttpGet("CivilCaseGenerateCaseSummaryReport")]
    public async Task<IActionResult> CivilCaseGenerateCaseSummaryReport()
    {
        string query = @"
        SELECT 
            c.cat_NatureCase AS NatureOfCases,
            SUM(CASE WHEN cr.rec_Case_Status = 'ACTIVE' THEN 1 ELSE 0 END) AS Active,
            SUM(CASE WHEN cr.rec_Case_Status = 'DISPOSED' THEN 1 ELSE 0 END) AS Disposed,
            SUM(CASE WHEN cr.rec_Case_Status = 'ARCHIVED' THEN 1 ELSE 0 END) AS Archived,
            SUM(CASE WHEN cr.rec_Case_Status = 'DECIDED' THEN 1 ELSE 0 END) AS Decided,
            COUNT(*) AS TotalCase
        FROM COURTRECORD cr
        JOIN Category c ON cr.rec_Nature_Descrip = c.cat_NatureCase
        WHERE c.cat_LegalCase = 'Civil Case'
        GROUP BY c.cat_NatureCase
        ORDER BY c.cat_NatureCase;";

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            var report = await connection.QueryAsync(query);
            return Ok(report);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Message = "Failed to generate civil case report.",
                Error = ex.Message
            });
        }
    }



    [HttpGet("export-reports-pdf")]
    public async Task<IActionResult> ExportReportsPdf()
    {
        try
        {
            string query = @"
            SELECT 
                c.cat_NatureCase AS NatureCase,
                SUM(CASE WHEN cr.rec_Case_Status = 'Active' THEN 1 ELSE 0 END) AS ActiveCount,
                SUM(CASE WHEN cr.rec_Case_Status = 'Disposed' THEN 1 ELSE 0 END) AS DisposedCount,
                SUM(CASE WHEN cr.rec_Case_Status = 'Decided' THEN 1 ELSE 0 END) AS DecidedCount,
                SUM(CASE WHEN cr.rec_Case_Status = 'Archived' THEN 1 ELSE 0 END) AS ArchivedCount
            FROM COURTRECORD cr
            INNER JOIN Category c ON cr.rec_Nature_Descrip = c.cat_NatureCase
            GROUP BY c.cat_NatureCase
            ORDER BY c.cat_NatureCase;";

            using var connection = new MySqlConnection(_connectionString);
            var reportData = (await connection.QueryAsync<dynamic>(query)).ToList();

            if (!reportData.Any())
                return NotFound("No report data found.");

            using var memoryStream = new MemoryStream();
            using (var writer = new PdfWriter(memoryStream))
            using (var pdf = new PdfDocument(writer))
            {
                var document = new Document(pdf);

                // Fonts
                PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
                PdfFont boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                PdfFont italicFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_OBLIQUE);

                // Title
                var title = new Paragraph("Case Reports Summary")
                    .SetFont(boldFont)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontSize(16);
                document.Add(title);

                // Subtitle
                var subtitle = new Paragraph($"Exported on {DateTime.Now:MMMM dd, yyyy hh:mm tt}")
                    .SetFont(italicFont)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontSize(10);
                document.Add(subtitle);

                // Spacer
                document.Add(new Paragraph("\n"));

                // Table with headers (6 columns now)
                var table = new Table(6, true);

                string[] headers = { "Nature of Case", "Active", "Disposed", "Decided", "Archived", "Total Cases" };
                foreach (var header in headers)
                {
                    table.AddHeaderCell(new Cell().Add(new Paragraph(header).SetFont(boldFont)));
                }

                // Rows (with calculated total per row in the last column)
                foreach (var record in reportData)
                {
                    int active = Convert.ToInt32(record.ActiveCount);
                    int disposed = Convert.ToInt32(record.DisposedCount);
                    int decided = Convert.ToInt32(record.DecidedCount);
                    int archived = Convert.ToInt32(record.ArchivedCount);
                    int rowTotal = active + disposed + decided + archived;

                    table.AddCell(new Paragraph(record.NatureCase ?? "N/A").SetFont(font));
                    table.AddCell(new Paragraph(active.ToString()).SetFont(font));
                    table.AddCell(new Paragraph(disposed.ToString()).SetFont(font));
                    table.AddCell(new Paragraph(decided.ToString()).SetFont(font));
                    table.AddCell(new Paragraph(archived.ToString()).SetFont(font));
                    table.AddCell(new Paragraph(rowTotal.ToString()).SetFont(font));
                }

                document.Add(table);
                document.Close();
            }

            return File(memoryStream.ToArray(), "application/pdf", $"ReportSummary_{DateTime.Now:yyyyMMdd}.pdf");
        }
        catch (PdfException pdfEx)
        {
            Console.WriteLine($"PDF Exception: {pdfEx.Message}");
            return StatusCode(500, $"PDF generation error: {pdfEx.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting report to PDF: {ex.Message}");
            return StatusCode(500, "An error occurred while exporting the report.");
        }
    }


    //Eto Sa baba yung code
    [HttpGet("CriminalCaseGenerateCaseSummaryPdf")]
    public async Task<IActionResult> CriminalCaseGenerateCaseSummaryPdf()
    {
        return await GenerateFilteredCaseSummaryPdf("Criminal Case", "CriminalCaseSummaryReport");
    }



    //Eto Sa baba yung code
    [HttpGet("CivilCaseGenerateCaseSummaryPdf")]
    public async Task<IActionResult> CivilCaseGenerateCaseSummaryPdf()
    {
        return await GenerateFilteredCaseSummaryPdf("Civil Case", "CivilCaseSummaryReport");
    }



    //kailgan to sa dalawa
    private async Task<IActionResult> GenerateFilteredCaseSummaryPdf(string legalCaseFilter, string filePrefix)
    {
        string query = @"
    SELECT 
        c.cat_NatureCase AS NatureOfCases,
        SUM(CASE WHEN cr.rec_Case_Status = 'ACTIVE' THEN 1 ELSE 0 END) AS Active,
        SUM(CASE WHEN cr.rec_Case_Status = 'DISPOSED' THEN 1 ELSE 0 END) AS Disposed,
        SUM(CASE WHEN cr.rec_Case_Status = 'ARCHIVED' THEN 1 ELSE 0 END) AS Archived,
        SUM(CASE WHEN cr.rec_Case_Status = 'DECIDED' THEN 1 ELSE 0 END) AS Decided
    FROM COURTRECORD cr
    JOIN Category c ON cr.rec_Nature_Descrip = c.cat_NatureCase
    WHERE c.cat_LegalCase = @LegalCase
    GROUP BY c.cat_NatureCase
    ORDER BY c.cat_NatureCase;";

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            var reportData = (await connection.QueryAsync(query, new { LegalCase = legalCaseFilter })).ToList();

            if (!reportData.Any())
                return NotFound("No report data found.");

            using var memoryStream = new MemoryStream();
            using (var writer = new PdfWriter(memoryStream))
            using (var pdf = new PdfDocument(writer))
            {
                var document = new Document(pdf);

                PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
                PdfFont boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                PdfFont italicFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_OBLIQUE);

                var title = new Paragraph($"{legalCaseFilter} Summary Report")
                    .SetFont(boldFont)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontSize(16);
                document.Add(title);

                var subtitle = new Paragraph($"Exported on {DateTime.Now:MMMM dd, yyyy hh:mm tt}")
                    .SetFont(italicFont)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontSize(10);
                document.Add(subtitle);

                document.Add(new Paragraph("\n"));

                var table = new Table(6, true); // 6 columns
                string[] headers = { "Nature of Case", "Active", "Disposed", "Decided", "Archived", "Total" };
                foreach (var header in headers)
                {
                    table.AddHeaderCell(new Cell().Add(new Paragraph(header).SetFont(boldFont)));
                }

                foreach (var record in reportData)
                {
                    int active = Convert.ToInt32(record.Active);
                    int disposed = Convert.ToInt32(record.Disposed);
                    int decided = Convert.ToInt32(record.Decided);
                    int archived = Convert.ToInt32(record.Archived);
                    int total = active + disposed + decided + archived;

                    table.AddCell(new Paragraph(record.NatureOfCases ?? "N/A").SetFont(font));
                    table.AddCell(new Paragraph(active.ToString()).SetFont(font));
                    table.AddCell(new Paragraph(disposed.ToString()).SetFont(font));
                    table.AddCell(new Paragraph(decided.ToString()).SetFont(font));
                    table.AddCell(new Paragraph(archived.ToString()).SetFont(font));
                    table.AddCell(new Paragraph(total.ToString()).SetFont(font));
                }

                document.Add(table);
                document.Close();
            }

            return File(memoryStream.ToArray(), "application/pdf", $"{filePrefix}_{DateTime.Now:yyyyMMdd}.pdf");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PDF Export Error: {ex.Message}");
            return StatusCode(500, "An error occurred while generating the PDF.");
        }
    }




}
