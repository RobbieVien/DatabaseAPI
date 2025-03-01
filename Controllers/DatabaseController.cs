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

    // LOGIN

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





    //----------------------------------------------------------------------------------------------------------------------------------------------------------------------------



    // ADD User IN MANAGE USERS

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

    // ADD CATEGORY
    [HttpPost("AddCategory")]
    public async Task<IActionResult> AddCategory([FromBody] Categorydto category)
    {
        if (category == null || string.IsNullOrWhiteSpace(category.CategoryLegalCase))
        {
            return BadRequest("Invalid category data.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        // Insert new category
        string insertQuery = @"INSERT INTO Category (cat_legalcase, cat_republicAct, cat_natureCase)
                          VALUES (@LegalCase, @RepublicAct, @NatureCase)";
        using var insertCmd = new MySqlCommand(insertQuery, con);
        insertCmd.Parameters.AddWithValue("@LegalCase", category.CategoryLegalCase);
        insertCmd.Parameters.AddWithValue("@RepublicAct", category.CategoryRepublicAct);
        insertCmd.Parameters.AddWithValue("@NatureCase", category.CategoryNatureCase);

        await insertCmd.ExecuteNonQueryAsync();

        return Ok("Category added successfully.");
    }




    //--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------



    //DELETING USER IN MANAGE USER

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
    
    //DELETE IN CATEGORY

    [HttpDelete("DeleteCategory/{id}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        Console.WriteLine($"DeleteUser called with ID: {id}"); // Log when the route is hit

        if (id <= 0)
        {
            return BadRequest("Invalid user ID.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string deleteQuery = "DELETE FROM Category WHERE cat_Id = @CategoryId";
        using var deleteCmd = new MySqlCommand(deleteQuery, con);
        deleteCmd.Parameters.AddWithValue("@CategoryId", id);

        int rowsAffected = await deleteCmd.ExecuteNonQueryAsync();

        if (rowsAffected > 0)
        {
            return Ok("Category has been deleted successfully.");
        }
        else
        {
            return NotFound("No category found with the selected ID.");
        }
    }

    //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    //UPDATE

    [HttpPut("EditCategory/{id}")]
    public async Task<IActionResult> EditCategory(int id, [FromBody] Categorydto category)
    {
        if (id <= 0 || category == null || string.IsNullOrWhiteSpace(category.CategoryLegalCase))
        {
            return BadRequest("Invalid category data.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string updateQuery = @"UPDATE Category
                            SET cat_legalcase = @LegalCase,
                                cat_republicAct = @RepublicAct,
                                cat_natureCase = @NatureCase
                            WHERE cat_Id = @Id";

        using var updateCmd = new MySqlCommand(updateQuery, con);
        updateCmd.Parameters.AddWithValue("@LegalCase", category.CategoryLegalCase);
        updateCmd.Parameters.AddWithValue("@RepublicAct", category.CategoryRepublicAct);
        updateCmd.Parameters.AddWithValue("@NatureCase", category.CategoryNatureCase);
        updateCmd.Parameters.AddWithValue("@Id", id);

        int rowsAffected = await updateCmd.ExecuteNonQueryAsync();

        if (rowsAffected > 0)
        {
            return Ok("Category updated successfully.");
        }

        return NotFound("No category found with the specified ID.");
    }


    //------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    //DATAGRIDVIEW GET USERS

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

    [HttpGet("GetCategories")]
    public async Task<IActionResult> GetCategories()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT cat_Id, cat_legalcase, cat_republicAct, cat_natureCase FROM Category";
        using var cmd = new MySqlCommand(query, con);

        using var reader = await cmd.ExecuteReaderAsync();

        var categories = new List<Categorydto>();
        while (await reader.ReadAsync())
        {
            categories.Add(new Categorydto
            {
                CategoryId = Convert.ToInt32(reader["cat_Id"]), // Make sure user_Id is included
                CategoryLegalCase = reader["cat_legalcase"]?.ToString(),
                CategoryRepublicAct = reader["cat_republicAct"]?.ToString(),
                CategoryNatureCase = reader["cat_natureCase"]?.ToString()
            });
        }

        return Ok(categories);
    }

    //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // IN DASHBOARD COUNT USERS
    [HttpGet("CountUsers")]
    public async Task<IActionResult> CountUsers()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT COUNT(*) FROM ManageUsers";
        using var cmd = new MySqlCommand(query, con);

        try
        {
            int userCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(userCount);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

}




//-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

// DTO Query
// Model to receive login requests
public class UserLogin
{
    public string user_Name { get; set; } = string.Empty; // Initialize to avoid null warning
    public string user_Pass { get; set; } = string.Empty; // Initialize to avoid null warning
}

//ADDING USERS
public class UserDto
{
    public int UserId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Role { get; set; }
    public string Status { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
}

//ADDING CATRGORY
public class Categorydto
{
    public int CategoryId { get; set; }
    public string CategoryLegalCase { get; set; }
    public string CategoryRepublicAct { get; set; }
    public string CategoryNatureCase { get; set; }
}