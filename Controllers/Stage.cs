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

                // Insert new stage with default usage count = 0
                string insertQuery = @"
                INSERT INTO Stage (stage_stage, stage_usage_count)
                VALUES (@Stage, 0)";

                int rowsAffected = await con.ExecuteAsync(insertQuery, new
                {
                    Stage = trimmedStage
                });

                if (rowsAffected == 0)
                {
                    return StatusCode(500, "Stage insertion failed.");
                }

                // Log the action
                await Logger.LogActionAdd(HttpContext,
                    action: "INSERT",
                    tableName: "Stage",
                    details: $"Stage '{trimmedStage}' added with default usage count 0.");

                return Ok(new { message = "Stage added successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }


    // Update Stage Name based on ID
    [HttpPut("UpdateStage/{id}")]
    public async Task<IActionResult> UpdateStage(int id, [FromBody] UpdateStageRequest request)
    {
        if (id <= 0 || request?.Stage == null || string.IsNullOrWhiteSpace(request.Stage.Stage))
        {
            return BadRequest("Invalid stage data.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        try
        {
            Console.WriteLine($"Attempting to update stage entry with ID: {id}");

            // Fetch old value
            string oldStageValue = "";
            string fetchOldQuery = "SELECT stage_stage FROM Stage WHERE stage_Id = @Id";
            using var fetchCmd = new MySqlCommand(fetchOldQuery, con);
            fetchCmd.Parameters.AddWithValue("@Id", id);

            using var reader = await fetchCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                oldStageValue = reader["stage_stage"]?.ToString() ?? "";
            }
            else
            {
                return NotFound($"No stage found with ID: {id}");
            }
            reader.Close();

            // Normalize nulls
            request.Stage.Stage ??= "";

            // Duplicate check
            string duplicateCheck = @"
            SELECT COUNT(*) FROM Stage 
            WHERE LOWER(stage_stage) = LOWER(@Stage) 
              AND stage_Id != @Id";
            using var duplicateCmd = new MySqlCommand(duplicateCheck, con);
            duplicateCmd.Parameters.AddWithValue("@Stage", request.Stage.Stage.Trim());
            duplicateCmd.Parameters.AddWithValue("@Id", id);
            int duplicateCount = Convert.ToInt32(await duplicateCmd.ExecuteScalarAsync());

            if (duplicateCount > 0)
            {
                return Conflict($"Stage name '{request.Stage.Stage}' already exists.");
            }

            // Update
            string updateQuery = "UPDATE Stage SET stage_stage = @Stage WHERE stage_Id = @Id";
            using var updateCmd = new MySqlCommand(updateQuery, con);
            updateCmd.Parameters.AddWithValue("@Stage", request.Stage.Stage.Trim());
            updateCmd.Parameters.AddWithValue("@Id", id);

            int rowsAffected = await updateCmd.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                // Log changes
                List<string> changes = new();
                if (!oldStageValue.Equals(request.Stage.Stage.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    changes.Add($"Updated Stage: \"{oldStageValue}\" → \"{request.Stage.Stage.Trim()}\"");
                }

                // Return updated object
                return Ok(new EditStageDto
                {
                    StageID = id,
                    Stage = request.Stage.Stage.Trim()
                });
            }
            else
            {
                return StatusCode(500, "Update failed. No rows affected.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating stage: {ex.Message}");
            Console.WriteLine($"Error StackTrace: {ex.StackTrace}");
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
                    s.stage_Id AS StageID,
                    s.stage_stage AS Stage, 
                    COUNT(c.rec_Case_Stage) AS StageUsageCount
                FROM Stage s
                LEFT JOIN COURTRECORD c ON s.stage_stage = c.rec_Case_Stage
                GROUP BY s.stage_Id, s.stage_stage
                ORDER BY s.stage_stage ASC";

                var stages = await con.QueryAsync<StageDto>(query);
                return Ok(stages);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }






}