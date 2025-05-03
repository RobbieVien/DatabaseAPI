using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Dapper;
using DatabaseAPI.Models;
using System.Collections.Generic;
using DatabaseAPI.Utilities;


namespace DatabaseAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TaskTypeController : ControllerBase
    {
        private readonly string _connectionString;

        public TaskTypeController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        }

        [HttpPost("AddTaskType")]
        public async Task<IActionResult> AddTaskType([FromBody] TaskTypeDto taskTypeDto)
        {
            if (taskTypeDto == null || string.IsNullOrWhiteSpace(taskTypeDto.TaskTypeName))
            {
                return BadRequest("Invalid task type data.");
            }

            string trimmedName = taskTypeDto.TaskTypeName.Trim();
            var username = HttpContext.Session.GetString("UserName");

            using (var con = new MySqlConnection(_connectionString))
            {
                await con.OpenAsync();

                try
                {
                    string checkQuery = "SELECT COUNT(*) FROM TaskType WHERE LOWER(TaskType_name) = LOWER(@Name)";
                    int exists = await con.ExecuteScalarAsync<int>(checkQuery, new { Name = trimmedName });

                    if (exists > 0)
                    {
                        return Conflict($"Task Type '{trimmedName}' already exists.");
                    }

                    string insertQuery = "INSERT INTO TaskType (TaskType_name) VALUES (@Name)";
                    int rowsAffected = await con.ExecuteAsync(insertQuery, new { Name = trimmedName });

                    if (rowsAffected == 0)
                        return StatusCode(500, "Task Type insertion failed.");

                    await Logger.LogActionAdd(HttpContext, "INSERT", "TaskType", $"Task Type '{trimmedName}' added.");

                    return Ok(new { message = "Task Type added successfully." });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"An error occurred: {ex.Message}");
                }
            }
        }



        [HttpPut("UpdateTaskType/{taskTypeId}")]
        public async Task<IActionResult> UpdateTaskType(int taskTypeId, [FromBody] TaskTypeDto taskTypeDto)
        {
            // Validate input
            if (taskTypeDto == null || string.IsNullOrWhiteSpace(taskTypeDto.TaskTypeName))
            {
                return BadRequest("Invalid task type data.");
            }

            string newName = taskTypeDto.TaskTypeName.Trim();

            using (var con = new MySqlConnection(_connectionString))
            {
                await con.OpenAsync();

                try
                {
                    // Check if the TaskType exists
                    string checkQuery = "SELECT COUNT(*) FROM TaskType WHERE taskType_Id = @Id";
                    int exists = await con.ExecuteScalarAsync<int>(checkQuery, new { Id = taskTypeId });

                    if (exists == 0)
                        return NotFound($"Task Type with ID {taskTypeId} not found.");

                    // Check for duplicate name (case-insensitive, excluding current)
                    string duplicateCheck = "SELECT COUNT(*) FROM TaskType WHERE LOWER(TaskType_name) = LOWER(@Name) AND taskType_Id != @Id";
                    int duplicates = await con.ExecuteScalarAsync<int>(duplicateCheck, new { Name = newName, Id = taskTypeId });

                    if (duplicates > 0)
                        return Conflict($"Task Type name '{newName}' already exists.");

                    // Update TaskType
                    string updateQuery = "UPDATE TaskType SET TaskType_name = @Name WHERE taskType_Id = @Id";
                    int rowsAffected = await con.ExecuteAsync(updateQuery, new { Name = newName, Id = taskTypeId });

                    if (rowsAffected == 0)
                        return StatusCode(500, "Failed to update Task Type.");

                    // Return updated DTO
                    return Ok(new TaskTypeDto
                    {
                        TaskTypeId = taskTypeId,
                        TaskTypeName = newName
                    });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"An error occurred: {ex.Message}");
                }
            }
        }




        [HttpDelete("DeleteTaskType/{taskTypeId}")]
        public async Task<IActionResult> DeleteTaskType(int taskTypeId)
        {
            if (taskTypeId <= 0)
                return BadRequest("Valid Task Type ID is required.");

            using (var con = new MySqlConnection(_connectionString))
            {
                await con.OpenAsync();

                try
                {
                    string checkQuery = "SELECT COUNT(*) FROM TaskType WHERE taskType_Id = @Id";
                    int exists = await con.ExecuteScalarAsync<int>(checkQuery, new { Id = taskTypeId });

                    if (exists == 0)
                        return NotFound($"Task Type with ID {taskTypeId} not found.");

                    string deleteQuery = "DELETE FROM TaskType WHERE taskType_Id = @Id";
                    int rowsAffected = await con.ExecuteAsync(deleteQuery, new { Id = taskTypeId });

                    if (rowsAffected == 0)
                        return StatusCode(500, "Task Type deletion failed.");

                    await Logger.LogAction(HttpContext, "DELETE", "TaskType", taskTypeId, $"Task Type ID {taskTypeId} deleted.");

                    return Ok(new { message = "Task Type deleted successfully." });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"An error occurred: {ex.Message}");
                }
            }
        }



        [HttpGet("GetTaskTypes")]
        public async Task<IActionResult> GetTaskTypes()
        {
            using (var con = new MySqlConnection(_connectionString))
            {
                await con.OpenAsync();

                try
                {
                    string query = "SELECT taskType_Id AS TaskTypeId, TaskType_name AS TaskTypeName FROM TaskType ORDER BY TaskType_name ASC";
                    var taskTypes = await con.QueryAsync<TaskTypeDto>(query);
                    return Ok(taskTypes);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"An error occurred: {ex.Message}");
                }
            }
        }


    }

}

