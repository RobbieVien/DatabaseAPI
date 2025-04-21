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
public class BranchController : ControllerBase
{
    private readonly string _connectionString;

    public BranchController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }

    [HttpPost("AddBranch")]
    public async Task<IActionResult> AddBranch([FromBody] BranchDto branchDto)
    {
        if (branchDto == null || string.IsNullOrWhiteSpace(branchDto.BranchName))
        {
            return BadRequest("Invalid branch data.");
        }

        string trimmedBranchName = branchDto.BranchName.Trim();

        using (var con = new MySqlConnection(_connectionString))
        {
            await con.OpenAsync();
            var username = HttpContext.Session.GetString("UserName");

            try
            {
                // Check if branch already exists (case-insensitive)
                string checkQuery = @"SELECT COUNT(*) FROM Branch WHERE LOWER(branch_Name) = LOWER(@BranchName)";
                int exists = await con.ExecuteScalarAsync<int>(checkQuery, new { BranchName = trimmedBranchName });

                if (exists > 0)
                {
                    return Conflict($"Branch '{trimmedBranchName}' already exists.");
                }

                // Insert new branch
                string insertQuery = @"INSERT INTO Branch (branch_Name) VALUES (@BranchName)";
                int rowsAffected = await con.ExecuteAsync(insertQuery, new { BranchName = trimmedBranchName });

                if (rowsAffected == 0)
                {
                    return StatusCode(500, "Branch insertion failed.");
                }

                // Log the action
                await Logger.LogActionAdd(HttpContext,
                    action: "INSERT",
                    tableName: "Branch",
                    details: $"Branch '{trimmedBranchName}' added.");

                return Ok(new { message = "Branch added successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }


    // Update Branch Name based on ID
    [HttpPut("UpdateBranch/{branchId}")]
    public async Task<IActionResult> UpdateBranch(int branchId, [FromBody] BranchDto branchDto)
    {
        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var username = HttpContext.Session.GetString("UserName");

                // Validate input
                if (branchDto == null || string.IsNullOrWhiteSpace(branchDto.BranchName))
                {
                    return BadRequest("Invalid branch data.");
                }

                string newBranchName = branchDto.BranchName.Trim();

                // Check if branch with this ID exists
                string checkIdQuery = "SELECT branch_Id FROM Branch WHERE branch_Id = @BranchId";
                var existingBranch = await connection.QueryFirstOrDefaultAsync<int?>(checkIdQuery, new { BranchId = branchId });

                if (existingBranch == null)
                {
                    return NotFound($"Branch with ID {branchId} not found.");
                }

                // Check for duplicate name (excluding current)
                string checkDuplicateQuery = @"
                SELECT COUNT(*) FROM Branch 
                WHERE LOWER(branch_Name) = LOWER(@BranchName) AND branch_Id != @BranchId";

                int duplicateCount = await connection.ExecuteScalarAsync<int>(checkDuplicateQuery, new
                {
                    BranchName = newBranchName,
                    BranchId = branchId
                });

                if (duplicateCount > 0)
                {
                    return Conflict($"Branch name '{newBranchName}' already exists.");
                }

                // Perform update
                string updateQuery = @"UPDATE Branch SET branch_Name = @BranchName WHERE branch_Id = @BranchId";
                int rowsAffected = await connection.ExecuteAsync(updateQuery, new
                {
                    BranchName = newBranchName,
                    BranchId = branchId
                });

                if (rowsAffected == 0)
                {
                    return StatusCode(500, "Failed to update branch.");
                }

                // Log the action
                await Logger.LogAction(HttpContext,
                    action: "UPDATE",
                    tableName: "Branch",
                    recordId: branchId,
                    details: $"Branch ID {branchId} updated to '{newBranchName}'");

                return Ok(new { message = "Branch updated successfully." });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Message = "An error occurred while updating the branch.",
                ErrorDetails = ex.Message
            });
        }
    }



    // Delete Branch by ID
    [HttpDelete("DeleteBranch/{branchId}")]
    public async Task<IActionResult> DeleteBranch(int branchId)
    {
        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var username = HttpContext.Session.GetString("UserName");

                // Check if branch exists
                string checkQuery = "SELECT branch_Id FROM Branch WHERE branch_Id = @BranchId";
                var existingBranch = await connection.QueryFirstOrDefaultAsync<int?>(checkQuery, new { BranchId = branchId });

                if (existingBranch == null)
                {
                    return NotFound($"Branch with ID {branchId} not found.");
                }

                // Delete the branch
                string deleteQuery = "DELETE FROM Branch WHERE branch_Id = @BranchId";
                int rowsAffected = await connection.ExecuteAsync(deleteQuery, new { BranchId = branchId });

                if (rowsAffected == 0)
                {
                    return StatusCode(500, "Failed to delete branch.");
                }

                // Log the action
                await Logger.LogAction(HttpContext,
                    action: "DELETE",
                    tableName: "Branch",
                    recordId: branchId,
                    details: $"Branch ID {branchId} deleted.");

                return Ok(new { message = "Branch deleted successfully." });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Message = "An error occurred while deleting the branch.",
                ErrorDetails = ex.Message
            });
        }
    }

    [HttpGet("GetBranches")]
    public async Task<IActionResult> GetBranches()
    {
        using (var con = new MySqlConnection(_connectionString))
        {
            await con.OpenAsync();

            try
            {
                string query = @"
            SELECT 
                branch_Name AS BranchName 
            FROM Branch
            ORDER BY branch_Name ASC";

                var branches = await con.QueryAsync<BranchDto>(query);
                return Ok(branches);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }



}
