using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Dapper;
using System.Collections.Generic;
using DatabaseAPI.Models;
using DatabaseAPI.Utilities;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Office2010.Excel;

[Route("api/[controller]")]
[ApiController]
public class TaskUserSideController : ControllerBase
{
    private readonly string _connectionString;

    public TaskUserSideController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }

 
        //for Datagridview
        [HttpGet("GetTasks")]
        public async Task<IActionResult> GetTasks()
        {
            try
            {
                using var con = new MySqlConnection(_connectionString);
                await con.OpenAsync();

                string query = @"
        SELECT 
            sched_Id, 
            sched_taskTitle,
            sched_user,
            sched_taskDescription, 
            sched_date, 
            sched_inputted, 
            sched_status 
        FROM Tasks";

                var tasks = await con.QueryAsync<Tasksdto>(@"
        SELECT 
            sched_Id AS ScheduleId, 
            sched_taskTitle AS ScheduleTaskTitle,
            sched_user AS ScheduleUser,
            sched_taskDescription AS ScheduleTaskDescription, 
            sched_date AS ScheduleDate, 
            sched_inputted AS ScheduleInputted, 
            sched_status AS ScheduleStatus 
        FROM Tasks");

                return Ok(tasks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving tasks: {ex.Message}");
                return StatusCode(500, $"An error occurred while retrieving tasks: {ex.Message}");
            }
        }



        [HttpGet("CountTasks")]
        public async Task<IActionResult> CountTasks()
        {
            using var con = new MySqlConnection(_connectionString);
            await con.OpenAsync();

            string query = "SELECT COUNT(*) FROM Tasks";
            int count = await con.ExecuteScalarAsync<int>(query);
            return Ok(count);
        }

        [HttpGet("UpcomingTasks")]
        public async Task<IActionResult> UpcomingTasks()
        {
            using var con = new MySqlConnection(_connectionString);
            await con.OpenAsync();

            string query = "SELECT COUNT(*) FROM Tasks WHERE sched_date > CURDATE()";
            int count = await con.ExecuteScalarAsync<int>(query);
            return Ok(count);
        }

        [HttpGet("DueTodayTasks")]
        public async Task<IActionResult> DueTodayTasks()
        {
            using var con = new MySqlConnection(_connectionString);
            await con.OpenAsync();

            string query = "SELECT COUNT(*) FROM Tasks WHERE sched_date = CURDATE()";
            int count = await con.ExecuteScalarAsync<int>(query);
            return Ok(count);
        }

        [HttpGet("OverDueTasks")]
        public async Task<IActionResult> OverDueTasks()
        {
            using var con = new MySqlConnection(_connectionString);
            await con.OpenAsync();

            string query = "SELECT COUNT(*) FROM Tasks WHERE sched_date < CURDATE()";
            int count = await con.ExecuteScalarAsync<int>(query);
            return Ok(count);
        }

        //Combobox get users
        [HttpGet("GetUsers")]
        public async Task<IActionResult> GetUsers()
        {
            using var con = new MySqlConnection(_connectionString);
            await con.OpenAsync();

            string query = "SELECT user_Fname, user_Lname FROM ManageUsers";
            using var cmd = new MySqlCommand(query, con);
            using var reader = await cmd.ExecuteReaderAsync();

            var users = new List<FullnameDto>();
            while (await reader.ReadAsync())
            {
                users.Add(new FullnameDto
                {
                    Name = $"{reader["user_Fname"]?.ToString()} {reader["user_Lname"]?.ToString()}".Trim()
                });
            }

            return Ok(users);
        }


        [HttpGet("export-tasks")]
        public async Task<IActionResult> ExportTasksReport()
        {
            try
            {
                string query = @"
                SELECT 
                    sched_id AS ScheduleId,
                    sched_taskTitle AS ScheduleTaskTitle,
                    sched_user AS ScheduleUser,
                    sched_taskDescription AS ScheduleTaskDescription,
                    DATE_FORMAT(sched_date, '%Y-%m-%d %H:%i:%s') AS ScheduleDate,
                    DATE_FORMAT(sched_inputted, '%Y-%m-%d %H:%i:%s') AS ScheduleInputted,
                    sched_status AS ScheduleStatus,
                    CASE 
                        WHEN sched_date >= CONVERT_TZ(NOW(), 'UTC', 'Asia/Manila') THEN 0
                        ELSE 1
                    END AS is_past
                FROM Tasks
                ORDER BY is_past ASC, 
                         CASE WHEN is_past = 0 THEN sched_date END ASC,
                         CASE WHEN is_past = 1 THEN sched_date END DESC,
                         ScheduleInputted DESC";

                using var connection = new MySqlConnection(_connectionString);
                var taskData = (await connection.QueryAsync<Tasksdto>(query)).ToList();

                Console.WriteLine($"Retrieved {taskData.Count} task records.");

                if (!taskData.Any())
                {
                    return NotFound("No task records found.");
                }

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Task Schedule");
                worksheet.Cell("A1").Value = "TASK SCHEDULE REPORT";
                worksheet.Range("A1:F1").Merge().Style.Font.SetBold(true).Font.SetFontSize(14).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                worksheet.Cell("A2").Value = $"Date Exported: {DateTime.Now:MM/dd/yyyy hh:mm tt}";
                worksheet.Range("A2:F2").Merge().Style.Font.SetItalic(true).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                var headers = new[] { "Task Title", "User", "Description", "Task Date", "Date Inputted", "Status" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(4, i + 1).Value = headers[i];
                    worksheet.Cell(4, i + 1).Style.Font.SetBold(true).Fill.SetBackgroundColor(XLColor.LightGray).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                }

                int row = 5;
                foreach (var record in taskData)
                {
                    worksheet.Cell(row, 1).Value = record.ScheduleTaskTitle ?? "N/A";
                    worksheet.Cell(row, 2).Value = record.ScheduleUser ?? "N/A";
                    worksheet.Cell(row, 3).Value = record.ScheduleTaskDescription ?? "N/A";
                    worksheet.Cell(row, 4).Value = record.ScheduleDate != null ? record.ScheduleDate.ToString("yyyy-MM-dd HH:mm:ss") : "N/A";
                    worksheet.Cell(row, 5).Value = record.ScheduleInputted.ToString("yyyy-MM-dd HH:mm:ss");
                    worksheet.Cell(row, 6).Value = record.ScheduleStatus ? "Completed" : "Pending";
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
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Task_Report_{DateTime.Now:yyyyMMdd}.xlsx");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting task data: {ex.Message}");
                return StatusCode(500, $"Error exporting task data: {ex.Message}");
            }
        }


    }

