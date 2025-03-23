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
public class UserController : ControllerBase
{
    private readonly string _connectionString;

    public UserController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }

    // LogAction method (moved from LogsController)
   

    // GetLogs method (optional, if you want to retrieve logs in UserController)


    //----------------------------------------------------------------------------------------------------------------

    [HttpPost("AddUser")]
    public async Task<IActionResult> AddUser([FromBody] UserDto user)
    {
        // Get current user info or use default for testing
        string currentUserName = "System";
        string userRole = "unknown";

        // Try to get authenticated user info if available
        if (User?.Identity?.IsAuthenticated == true)
        {
            currentUserName = User.Identity.Name ?? "System";
            userRole = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? "unknown";

            // Check if user has admin or chiefadmin role when authenticated
            if (userRole != "admin" && userRole != "chiefadmin")
            {
                return StatusCode(403, "Only administrators can add new users.");
            }
        }

        if (user == null || string.IsNullOrWhiteSpace(user.UserName) || string.IsNullOrWhiteSpace(user.Password))
        {
            return BadRequest("Invalid user data.");
        }

        // Rest of your existing code
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        // Check if username already exists
        string checkQuery = "SELECT COUNT(*) FROM ManageUsers WHERE user_Name = @UserName";
        int userCount = await con.ExecuteScalarAsync<int>(checkQuery, new { UserName = user.UserName });
        if (userCount > 0)
        {
            return Conflict("Username already exists.");
        }

        // Hash the password correctly
        string hashedPassword = PasswordHasher.HashPassword(user.Password);

        string insertQuery = @"
        INSERT INTO ManageUsers (user_Fname, user_Lname, user_Role, user_Status, user_Name, user_Pass)
        VALUES (@FirstName, @LastName, @Role, @Status, @UserName, @Password)";

        int rowsAffected = await con.ExecuteAsync(insertQuery, new
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            Status = user.Status,
            UserName = user.UserName,
            Password = hashedPassword
        });

        if (rowsAffected > 0)
        {
            // Get the ID of the newly added user
            int newUserId = await con.ExecuteScalarAsync<int>("SELECT LAST_INSERT_ID()");

            // Log the action
            await Logger.LogAction("Add", "ManageUsers", newUserId, currentUserName);

            return Ok("User added successfully.");
        }

        return BadRequest("Failed to add user.");
    }

    //----------------------------------------------------------------------------------------------------------------
    [HttpPut("UserEdit/{id}")]
    public async Task<IActionResult> UserEdit(int id, [FromBody] UserDto user)
    {
        // Get current user info or use default for testing
        string currentUserName = "System";
        string userRole = "unknown";

        // Try to get authenticated user info if available
        if (User?.Identity?.IsAuthenticated == true)
        {
            currentUserName = User.Identity.Name ?? "System";
            userRole = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? "unknown";

            // Check if user has admin or chiefadmin role when authenticated
            if (userRole != "admin" && userRole != "chiefadmin")
            {
                return StatusCode(403, "Only administrators can edit users.");
            }
        }
        // For backwards compatibility with Windows Forms that might use the header
        else if (Request.Headers.TryGetValue("UserRole", out var headerRole))
        {
            userRole = headerRole.ToString();
            if (userRole != "Admin" && userRole != "ChiefAdmin")
            {
                return StatusCode(403, "Only Admin and ChiefAdmin roles can edit users.");
            }
        }

        if (id <= 0 || user == null || string.IsNullOrWhiteSpace(user.UserName))
        {
            return BadRequest("Invalid user data.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        // Get existing user details for comparison
        string selectQuery = "SELECT user_Id, user_Fname, user_Lname, user_Role, user_Status, user_Name, user_Pass FROM ManageUsers WHERE user_Id = @Id";
        var oldUser = await con.QueryFirstOrDefaultAsync<dynamic>(selectQuery, new { Id = id });
        if (oldUser == null)
        {
            return NotFound("No user found with the specified ID.");
        }

        // Track changes
        List<string> changes = new List<string>();

        // Check for role change
        if (oldUser.user_Role != user.Role)
        {
            changes.Add($"updated \"{oldUser.user_Role}\" to \"{user.Role}\" in Role");
        }

        // Check for other changes
        if (oldUser.user_Fname != user.FirstName)
        {
            changes.Add($"updated \"{oldUser.user_Fname}\" to \"{user.FirstName}\" in FirstName");
        }

        if (oldUser.user_Lname != user.LastName)
        {
            changes.Add($"updated \"{oldUser.user_Lname}\" to \"{user.LastName}\" in LastName");
        }

        if (oldUser.user_Status != user.Status)
        {
            changes.Add($"updated \"{oldUser.user_Status}\" to \"{user.Status}\" in Status");
        }

        if (oldUser.user_Name != user.UserName)
        {
            changes.Add($"updated \"{oldUser.user_Name}\" to \"{user.UserName}\" in UserName");
        }

        // Only re-hash the password if it's changed
        string newPassword = oldUser.user_Pass;
        bool passwordChanged = false;

        if (!string.IsNullOrWhiteSpace(user.Password) && !PasswordHasher.VerifyPassword(user.Password, oldUser.user_Pass))
        {
            newPassword = PasswordHasher.HashPassword(user.Password);
            passwordChanged = true;
            changes.Add("Password was updated");
        }

        // Update user
        string updateQuery = @"
    UPDATE ManageUsers 
    SET user_Fname = @FirstName,
        user_Lname = @LastName,
        user_Role = @Role,
        user_Status = @Status,
        user_Name = @UserName,
        user_Pass = @Password
    WHERE user_Id = @Id";

        int rowsAffected = await con.ExecuteAsync(updateQuery, new
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            Status = user.Status,
            UserName = user.UserName,
            Password = newPassword,
            Id = id
        });

        if (rowsAffected > 0)
        {
            // Combine the changes into a single message
            string changeMessage = string.Join(", ", changes);

            // If no changes detected, provide a generic message
            if (string.IsNullOrEmpty(changeMessage))
            {
                changeMessage = "No changes were made";
            }

            // Log the edit action with detailed changes
            await Logger.LogAction("Edit", "ManageUsers", id, currentUserName, changeMessage);
            return Ok("User updated successfully.");
        }

        return NotFound("No user found.");
    }


    [HttpDelete("DeleteUser/{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        Console.WriteLine($"DeleteUser called with ID: {id}");

        if (id <= 0)
        {
            return BadRequest("Invalid user ID.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string deleteQuery = "DELETE FROM ManageUsers WHERE user_Id = @UserId";
        int rowsAffected = await con.ExecuteAsync(deleteQuery, new { UserId = id });

        if (rowsAffected > 0)
        {
            await Logger.LogAction("Delete", "ManageUsers", id, "System", "User deleted");
            return Ok("User has been deleted successfully.");
        }

        return NotFound("No user found with the selected ID.");
    }

    //----------------------------------------------------------------------------------------------------------------

   
    //for DataGridview
    [HttpGet("GetUsers")]
    public async Task<IActionResult> GetUsers()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT user_Id, user_Fname, user_Lname, user_Role, user_Status, user_Name FROM ManageUsers";
        using var cmd = new MySqlCommand(query, con);

        using var reader = await cmd.ExecuteReaderAsync();

        var users = new List<UserDto>();
        while (await reader.ReadAsync())
        {
            users.Add(new UserDto
            {
                UserId = Convert.ToInt32(reader["user_Id"]), // Make sure user_Id is included
                FirstName = reader["user_Fname"]?.ToString(),
                LastName = reader["user_Lname"]?.ToString(),
                Role = reader["user_Role"]?.ToString(),
                Status = reader["user_Status"]?.ToString(),
                UserName = reader["user_Name"]?.ToString()
            });
        }

        return Ok(users);
    }
}
