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
public class FirstUserController : ControllerBase
{
    private readonly string _connectionString;

    public FirstUserController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }

    // LogAction method (moved from LogsController)


    // GetLogs method (optional, if you want to retrieve logs in UserController)


    //----------------------------------------------------------------------------------------------------------------

    //di pa to natetest yung validation lng naman dinagdag ko. 
    [HttpPost("FirstAddUser")]
    public async Task<IActionResult> FirstAddUser([FromBody] UserDto user)
    {
        if (user == null || string.IsNullOrWhiteSpace(user.UserName) || string.IsNullOrWhiteSpace(user.Password))
        {
            return BadRequest("Invalid user data.");
        }

        // Password validation: minimum 6 characters and at least 1 special character
        if (user.Password.Length < 6)
        {
            return BadRequest("Password must be at least 6 characters long.");
        }

        var specialCharPattern = new System.Text.RegularExpressions.Regex(@"[!""#$%&'()*+,\-./:;<=>?@[\\\]^_`{|}~]");
        if (!specialCharPattern.IsMatch(user.Password))
        {
            return BadRequest("Password must contain at least one special character (e.g., !, @, #, $, etc.).");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        // Check if any users exist in the table
        int existingUsers = await con.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ManageUsers");

        if (existingUsers > 0)
        {
            return Conflict("User creation is disabled after initial user setup.");
        }

        // Check if username exists
        int userCount = await con.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM ManageUsers WHERE user_Name = @UserName",
            new { UserName = user.UserName });

        if (userCount > 0)
        {
            return Conflict("Username already exists.");
        }

        // Hash password
        string hashedPassword = PasswordHasher.HashPassword(user.Password);

        // Insert new user
        var insertResult = await con.ExecuteAsync(@"
        INSERT INTO ManageUsers 
        (user_Fname, user_Lname, user_Role, user_Status, user_Name, user_Pass)
        VALUES (@FirstName, @LastName, @Role, @Status, @UserName, @Password)",
            new
            {
                user.FirstName,
                user.LastName,
                user.Role,
                user.Status,
                user.UserName,
                Password = hashedPassword
            });

        if (insertResult > 0)
        {
            // Get the new user ID
            int newUserId = await con.ExecuteScalarAsync<int>("SELECT LAST_INSERT_ID()");

            // Log with static message in details
            await Logger.LogAction(HttpContext, "Add", "ManageUsers", newUserId, "User has been created");

            return Ok(new { Message = $"User Added Successfully" });
        }

        return BadRequest("Failed to add user.");
    }

}
