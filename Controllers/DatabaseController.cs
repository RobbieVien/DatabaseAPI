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

    //ADD COURTRECORD

    [HttpPost("AddTasks")]
    public async Task<IActionResult> AddTasks([FromBody] Tasksdto tasks)
    {
        if (tasks == null || string.IsNullOrWhiteSpace(tasks.ScheduleTaskTitle))
        {
            return BadRequest("Invalid Task data.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        try
        {
            Console.WriteLine($"Incoming Task: Title={tasks.ScheduleTaskTitle}, Date={tasks.ScheduleDate:yyyy-MM-dd}, Time={tasks.ScheduleDate:HH:mm:ss}");

            // Direct check for task with same title AND date - simplified query
            string checkQuery = @"SELECT COUNT(*) FROM Tasks 
                             WHERE sched_taskTitle = @TaskTitle 
                             AND DATE(sched_date) = DATE(@Date)";

            using var checkCmd = new MySqlCommand(checkQuery, con);
            checkCmd.Parameters.AddWithValue("@TaskTitle", tasks.ScheduleTaskTitle.Trim());
            checkCmd.Parameters.AddWithValue("@Date", tasks.ScheduleDate);

            var existingCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
            Console.WriteLine($"Existing Task Count for same title and date: {existingCount}");

            if (existingCount > 0)
            {
                return Conflict("A task with the same title and date already exists.");
            }

            // Insert new task
            string insertQuery = @"INSERT INTO Tasks (sched_taskTitle, sched_taskDescription, sched_date, sched_status)
                           VALUES (@TaskTitle, @TaskDescription, @Date, @Status)";

            using var insertCmd = new MySqlCommand(insertQuery, con);
            insertCmd.Parameters.AddWithValue("@TaskTitle", tasks.ScheduleTaskTitle.Trim());
            insertCmd.Parameters.AddWithValue("@TaskDescription", tasks.ScheduleTaskDescription);
            insertCmd.Parameters.AddWithValue("@Date", tasks.ScheduleDate);
            insertCmd.Parameters.AddWithValue("@Status", tasks.ScheduleStatus);

            await insertCmd.ExecuteNonQueryAsync();
            return Ok("Task added successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return StatusCode(500, "An error occurred while adding the task.");
        }
    }

    // HEARING


    [HttpPost("AddHearing")]
    public async Task<IActionResult> AddHearing([FromBody] Hearingdto hearing)
    {
         if (hearing == null || string.IsNullOrWhiteSpace(hearing.HearingCaseTitle) || 
        string.IsNullOrWhiteSpace(hearing.HearingCaseNumber))
    {
        return BadRequest("Invalid Hearing data.");
    }
    
    using var con = new MySqlConnection(_connectionString);
    await con.OpenAsync();
    
    try
    {
        Console.WriteLine($"Incoming Hearing: Title={hearing.HearingCaseTitle}, Number={hearing.HearingCaseNumber}, Date={hearing.HearingCaseDate:yyyy-MM-dd},  Date={hearing.HearingCaseDate:HH:mm:ss}");
        
        // Check if a hearing with the same title and case number already exists on the same date
        string checkQuery = @"SELECT COUNT(*) FROM Hearing 
                             WHERE hearing_Case_Title = @CaseTitle 
                             AND hearing_Case_Num = @CaseNumber
                             AND DATE(hearing_Case_Date) = DATE(@CaseDate)";
        
        using var checkCmd = new MySqlCommand(checkQuery, con);
        checkCmd.Parameters.AddWithValue("@CaseTitle", hearing.HearingCaseTitle.Trim());
        checkCmd.Parameters.AddWithValue("@CaseNumber", hearing.HearingCaseNumber.Trim());
        checkCmd.Parameters.AddWithValue("@CaseDate", hearing.HearingCaseDate);
        
        var existingCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
        Console.WriteLine($"Existing Hearing Count for same title, number and date: {existingCount}");
        
        if (existingCount > 0)
        {
            return Conflict("A hearing with the same title, case number and date already exists.");
        }
        
        // Insert new hearing
        string insertQuery = @"INSERT INTO Hearing (hearing_Case_Title, hearing_Case_Num, hearing_Case_Date, hearing_case_status)
                           VALUES (@CaseTitle, @CaseNumber, @CaseDate, @CaseStatus)";
        
        using var insertCmd = new MySqlCommand(insertQuery, con);
        insertCmd.Parameters.AddWithValue("@CaseTitle", hearing.HearingCaseTitle.Trim());
        insertCmd.Parameters.AddWithValue("@CaseNumber", hearing.HearingCaseNumber.Trim());
        insertCmd.Parameters.AddWithValue("@CaseDate", hearing.HearingCaseDate);
        insertCmd.Parameters.AddWithValue("@CaseStatus", hearing.HearingCaseStatus);
        
        await insertCmd.ExecuteNonQueryAsync();
        return Ok("Hearing added successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        return StatusCode(500, "An error occurred while adding the hearing.");
    }
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

    //DeleteHearing 
    [HttpDelete("DeleteHearing/{id}")]
    public async Task<IActionResult> DeleteHearing(int id)
    {
        Console.WriteLine($"DeleteHearing called with ID: {id}"); // Log when the route is hit

        if (id <= 0)
        {
            return BadRequest("Invalid hearing ID.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string deleteQuery = "DELETE FROM Hearing WHERE hearing_Id = @HearingId";
        using var deleteCmd = new MySqlCommand(deleteQuery, con);
        deleteCmd.Parameters.AddWithValue("@HearingId", id);

        int rowsAffected = await deleteCmd.ExecuteNonQueryAsync();

        if (rowsAffected > 0)
        {
            return Ok("Hearing has been deleted successfully.");
        }
        else
        {
            return NotFound("No hearing found with the selected ID.");
        }
    }

    //DeleteTask
    [HttpDelete("Deletetasks/{id}")]
    public async Task<IActionResult> Deletetasks(int id)
    {
        Console.WriteLine($"DeleteTask called with ID: {id}"); // Log when the route is hit

        if (id <= 0)
        {
            return BadRequest("Invalid task ID.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string deleteQuery = "DELETE FROM Tasks WHERE sched_Id = @ScheduleId";
        using var deleteCmd = new MySqlCommand(deleteQuery, con);
        deleteCmd.Parameters.AddWithValue("@ScheduleId", id);

        int rowsAffected = await deleteCmd.ExecuteNonQueryAsync();

        if (rowsAffected > 0)
        {
            return Ok("Task has been deleted successfully.");
        }
        else
        {
            return NotFound("No Task found with the selected ID.");
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

public class Tasksdto
{
    public int ScheduleId { get; set; }
    public string ScheduleTaskTitle { get; set; }
    public string ScheduleTaskDescription { get; set; }
    public DateTime ScheduleDate { get; set; }
    public string ScheduleStatus { get; set; }
}
public class Hearingdto
{
    public int HearingId { get; set; }
    public string HearingCaseTitle { get; set; }
    public string HearingCaseNumber { get; set; }
    public DateTime HearingCaseDate { get; set; }
    public string HearingCaseStatus { get; set; }
}