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

    [HttpPost("AddUser")]
    public async Task<IActionResult> AddUser([FromBody] UserDto user)
    {
        if (user == null || string.IsNullOrWhiteSpace(user.UserName))
        {
            return BadRequest("Invalid user data.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        // Check if username already exists
        string checkQuery = "SELECT COUNT(*) FROM ManageUsers WHERE user_Name = @UserName";
        using var checkCmd = new MySqlCommand(checkQuery, con);
        checkCmd.Parameters.AddWithValue("@UserName", user.UserName);
        int userCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

        if (userCount > 0)
        {
            return Conflict("Username already exists.");
        }

        // Insert new user
        string insertQuery = @"INSERT INTO ManageUsers (user_Fname, user_Lname, user_Role, user_Status, user_Name, user_Pass)
                              VALUES (@FirstName, @LastName, @Role, @Status, @UserName, @Password)";
        using var insertCmd = new MySqlCommand(insertQuery, con);
        insertCmd.Parameters.AddWithValue("@FirstName", user.FirstName);
        insertCmd.Parameters.AddWithValue("@LastName", user.LastName);
        insertCmd.Parameters.AddWithValue("@Role", user.Role);
        insertCmd.Parameters.AddWithValue("@Status", user.Status);
        insertCmd.Parameters.AddWithValue("@UserName", user.UserName);
        insertCmd.Parameters.AddWithValue("@Password", user.Password);

        await insertCmd.ExecuteNonQueryAsync();

        return Ok("User added successfully.");
    }


    [HttpDelete("DeleteUser/{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        Console.WriteLine($"DeleteUser called with ID: {id}"); // Log when the route is hit

        if (id <= 0)
        {
            return BadRequest("Invalid user ID.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string deleteQuery = "DELETE FROM ManageUsers WHERE user_Id = @UserId";
        using var deleteCmd = new MySqlCommand(deleteQuery, con);
        deleteCmd.Parameters.AddWithValue("@UserId", id);

        int rowsAffected = await deleteCmd.ExecuteNonQueryAsync();

        if (rowsAffected > 0)
        {
            return Ok("User has been deleted successfully.");
        }
        else
        {
            return NotFound("No user found with the selected ID.");
        }
    }


    [HttpGet("Test")]
    public IActionResult Test()
    {
        return Ok("API is working!");
    }

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




// Model to receive login requests
public class UserLogin
{
    public string user_Name { get; set; } = string.Empty; // Initialize to avoid null warning
    public string user_Pass { get; set; } = string.Empty; // Initialize to avoid null warning
}
public class UserDto
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Role { get; set; }
    public string Status { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
}