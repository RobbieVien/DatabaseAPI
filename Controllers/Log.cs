using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Dapper;
using DatabaseAPI.Models;
using DatabaseAPI.Utilities;
using System.Collections.Generic;
//ITEXT
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.IO.Font.Constants;
using iText.IO.Font;
using iText.Kernel.Font;
using iText.Kernel.Exceptions;


namespace DatabaseAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LogsController : ControllerBase
    {
        private readonly string _connectionString;

        public LogsController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        }

        [HttpGet("GetLogs")]
        public async Task<ActionResult<IEnumerable<LogsDto>>> GetLogs()
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT * FROM Logs ORDER BY Timestamp DESC";
            var logs = await connection.QueryAsync<LogsDto>(sql);
            return Ok(logs);
        }
        [HttpGet("GetLogsAdd")]
        public async Task<ActionResult<IEnumerable<LogsAddDto>>> GetLogsAdd()
        {
            using var connection = new MySqlConnection(_connectionString);

            var sql = @"
        SELECT 
            Action, 
            TableName, 
            UserName, 
            Timestamp, 
            Details 
        FROM Logs 
        ORDER BY Timestamp DESC";

            var logs = await connection.QueryAsync<LogsAddDto>(sql);
            return Ok(logs);
        }



        [HttpGet("GetLogsHearing")]
        public async Task<ActionResult<IEnumerable<LogsDto>>> GetLogsHearing()
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT * FROM Logs WHERE TableName = 'Hearing' ORDER BY Timestamp DESC";
            var logs = await connection.QueryAsync<LogsDto>(sql);
            return Ok(logs);
        }


        [HttpGet("GetLogsTasks")]
        public async Task<ActionResult<IEnumerable<LogsDto>>> GetLogsTasks()
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT * FROM Logs WHERE TableName = 'Tasks' ORDER BY Timestamp DESC";
            var logs = await connection.QueryAsync<LogsDto>(sql);
            return Ok(logs);
        }

        [HttpGet("export-logs-pdf")]
        public async Task<IActionResult> ExportLogsPdf()
        {
            try
            {
                // Define SQL query to fetch logs
                const string query = @"
            SELECT 
                Action,
                TableName,
                RecordId,
                UserName,
                Timestamp,
                Details
            FROM Logs
            ORDER BY Timestamp DESC";

                // Fetch logs from the database
                using var connection = new MySqlConnection(_connectionString);
                var logs = (await connection.QueryAsync<LogsDto>(query)).ToList();

                // If no logs are found, return a 404
                if (!logs.Any())
                    return NotFound("No logs found.");

                // Prepare to generate PDF in memory
                using var memoryStream = new MemoryStream(); // Ensuring the stream is open
                using (var writer = new PdfWriter(memoryStream))
                using (var pdf = new PdfDocument(writer))
                {
                    var document = new Document(pdf);

                    // Load font for the document (Use Helvetica)
                    PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
                    PdfFont boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                    PdfFont italicFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_OBLIQUE);

                    // Title
                    var title = new Paragraph("System Logs Report")
                        .SetFont(boldFont)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetFontSize(16);
                    document.Add(title);

                    // Subtitle (removed username from session)
                    var subtitle = new Paragraph($"Exported on {DateTime.Now:MMMM dd, yyyy hh:mm tt}")
                        .SetFont(italicFont)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetFontSize(10);
                    document.Add(subtitle);

                    // Add a blank line
                    document.Add(new Paragraph("\n"));

                    // Create a table for the logs with 6 columns
                    var table = new Table(6, true); // 6 columns

                    // Table headers
                    string[] headers = { "Action", "Table", "Record ID", "User", "Details", "Timestamp" };
                    foreach (var header in headers)
                    {
                        table.AddHeaderCell(new Cell().Add(new Paragraph().Add(new Text(header).SetFont(boldFont))));
                    }

                    // Add data rows
                    foreach (var log in logs)
                    {
                        table.AddCell(new Paragraph(log.Action).SetFont(font));
                        table.AddCell(new Paragraph(log.TableName).SetFont(font));
                        table.AddCell(new Paragraph(log.RecordId.ToString()).SetFont(font));
                        table.AddCell(new Paragraph(log.UserName).SetFont(font));
                        table.AddCell(new Paragraph(log.Details ?? "N/A").SetFont(font));
                        table.AddCell(new Paragraph(log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")).SetFont(font));
                    }

                    // Add the table to the document
                    document.Add(table);

                    // Finalize the document
                    document.Close();
                }

                // Get the PDF bytes and return it as a download
                var pdfBytes = memoryStream.ToArray();
                return File(memoryStream.ToArray(), "application/pdf", $"LogsReport_{DateTime.Now:yyyyMMdd}.pdf");
            }
            catch (PdfException pdfEx)
            {
                // Specific catch for PdfException
                Console.WriteLine($"PDF Exception: {pdfEx.Message}");
                Console.WriteLine(pdfEx.StackTrace); // Print stack trace for detailed debug info
                return StatusCode(500, $"PDF generation error: {pdfEx.Message}");

            }
            catch (Exception ex)
            {
                // General error handling
                Console.WriteLine($"Error exporting logs to PDF: {ex.Message}");
                Console.WriteLine(ex.StackTrace); // Print stack trace for detailed debug info
                return StatusCode(500, "An error occurred while exporting logs.");
            }
        }

    }
}
