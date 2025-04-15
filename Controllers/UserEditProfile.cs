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
                currentUserName = User?.Identity?.IsAuthenticated == true ? User.Identity.Name : null;
            }

            if (string.IsNullOrEmpty(currentUserName))
                return Unauthorized("User is not logged in.");

            if (userUpdate == null || string.IsNullOrWhiteSpace(userUpdate.UserName)
                || string.IsNullOrWhiteSpace(userUpdate.Password) || string.IsNullOrWhiteSpace(userUpdate.ConfirmPassword))
                return BadRequest("Username, password, and confirm password are required.");

            // Password length validation
            if (userUpdate.Password.Length < 6)
                return BadRequest("Password must be at least 6 characters long.");

            // Confirm password match validation
            if (userUpdate.Password != userUpdate.ConfirmPassword)
                return BadRequest("Password and confirm password do not match.");

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

            List<string> changes = new List<string>();

            // Check if username is changing (case-insensitive)
            if (!string.Equals(existingUser.user_Name, userUpdate.UserName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                string checkUserQuery = @"
                SELECT COUNT(*) FROM ManageUsers 
                WHERE LOWER(TRIM(user_Name)) = @NewUserName AND user_Id != @UserId";

                int usernameExists = await con.ExecuteScalarAsync<int>(checkUserQuery, new { NewUserName = userUpdate.UserName.Trim().ToLowerInvariant(), UserId = existingUser.user_Id });

                if (usernameExists > 0)
                    return Conflict("The new username is already taken.");

                changes.Add($"UserName: \"{existingUser.user_Name}\" → \"{userUpdate.UserName.Trim()}\"");
            }

            // Check if password is the same as current
            bool isSamePassword = PasswordHasher.VerifyPassword(userUpdate.Password, existingUser.user_Pass);
            if (isSamePassword)
            {
                return BadRequest("The new password cannot be the same as the current password.");
            }

            // Hash the new password
            string hashedPassword = PasswordHasher.HashPassword(userUpdate.Password);
            changes.Add("Password: (updated)");

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
