using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Dapper;
using DatabaseAPI.Models;
using System.Collections.Generic;

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
    }
}
