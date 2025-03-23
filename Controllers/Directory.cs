using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using Dapper;
using DatabaseAPI.Models;
using DatabaseAPI.Utilities;

[Route("api/[controller]")]
[ApiController]
public class DirectoryController : ControllerBase
{
    private readonly string _connectionString;

    public DirectoryController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }

    [HttpPost("AddDirectory")]
    public async Task<IActionResult> AddDirectory([FromBody] DirectoryDto directory)
    {
        if (directory == null || string.IsNullOrWhiteSpace(directory.DirectoryName))
        {
            return BadRequest("Invalid user data.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        // Check if username already exists
        string checkQuery = "SELECT COUNT(*) FROM Directory WHERE direct_name = @DirectoryName";
        using var checkCmd = new MySqlCommand(checkQuery, con);
        checkCmd.Parameters.AddWithValue("@DirectoryName", directory.DirectoryName);
        int userCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

        if (userCount > 0)
        {
            return Conflict("That name already exists.");
        }

        // Insert new user
        string insertQuery = @"INSERT INTO Directory (direct_name, direct_position, direct_contact, direct_email, direct_status)
                           VALUES (@DirectoryName, @DirectoryPosition, @DirectoryContact, @DirectoryEmail, @DirectoryStatus)";
        using var insertCmd = new MySqlCommand(insertQuery, con);
        insertCmd.Parameters.AddWithValue("@DirectoryName", directory.DirectoryName);
        insertCmd.Parameters.AddWithValue("@DirectoryPosition", directory.DirectoryPosition);
        insertCmd.Parameters.AddWithValue("@DirectoryContact", directory.DirectoryContact);
        insertCmd.Parameters.AddWithValue("@DirectoryEmail", directory.DirectoryEmail);
        insertCmd.Parameters.AddWithValue("@DirectoryStatus", directory.DirectoryStatus);

        await insertCmd.ExecuteNonQueryAsync();

        // Log the action
        await Logger.LogAction($"Directory {directory.DirectoryName} has been added.", "Directory", 0);

        return Ok("Directory added successfully.");
    }

    [HttpPut("DirectoryEdit/{id}")]
    public async Task<IActionResult> DirectoryEdit(int id, [FromBody] DirectoryDto directory, [FromQuery] string userName)
    {
        if (id <= 0 || directory == null || string.IsNullOrWhiteSpace(directory.DirectoryName))
        {
            return BadRequest("Invalid directory data.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        try
        {
            Console.WriteLine($"Attempting to update directory entry with ID: {id}");

            // Fetch old values
            string oldName = "", oldPosition = "", oldContact = "", oldEmail = "", oldStatus = "";

            string fetchOldValuesQuery = "SELECT direct_name, direct_position, direct_contact, direct_email, direct_status FROM Directory WHERE directory_Id = @Id";
            using var fetchOldValuesCmd = new MySqlCommand(fetchOldValuesQuery, con);
            fetchOldValuesCmd.Parameters.AddWithValue("@Id", id);
            using var reader = await fetchOldValuesCmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                oldName = reader["direct_name"]?.ToString() ?? "";
                oldPosition = reader["direct_position"]?.ToString() ?? "";
                oldContact = reader["direct_contact"]?.ToString() ?? "";
                oldEmail = reader["direct_email"]?.ToString() ?? "";
                oldStatus = reader["direct_status"]?.ToString() ?? "";
            }
            reader.Close();

            // Update query
            string updateQuery = @"UPDATE Directory 
                     SET direct_name = @DirectoryName,
                         direct_position = @DirectoryPosition,
                         direct_contact = @DirectoryContact,
                         direct_email = @DirectoryEmail,
                         direct_status = @DirectoryStatus
                     WHERE directory_Id = @Id";

            using var updateCmd = new MySqlCommand(updateQuery, con);
            updateCmd.Parameters.AddWithValue("@Id", id);
            updateCmd.Parameters.AddWithValue("@DirectoryName", directory.DirectoryName);
            updateCmd.Parameters.AddWithValue("@DirectoryPosition", directory.DirectoryPosition);
            updateCmd.Parameters.AddWithValue("@DirectoryContact", directory.DirectoryContact);
            updateCmd.Parameters.AddWithValue("@DirectoryEmail", directory.DirectoryEmail);
            updateCmd.Parameters.AddWithValue("@DirectoryStatus", directory.DirectoryStatus);

            int rowsAffected = await updateCmd.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                // Log changes
                List<string> changes = new List<string>();

                if (oldName != directory.DirectoryName)
                {
                    changes.Add($"Updated Name: \"{oldName}\" → \"{directory.DirectoryName}\"");
                }
                if (oldPosition != directory.DirectoryPosition)
                {
                    changes.Add($"Updated Position: \"{oldPosition}\" → \"{directory.DirectoryPosition}\"");
                }
                if (oldContact != directory.DirectoryContact)
                {
                    changes.Add($"Updated Contact: \"{oldContact}\" → \"{directory.DirectoryContact}\"");
                }
                if (oldEmail != directory.DirectoryEmail)
                {
                    changes.Add($"Updated Email: \"{oldEmail}\" → \"{directory.DirectoryEmail}\"");
                }
                if (oldStatus != directory.DirectoryStatus)
                {
                    changes.Add($"Updated Status: \"{oldStatus}\" → \"{directory.DirectoryStatus}\"");
                }

                if (changes.Count > 0)
                {
                    string logMessage = $"Updated directory entry (ID: {id})";
                    string details = string.Join(", ", changes);
                    await Logger.LogAction(logMessage, "DIRECTORY", id, userName, details);
                }

                return Ok(new { Message = $"Directory entry with ID {id} updated successfully." });
            }
            else
            {
                return NotFound("No directory entry found with the specified ID.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating directory entry: {ex.Message}");
            Console.WriteLine($"Error StackTrace: {ex.StackTrace}");
            return StatusCode(500, new
            {
                Message = "An error occurred while updating the directory entry.",
                ErrorDetails = ex.Message
            });
        }
    }

    [HttpDelete("DeleteDirectory/{id}")]
    public async Task<IActionResult> DeleteDirectory(int id, [FromHeader(Name = "UserName")] string userName = "System")
    {
        Console.WriteLine($"DeleteDirectory called with ID: {id}"); // Log when the route is hit

        if (id <= 0)
        {
            return BadRequest("Invalid directory ID.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        try
        {
            // Retrieve directory details before deletion
            string selectQuery = "SELECT * FROM Directory WHERE directory_Id = @DirectoryId";
            var directory = await con.QueryFirstOrDefaultAsync(selectQuery, new { DirectoryId = id });

            if (directory == null)
            {
                return NotFound("No directory found with the selected ID.");
            }

            // Delete the directory record
            string deleteQuery = "DELETE FROM Directory WHERE directory_Id = @DirectoryId";
            int rowsAffected = await con.ExecuteAsync(deleteQuery, new { DirectoryId = id });

            if (rowsAffected > 0)
            {
                // Log the deleted directory details
                string details = $"Deleted Directory: ID={directory.directory_Id}, Name={directory.directory_Name}, Contact={directory.directory_Contact}, Address={directory.directory_Address}";
                await Logger.LogAction("Delete", "Directory", id, userName, details);

                return Ok("Directory entry has been deleted successfully.");
            }
            else
            {
                return NotFound("No directory found with the selected ID.");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred while deleting the directory: {ex.Message}");
        }
    }


    [HttpGet("GetDirectories")]
    public async Task<IActionResult> GetDirectories()
    {
        try
        {
            using var con = new MySqlConnection(_connectionString);
            await con.OpenAsync();

            string query = @"
            SELECT 
                directory_Id AS DirectoryId, 
                direct_name AS DirectoryName, 
                direct_position AS DirectoryPosition, 
                direct_contact AS DirectoryContact, 
                direct_email AS DirectoryEmail, 
                direct_status AS DirectoryStatus
            FROM Directory";

            var directories = (await con.QueryAsync<DirectoryDto>(query)).ToList();

            if (directories.Count == 0)
                return NotFound("No directory records found.");

            return Ok(directories);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving directories: {ex.Message}");
        }
    }




}
