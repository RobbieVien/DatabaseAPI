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
public class UserEditProfileController : ControllerBase
{
    private readonly string _connectionString;

    public UserEditProfileController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }


    [HttpPut("UserUpdate")]
    public async Task<IActionResult> UserUpdate([FromBody] EditUserDto userUpdate)
    {
        try
        {
            // Get username from session or claims
            var currentUserName = HttpContext.Session.GetString("UserName");
            if (string.IsNullOrEmpty(currentUserName))
            {
                // Alternatively, get from claims if session is not used
                currentUserName = User?.Identity?.IsAuthenticated == true ? User.Identity.Name : null;
            }

            if (string.IsNullOrEmpty(currentUserName))
                return Unauthorized("User is not logged in.");

            if (userUpdate == null || string.IsNullOrWhiteSpace(userUpdate.UserName) || string.IsNullOrWhiteSpace(userUpdate.Password))
                return BadRequest("Username and password are required.");

            using var con = new MySqlConnection(_connectionString);
            await con.OpenAsync();

            // Get current user record by username (case-insensitive)
            string selectQuery = @"
            SELECT user_Id, user_Fname, user_Lname, user_Role, user_Status, user_Name, user_Pass 
            FROM ManageUsers 
            WHERE LOWER(TRIM(user_Name)) = @UserName";

            var existingUser = await con.QueryFirstOrDefaultAsync<dynamic>(selectQuery, new { UserName = currentUserName.Trim().ToLowerInvariant() });

            if (existingUser == null)
                return NotFound("User not found.");

            // Track changes for logging
            List<string> changes = new List<string>();

            // Check if username is changing (case-insensitive compare)
            if (!string.Equals(existingUser.user_Name, userUpdate.UserName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                // Check if new username already exists
                string checkUserQuery = @"
                SELECT COUNT(*) FROM ManageUsers 
                WHERE LOWER(TRIM(user_Name)) = @NewUserName AND user_Id != @UserId";

                int usernameExists = await con.ExecuteScalarAsync<int>(checkUserQuery, new { NewUserName = userUpdate.UserName.Trim().ToLowerInvariant(), UserId = existingUser.user_Id });

                if (usernameExists > 0)
                    return Conflict("The new username is already taken.");

                changes.Add($"UserName: \"{existingUser.user_Name}\" → \"{userUpdate.UserName.Trim()}\"");
            }

            // Hash the new password
            string hashedPassword = PasswordHasher.HashPassword(userUpdate.Password);

            // Check if password is different (verify hashed)
            bool passwordChanged = !PasswordHasher.VerifyPassword(userUpdate.Password, existingUser.user_Pass);
            if (passwordChanged)
            {
                changes.Add("Password: (updated)");
            }

            if (changes.Count == 0)
            {
                return Ok("No changes detected.");
            }

            // Update username and password
            string updateQuery = @"
            UPDATE ManageUsers 
            SET user_Name = @UserName,
                user_Pass = @Password
            WHERE user_Id = @UserId";

            int rowsAffected = await con.ExecuteAsync(updateQuery, new
            {
                UserName = userUpdate.UserName.Trim(),
                Password = hashedPassword,
                UserId = existingUser.user_Id
            });

            if (rowsAffected > 0)
            {
                // Log the update action
                await Logger.LogAction(HttpContext, "Edit", "ManageUsers", existingUser.user_Id, $"User updated their profile: {string.Join(", ", changes)}");

                return Ok(new
                {
                    Message = "User profile updated successfully.",
                    Changes = changes
                });
            }

            return BadRequest("Failed to update user profile.");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Message = "An error occurred while updating the user profile.",
                ErrorDetails = ex.Message
            });
        }
    }


}
