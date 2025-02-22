using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

[Route("api/[controller]")]
[ApiController]
public class DatabaseController : ControllerBase
{
    private readonly string _connectionString;

    public DatabaseController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty; // Prevent null warning
    }

    [HttpPost("Login")]
    public async Task<IActionResult> Login([FromBody] UserLogin user)
    {
        if (string.IsNullOrWhiteSpace(user.user_Name) || string.IsNullOrWhiteSpace(user.user_Pass))
        {
            return BadRequest("Invalid credentials");
        }

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = new MySqlCommand("SELECT user_Role FROM ManageUsers WHERE user_Name = @USERNAME AND user_Pass = @PASSWORD", connection);
        cmd.Parameters.AddWithValue("@USERNAME", user.user_Name);
        cmd.Parameters.AddWithValue("@PASSWORD", user.user_Pass);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            string role = reader["user_Role"]?.ToString() ?? "Unknown";
            return Ok(new { Success = true, Role = role });
        }

        return Unauthorized("Incorrect username or password");
    }
}

// Model to receive login requests
public class UserLogin
{
    public string user_Name { get; set; } = string.Empty; // Initialize to avoid null warning
    public string user_Pass { get; set; } = string.Empty; // Initialize to avoid null warning
}
