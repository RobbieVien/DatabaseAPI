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
public class StageController : ControllerBase
{
    private readonly string _connectionString;

    public StageController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }


    [HttpPost("AddStage")]
    public async Task<IActionResult> AddStage([FromBody] StageDto stageDto)
    {
        if (stageDto == null || string.IsNullOrWhiteSpace(stageDto.Stage))
        {
            return BadRequest("Invalid stage data.");
        }

        string trimmedStage = stageDto.Stage.Trim();

        using (var con = new MySqlConnection(_connectionString))
        {
            await con.OpenAsync();
            var username = HttpContext.Session.GetString("UserName");
            try
            {
                // Check if stage already exists (case-insensitive)
                string checkQuery = @"SELECT COUNT(*) FROM Stage WHERE LOWER(stage_stage) = LOWER(@Stage)";
                int exists = await con.ExecuteScalarAsync<int>(checkQuery, new { Stage = trimmedStage });

                if (exists > 0)
                {
                    return Conflict($"Stage '{trimmedStage}' already exists.");
                }

                // Insert new stage
                string insertQuery = @"
                INSERT INTO Stage (stage_stage, stage_usage_count)
                VALUES (@Stage, @UsageCount)";

                int rowsAffected = await con.ExecuteAsync(insertQuery, new
                {
                    Stage = trimmedStage,
                    UsageCount = stageDto.UsageCount
                });

                if (rowsAffected == 0)
                {
                    return StatusCode(500, "Stage insertion failed.");
                }

                // Log the action
                await Logger.LogActionAdd(HttpContext,
                    action: "INSERT",
                    tableName: "Stage",
                    details: $"Stage '{trimmedStage}' added with usage count {stageDto.UsageCount}.");

                return Ok(new { message = "Stage added successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }


    // Update Stage Name based on ID
    [HttpPut("UpdateStage/{stageId}")]
    public async Task<IActionResult> UpdateStage(int stageId, [FromBody] StageDto stageDto)
    {
        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var username = HttpContext.Session.GetString("UserName");
                // Validate input
                if (stageDto == null || string.IsNullOrWhiteSpace(stageDto.Stage))
                {
                    return BadRequest("Invalid stage data.");
                }

                string newStage = stageDto.Stage.Trim();

                // Check if stage with this ID exists
                string checkIdQuery = "SELECT stage_Id FROM Stage WHERE stage_Id = @StageId";
                var existingStage = await connection.QueryFirstOrDefaultAsync<int>(checkIdQuery, new { StageId = stageId });

                if (existingStage == 0)
                {
                    return NotFound($"Stage with ID {stageId} not found.");
                }

                // Check if the new stage name already exists (excluding the current stage)
                string checkDuplicateQuery = @"
                    SELECT COUNT(*) FROM Stage 
                    WHERE LOWER(stage_stage) = LOWER(@Stage) AND stage_Id != @StageId";

                int duplicateCount = await connection.ExecuteScalarAsync<int>(checkDuplicateQuery, new
                {
                    Stage = newStage,
                    StageId = stageId
                });

                if (duplicateCount > 0)
                {
                    return Conflict($"Stage name '{newStage}' already exists.");
                }

                // Update the stage name
                string updateQuery = @"UPDATE Stage SET stage_stage = @Stage WHERE stage_Id = @StageId";

                int rowsAffected = await connection.ExecuteAsync(updateQuery, new
                {
                    Stage = newStage,
                    StageId = stageId
                });

                if (rowsAffected == 0)
                {
                    return StatusCode(500, "Failed to update stage.");
                }

                // Log the action
                await Logger.LogAction(HttpContext,
                    action: "UPDATE",
                    tableName: "Stage",
                    recordId: stageId,
                    details: $"Stage ID {stageId} updated to '{newStage}'");

                return Ok(new { message = "Stage updated successfully." });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Message = "An error occurred while updating the stage.",
                ErrorDetails = ex.Message
            });
        }
    }





    [HttpDelete("DeleteStage/{stageId}")]
    public async Task<IActionResult> DeleteStage(int stageId)
    {
        if (stageId <= 0)
        {
            return BadRequest("Valid stage ID is required.");
        }

        using (var con = new MySqlConnection(_connectionString))
        {
            await con.OpenAsync();
            var username = HttpContext.Session.GetString("UserName");
            try
            {
                // Check if the stage exists by ID
                string checkQuery = @"SELECT COUNT(*) FROM Stage WHERE stage_Id = @StageId";

                int exists = await con.ExecuteScalarAsync<int>(checkQuery, new { StageId = stageId });

                if (exists == 0)
                {
                    return NotFound($"Stage with ID '{stageId}' not found.");
                }

                // Delete the stage by ID
                string deleteQuery = @"DELETE FROM Stage WHERE stage_Id = @StageId";

                int rowsAffected = await con.ExecuteAsync(deleteQuery, new { StageId = stageId });

                if (rowsAffected == 0)
                    return StatusCode(500, "Stage deletion failed.");

                // Log the deletion action
                await Logger.LogAction(HttpContext,
                    action: "DELETE",
                    tableName: "Stage",
                    recordId: stageId,
                    details: $"Stage ID {stageId} deleted.");

                return Ok(new { message = "Stage deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }



    [HttpGet("GetStages")]
    public async Task<IActionResult> GetStages()
    {
        using (var con = new MySqlConnection(_connectionString))
        {
            await con.OpenAsync();

            try
            {
                string query = @"
                SELECT 
                    s.stage_stage AS Stage, 
                    COUNT(c.rec_Case_Stage) AS UsageCount
                FROM Stage s
                LEFT JOIN COURTRECORD c ON s.stage_stage = c.rec_Case_Stage
                GROUP BY s.stage_stage
                ORDER BY s.stage_stage ASC";

                var stages = await con.QueryAsync<StageDto>(query);
                return Ok(stages);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }s





}