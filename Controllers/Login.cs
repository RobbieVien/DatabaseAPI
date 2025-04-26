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
    public class LoginController : ControllerBase
    {
        private readonly string _connectionString;

        public LoginController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] UserLogin user)
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();


            string passwordSql = "SELECT user_Pass FROM ManageUsers WHERE user_Name = @UserName";
            string storedHash = await connection.QueryFirstOrDefaultAsync<string>(passwordSql, new { UserName = user.UserName });

            if (string.IsNullOrWhiteSpace(user.Password))
            {
                return BadRequest("Password cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(storedHash))
            {
                return Unauthorized("Invalid username or password.");
            }

            bool isPasswordValid = PasswordHasher.VerifyPassword(user.Password, storedHash);

            if (!isPasswordValid)
            {
                return Unauthorized("Invalid username or password.");
            }

            string userSql = @"
        SELECT 
            user_id AS UserId,
            user_Fname AS FirstName,
            user_Lname AS LastName,
            user_Role AS Role,
            user_Status AS Status,
            user_Name AS UserName,
            user_Pass AS Password
        FROM ManageUsers 
        WHERE user_Name = @UserName";

            var userData = await connection.QueryFirstOrDefaultAsync<UserDto>(userSql, new { UserName = user.UserName });

            if (userData == null)
            {
                return Unauthorized("User not found.");
            }


            HttpContext.Session.SetString("UserName", userData.UserName ?? "");
            HttpContext.Session.SetString("UserRole", userData.Role ?? "");

            // Log the login action
            return Ok(new { message = "Login successfully." });
        }


        [HttpGet("SecureCheck")]
        public IActionResult SecureCheck()
        {
            var userName = HttpContext.Session.GetString("UserName") ?? "Not logged in";
            return Ok($"You are: {userName}");
        }

        //Logout to
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear(); // Clear all session data

            return Ok(new { message = "Logout successful" });
        }

        [HttpGet("GetCurrentUser")]
        public IActionResult GetCurrentUser()
        {
            var userName = HttpContext.Session.GetString("UserName");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(userName))
            {
                return Unauthorized(new { message = "User not logged in" });
            }

            return Ok(new { UserName = userName, UserRole = userRole });
        }

        [HttpGet("TestSession")]
        public IActionResult TestSession()
        {
            var userName = HttpContext.Session.GetString("UserName");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(userRole))
            {
                return Unauthorized("Session not set or expired.");
            }

            return Ok(new { UserName = userName, UserRole = userRole });
        }

    }
}

