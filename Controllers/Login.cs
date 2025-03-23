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

            string sql = "SELECT user_Pass FROM ManageUsers WHERE user_Name = @UserName";
            string storedHash = await connection.QueryFirstOrDefaultAsync<string>(sql, new { UserName = user.UserName });

            Console.WriteLine($"🔍 Entered Password: {user.Password}");
            Console.WriteLine($"🔍 Stored Hash: {storedHash}");

            if (string.IsNullOrWhiteSpace(user.Password))
            {
                return BadRequest("Password cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(storedHash))
            {
                return Unauthorized("Invalid username or password.");
            }

            // ✅ Verify password
            bool isPasswordValid = PasswordHasher.VerifyPassword(user.Password, storedHash);
            Console.WriteLine($"🔍 Password Match: {isPasswordValid}");

            if (!isPasswordValid)
            {
                return Unauthorized("Invalid username or password.");
            }

            return Ok("Login successful.");
        }
    }
}
