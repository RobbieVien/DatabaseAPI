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

    //CourtRecord

    [HttpPost("AddCourtRecord")]
    public async Task<IActionResult> AddCourtRecord([FromBody] CourtRecorddto courtrecord)
    {
        if (courtrecord == null || string.IsNullOrWhiteSpace(courtrecord.RecordCaseNumber))
        {
            return BadRequest("Invalid court record data.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        try
        {
            Console.WriteLine($"Incoming Court Record: Case Number={courtrecord.RecordCaseNumber}, Title={courtrecord.RecordCaseTitle}");
            Console.WriteLine($"Date Filed OCC: {courtrecord.RecordDateFiledOCC}, Date Filed Received: {courtrecord.RecordDateFiledReceived}");

            // Check if a court record with the same case number already exists
            string checkQuery = @"SELECT COUNT(*) FROM COURTRECORD 
                             WHERE rec_Case_Number = @CaseNumber";

            using var checkCmd = new MySqlCommand(checkQuery, con);
            checkCmd.Parameters.AddWithValue("@CaseNumber", courtrecord.RecordCaseNumber.Trim());

            var existingCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
            Console.WriteLine($"Existing Court Record Count for case number '{courtrecord.RecordCaseNumber}': {existingCount}");

            if (existingCount > 0)
            {
                return Conflict("A court record with the same case number already exists.");
            }

            // Insert new court record - convert DateOnly to DateTime for MySQL compatibility
            string insertQuery = @"INSERT INTO COURTRECORD (rec_Case_Number, rec_Case_Title, rec_Date_Filed_Occ, rec_Date_Filed_Received, rec_Transferred, rec_Case_Status, rec_Nature_Case, rec_Nature_Descrip)
                             VALUES (@CaseNumber, @CaseTitle, @RecordDateFiledOcc, @RecordDateFiledReceived, @RecordTransferred, @RecordCaseStatus, @RecordNatureCase, @RecordNatureDescription)";

            using var insertCmd = new MySqlCommand(insertQuery, con);
            insertCmd.Parameters.AddWithValue("@CaseNumber", courtrecord.RecordCaseNumber.Trim());
            insertCmd.Parameters.AddWithValue("@CaseTitle", courtrecord.RecordCaseTitle);

            // Convert DateOnly to DateTime for MySQL compatibility
            DateTime occDateTime = new DateTime(courtrecord.RecordDateFiledOCC.Year,
                                              courtrecord.RecordDateFiledOCC.Month,
                                              courtrecord.RecordDateFiledOCC.Day);

            DateTime receivedDateTime = new DateTime(courtrecord.RecordDateFiledReceived.Year,
                                                   courtrecord.RecordDateFiledReceived.Month,
                                                   courtrecord.RecordDateFiledReceived.Day);

            insertCmd.Parameters.AddWithValue("@RecordDateFiledOcc", occDateTime);
            insertCmd.Parameters.AddWithValue("@RecordDateFiledReceived", receivedDateTime);

            insertCmd.Parameters.AddWithValue("@RecordTransferred", courtrecord.RecordTransfer);
            insertCmd.Parameters.AddWithValue("@RecordCaseStatus", courtrecord.RecordCaseStatus);
            insertCmd.Parameters.AddWithValue("@RecordNatureCase", courtrecord.RecordNatureCase);
            insertCmd.Parameters.AddWithValue("@RecordNatureDescription", courtrecord.RecordNatureDescription);

            await insertCmd.ExecuteNonQueryAsync();
            return Ok("Court record added successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return StatusCode(500, "An error occurred while adding the court record.");
        }
    }


    [HttpPost("AddDirectory")]
    public async Task<IActionResult> AddDirectory([FromBody] DirectoryDto directory)
    {
        if (directory == null || string.IsNullOrWhiteSpace(directory.DirectoryName))
        {
            return BadRequest("Invalid user data.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        // Check if username already exists
        string checkQuery = "SELECT COUNT(*) FROM Directory WHERE direct_name = @DirectoryName";
        using var checkCmd = new MySqlCommand(checkQuery, con);
        checkCmd.Parameters.AddWithValue("@DirectoryName", directory.DirectoryName);
        int userCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

        if (userCount > 0)
        {
            return Conflict("That name already exists.");
        }

        // Insert new user
        string insertQuery = @"INSERT INTO Directory (direct_name, direct_position, direct_contact, direct_email, direct_status)
                              VALUES (@DirectoryName, @DirectoryPosition, @DirectoryContact, @DirectoryEmail, @DirectoryStatus)";
        using var insertCmd = new MySqlCommand(insertQuery, con);
        insertCmd.Parameters.AddWithValue("@DirectoryName", directory.DirectoryName);
        insertCmd.Parameters.AddWithValue("@DirectoryPosition", directory.DirectoryPosition);
        insertCmd.Parameters.AddWithValue("@DirectoryContact", directory.DirectoryContact);
        insertCmd.Parameters.AddWithValue("@DirectoryEmail", directory.DirectoryEmail);
        insertCmd.Parameters.AddWithValue("@DirectoryStatus", directory.DirectoryStatus);

        await insertCmd.ExecuteNonQueryAsync();

        return Ok("User added successfully.");
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

    //DeleteCourRecord
    [HttpDelete("DeleteCourtRecord/{id}")]
    public async Task<IActionResult> DeleteCourtRecord(int id)
    {

        if (id <= 0)
        {
            return BadRequest("Invalid court record ID.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        try
        {
            Console.WriteLine($"Attempting to delete court record with ID: {id}");

            // Get the column name from database schema - adjust this if your primary key has a different name
            string columnName = "id"; // Likely needs to be changed to match your DB schema

            // Verify the actual column name first by querying the schema
            string schemaQuery = "DESCRIBE COURTRECORD";
            using var schemaCmd = new MySqlCommand(schemaQuery, con);
            using var schemaReader = await schemaCmd.ExecuteReaderAsync();

            while (await schemaReader.ReadAsync())
            {
                string field = schemaReader.GetString(0);
                string key = schemaReader.GetString(3);
                Console.WriteLine($"Column: {field}, Key: {key}");
                if (key.Equals("PRI", StringComparison.OrdinalIgnoreCase))
                {
                    columnName = field;
                    break;
                }
            }
            schemaReader.Close();

            Console.WriteLine($"Using primary key column: {columnName}");

            // Delete the court record using the correct column name
            string deleteQuery = $"DELETE FROM COURTRECORD WHERE {columnName} = @Id";

            using var deleteCmd = new MySqlCommand(deleteQuery, con);
            deleteCmd.Parameters.AddWithValue("@Id", id);

            int rowsAffected = await deleteCmd.ExecuteNonQueryAsync();
            Console.WriteLine($"Rows affected: {rowsAffected}");

            if (rowsAffected > 0)
            {
                return Ok($"Court record with ID {id} deleted successfully.");
            }
            else
            {
                return NotFound($"Court record with ID {id} not found or could not be deleted.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting court record: {ex.Message}");
            Console.WriteLine($"Error StackTrace: {ex.StackTrace}");
            return StatusCode(500, $"An error occurred while deleting the court record: {ex.Message}");
        }
    }


    [HttpDelete("DeleteDirectory/{id}")]
    public async Task<IActionResult> DeleteDirectory(int id)
    {
        Console.WriteLine($"DeleteName called with ID: {id}"); // Log when the route is hit

        if (id <= 0)
        {
            return BadRequest("Invalid name ID.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string deleteQuery = "DELETE FROM Directory WHERE directory_Id  = @DirectoryId";
        using var deleteCmd = new MySqlCommand(deleteQuery, con);
        deleteCmd.Parameters.AddWithValue("@DirectoryId", id);

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

    //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    //UPDATE 


    //Users EDIT

    [HttpPut("UserEdit/{id}")]
    public async Task<IActionResult> UserEdit(int id, [FromBody] UserDto user)
    {
        if (id <= 0 || user == null || string.IsNullOrWhiteSpace(user.UserName))
        {
            return BadRequest("Invalid user data.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        // Fixed the query syntax
        string updateQuery = @"UPDATE ManageUsers 
                          SET user_Fname = @FirstName,
                              user_Lname = @LastName,
                              user_Role = @Role,
                              user_Status = @Status,
                              user_Name = @UserName,
                              user_Pass = @Password
                          WHERE user_Id = @Id";

        using var updateCmd = new MySqlCommand(updateQuery, con);
        updateCmd.Parameters.AddWithValue("@FirstName", user.FirstName);
        updateCmd.Parameters.AddWithValue("@LastName", user.LastName);
        updateCmd.Parameters.AddWithValue("@Role", user.Role);
        updateCmd.Parameters.AddWithValue("@Status", user.Status);
        updateCmd.Parameters.AddWithValue("@UserName", user.UserName);
        updateCmd.Parameters.AddWithValue("@Password", user.Password);
        updateCmd.Parameters.AddWithValue("@Id", id);

        int rowsAffected = await updateCmd.ExecuteNonQueryAsync();

        if (rowsAffected > 0)
        {
            return Ok("User updated successfully.");
        }

        return NotFound("No user found with the specified ID.");
    }


    //Categories EDIT
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

    //UpdateCourtRecord
    [HttpPut("UpdateCourtRecord/{id}")]
    public async Task<IActionResult> UpdateCourtRecord(int id, [FromBody] CourtRecorddto courtrecord)
    {
        if (id <= 0 || courtrecord == null || string.IsNullOrWhiteSpace(courtrecord.RecordCaseNumber))
        {
            return BadRequest("Invalid court record data or ID.");
        }

        // Ensure the ID in the path matches the ID in the payload or set it explicitly
        courtrecord.CourtRecordId = id; // Force the ID to match the route parameter

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        try
        {
            Console.WriteLine($"Attempting to update court record with ID: {id}");

            // Check if the court record exists
            string checkQuery = "SELECT COUNT(*) FROM COURTRECORD WHERE id = @Id";

            // First, verify the actual primary key column name
            string columnName = "id"; // Default assumption
            string schemaQuery = "DESCRIBE COURTRECORD";
            using var schemaCmd = new MySqlCommand(schemaQuery, con);
            using var schemaReader = await schemaCmd.ExecuteReaderAsync();

            while (await schemaReader.ReadAsync())
            {
                string field = schemaReader.GetString(0);
                string key = schemaReader.GetString(3);
                if (key.Equals("PRI", StringComparison.OrdinalIgnoreCase))
                {
                    columnName = field;
                    break;
                }
            }
            schemaReader.Close();

            Console.WriteLine($"Using primary key column: {columnName}");

            // Check if record exists
            string existsQuery = $"SELECT COUNT(*) FROM COURTRECORD WHERE {columnName} = @Id";
            using var existsCmd = new MySqlCommand(existsQuery, con);
            existsCmd.Parameters.AddWithValue("@Id", id);

            var existingCount = Convert.ToInt32(await existsCmd.ExecuteScalarAsync());

            if (existingCount == 0)
            {
                return NotFound($"Court record with ID {id} not found.");
            }

            // Check if the updated case number conflicts with another record
            string duplicateQuery = $"SELECT COUNT(*) FROM COURTRECORD WHERE rec_Case_Number = @CaseNumber AND {columnName} != @Id";
            using var duplicateCmd = new MySqlCommand(duplicateQuery, con);
            duplicateCmd.Parameters.AddWithValue("@CaseNumber", courtrecord.RecordCaseNumber.Trim());
            duplicateCmd.Parameters.AddWithValue("@Id", id);

            var duplicateCount = Convert.ToInt32(await duplicateCmd.ExecuteScalarAsync());

            if (duplicateCount > 0)
            {
                return Conflict("Another court record with the same case number already exists.");
            }

            // Convert DateOnly to DateTime for MySQL compatibility
            DateTime occDateTime = new DateTime(courtrecord.RecordDateFiledOCC.Year,
                                              courtrecord.RecordDateFiledOCC.Month,
                                              courtrecord.RecordDateFiledOCC.Day);

            DateTime receivedDateTime = new DateTime(courtrecord.RecordDateFiledReceived.Year,
                                                   courtrecord.RecordDateFiledReceived.Month,
                                                   courtrecord.RecordDateFiledReceived.Day);

            // Update the court record - ID field is not included in the SET clause
            string updateQuery = $@"UPDATE COURTRECORD 
                             SET rec_Case_Number = @CaseNumber,
                                 rec_Case_Title = @CaseTitle,
                                 rec_Date_Filed_Occ = @RecordDateFiledOcc,
                                 rec_Date_Filed_Received = @RecordDateFiledReceived,
                                 rec_Transferred = @RecordTransferred,
                                 rec_Case_Status = @RecordCaseStatus,
                                 rec_Nature_Case = @RecordNatureCase,
                                 rec_Nature_Descrip = @RecordNatureDescription
                             WHERE {columnName} = @Id";

            using var updateCmd = new MySqlCommand(updateQuery, con);
            updateCmd.Parameters.AddWithValue("@Id", id);
            updateCmd.Parameters.AddWithValue("@CaseNumber", courtrecord.RecordCaseNumber.Trim());
            updateCmd.Parameters.AddWithValue("@CaseTitle", courtrecord.RecordCaseTitle);
            updateCmd.Parameters.AddWithValue("@RecordDateFiledOcc", occDateTime);
            updateCmd.Parameters.AddWithValue("@RecordDateFiledReceived", receivedDateTime);
            updateCmd.Parameters.AddWithValue("@RecordTransferred", courtrecord.RecordTransfer);
            updateCmd.Parameters.AddWithValue("@RecordCaseStatus", courtrecord.RecordCaseStatus);
            updateCmd.Parameters.AddWithValue("@RecordNatureCase", courtrecord.RecordNatureCase);
            updateCmd.Parameters.AddWithValue("@RecordNatureDescription", courtrecord.RecordNatureDescription);

            int rowsAffected = await updateCmd.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                return Ok($"Court record with ID {id} updated successfully.");
            }
            else
            {
                return StatusCode(500, "Failed to update the court record.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating court record: {ex.Message}");
            Console.WriteLine($"Error StackTrace: {ex.StackTrace}");
            return StatusCode(500, $"An error occurred while updating the court record: {ex.Message}");
        }
    }


    //UPDATEDIRECTORY
    [HttpPut("DirectoryEdit/{id}")]
    public async Task<IActionResult> DirectoryEdit(int id, [FromBody] DirectoryDto directory)
    {
        if (id <= 0 || directory == null || string.IsNullOrWhiteSpace(directory.DirectoryName))
        {
            return BadRequest("Invalid user data.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        // Fixed the query syntax
        string updateQuery = @"UPDATE Directory 
                          SET direct_name = @DirectoryName,
                              direct_position = @DirectoryPosition,
                              direct_contact = @DirectoryContact,
                              direct_email = @DirectoryEmail,
                              direct_status = @DirectoryStatus
                          WHERE directory_Id  = @Id";

        using var updateCmd = new MySqlCommand(updateQuery, con);
        updateCmd.Parameters.AddWithValue("@DirectoryName", directory.DirectoryName);
        updateCmd.Parameters.AddWithValue("@DirectoryPosition", directory.DirectoryPosition);
        updateCmd.Parameters.AddWithValue("@DirectoryContact", directory.DirectoryContact);
        updateCmd.Parameters.AddWithValue("@DirectoryEmail", directory.DirectoryEmail);
        updateCmd.Parameters.AddWithValue("@DirectoryStatus", directory.DirectoryStatus);
        updateCmd.Parameters.AddWithValue("@Id", id);

        int rowsAffected = await updateCmd.ExecuteNonQueryAsync();

        if (rowsAffected > 0)
        {
            return Ok("User updated successfully.");
        }

        return NotFound("No user found with the specified ID.");
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


    //Get Categories DATAGRIDVIEW from the dashboards
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


    //GET Tasks DATAGRIDVIEW from the Dashboard and Schedules

    [HttpGet("GetTasks")]
    public async Task<IActionResult> GetTasks()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT sched_Id, sched_taskTitle, sched_taskDescription, sched_date, sched_status FROM Tasks";
        using var cmd = new MySqlCommand(query, con);

        using var reader = await cmd.ExecuteReaderAsync();

        var categories = new List<Tasksdto>();
        while (await reader.ReadAsync())
        {
            categories.Add(new Tasksdto
            {
                ScheduleId = Convert.ToInt32(reader["sched_Id"]), // Make sure user_Id is included
                ScheduleTaskTitle = reader["sched_taskTitle"]?.ToString(),
                ScheduleTaskDescription = reader["sched_taskDescription"]?.ToString(),
                ScheduleDate = reader["sched_date"] == DBNull.Value ? default : Convert.ToDateTime(reader["sched_date"]),
                ScheduleStatus = reader["sched_status"]?.ToString()
            });
        }

        return Ok(categories);
    }

    [HttpGet("GetHearing")]
    public async Task<IActionResult> GetHearing()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT hearing_Id, hearing_Case_Title, hearing_Case_Num, hearing_Case_Date, hearing_case_status FROM Hearing";
        using var cmd = new MySqlCommand(query, con);

        using var reader = await cmd.ExecuteReaderAsync();

        var categories = new List<Hearingdto>();
        while (await reader.ReadAsync())
        {
            categories.Add(new Hearingdto
            {
                HearingId = Convert.ToInt32(reader["hearing_Id"]), // Make sure user_Id is included
                HearingCaseTitle = reader["hearing_Case_Title"]?.ToString(),
                HearingCaseNumber = reader["hearing_Case_Num"]?.ToString(),
                HearingCaseDate = reader["hearing_Case_Date"] == DBNull.Value ? default : Convert.ToDateTime(reader["hearing_Case_Date"]),
                HearingCaseStatus = reader["hearing_case_status"]?.ToString()
            });
        }

        return Ok(categories);
    }

    [HttpGet("GetCourtRecords")]
    public async Task<IActionResult> GetCourtRecords()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        try
        {
            // First, verify the actual primary key column name to ensure correct mapping
            string idColumnName = "id"; // Default assumption
            string schemaQuery = "DESCRIBE COURTRECORD";
            using var schemaCmd = new MySqlCommand(schemaQuery, con);
            using var schemaReader = await schemaCmd.ExecuteReaderAsync();

            while (await schemaReader.ReadAsync())
            {
                string field = schemaReader.GetString(0);
                string key = schemaReader.GetString(3);
                if (key.Equals("PRI", StringComparison.OrdinalIgnoreCase))
                {
                    idColumnName = field;
                    break;
                }
            }
            schemaReader.Close();

            string query = $@"SELECT {idColumnName}, rec_Case_Number, rec_Case_Title, rec_Date_Filed_Occ, 
                               rec_Date_Filed_Received, rec_Transferred, rec_Case_Status, 
                               rec_Nature_Case, rec_Nature_Descrip 
                        FROM COURTRECORD";

            using var cmd = new MySqlCommand(query, con);
            using var reader = await cmd.ExecuteReaderAsync();

            var courtRecords = new List<CourtRecorddto>();

            while (await reader.ReadAsync())
            {
                courtRecords.Add(new CourtRecorddto
                {
                    CourtRecordId = Convert.ToInt32(reader[idColumnName]),
                    RecordCaseNumber = reader["rec_Case_Number"]?.ToString(),
                    RecordCaseTitle = reader["rec_Case_Title"]?.ToString(),
                    RecordDateFiledOCC = reader["rec_Date_Filed_Occ"] == DBNull.Value
                        ? default
                        : DateOnly.FromDateTime(Convert.ToDateTime(reader["rec_Date_Filed_Occ"])),
                    RecordDateFiledReceived = reader["rec_Date_Filed_Received"] == DBNull.Value
                        ? default
                        : DateOnly.FromDateTime(Convert.ToDateTime(reader["rec_Date_Filed_Received"])),
                    RecordTransfer = reader["rec_Transferred"]?.ToString(),
                    RecordCaseStatus = reader["rec_Case_Status"]?.ToString(),
                    RecordNatureCase = reader["rec_Nature_Case"]?.ToString(),
                    RecordNatureDescription = reader["rec_Nature_Descrip"]?.ToString()
                });
            }

            return Ok(courtRecords);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving court records: {ex.Message}");
            return StatusCode(500, "An error occurred while retrieving court records.");
        }
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

    // IN DASHBOARD COUNT TASKS
    [HttpGet("CountTasks")]
    public async Task<IActionResult> CountTasks()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT COUNT(*) FROM Tasks";
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

    // IN DASHBOARD COUNT HEARINGS
    [HttpGet("CountHearings")]
    public async Task<IActionResult> CountHearings()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT COUNT(*) FROM Hearing";
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

    //Count Active Case Records

    [HttpGet("CountCaseRecordsActive")]
    public async Task<IActionResult> CountCaseRecords()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT COUNT(*) FROM COURTRECORD WHERE rec_Case_Status IN ('Active', 'Archived')";
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

    //Count Disposed Case Records
    [HttpGet("CountCaseRecordsDisposed")]
    public async Task<IActionResult> CountCaseRecordsDisposed()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT COUNT(*) FROM COURTRECORD WHERE rec_Case_Status IN ('Disposed')";
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

    //Count Archived Case Records
    [HttpGet("CountCaseRecordsArchived")]
    public async Task<IActionResult> CountCaseRecordsArchived()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT COUNT(*) FROM COURTRECORD WHERE rec_Case_Status IN ('Archived')";
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

    //Notifications Counts
    [HttpGet("NotificationCounts")]
    public async Task<IActionResult> GetNotificationCount()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = @"SELECT COUNT(*) FROM (
                        SELECT sched_Id FROM Tasks
                        WHERE CAST(sched_date AS DATE) = CAST(CURDATE() AS DATE)
                        AND TRIM(sched_status) = 'Pending'
                        UNION ALL
                        SELECT hearing_Id FROM Hearing
                        WHERE CAST(hearing_Case_Date AS DATE) = CAST(CURDATE() AS DATE)
                        AND TRIM(hearing_case_status) = 'Pending'
                    ) AS CombinedCount";

        using var cmd = new MySqlCommand(query, con);

        try
        {
            int notificationCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(notificationCount);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    //Notifications DATA
    [HttpGet("NotificationsData")]
    public async Task<IActionResult> GetNotificationDetails()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = @"SELECT 
                        sched_taskTitle AS Title,
                        sched_taskDescription AS Description,
                        TRIM(sched_status) AS Status
                    FROM Tasks 
                    WHERE CAST(sched_date AS DATE) = CAST(CURDATE() AS DATE)
                    AND TRIM(sched_status) = 'Pending'

                    UNION ALL

                    SELECT 
                        TRIM(hearing_Case_Title) AS Title,
                        TRIM(hearing_Case_Num) AS Description,
                        TRIM(hearing_case_status) AS Status
                    FROM Hearing 
                    WHERE CAST(hearing_Case_Date AS DATE) = CAST(CURDATE() AS DATE)
                    AND TRIM(hearing_case_status) = 'Pending'";

        using var cmd = new MySqlCommand(query, con);

        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            var results = new List<object>();

            while (await reader.ReadAsync())
            {
                results.Add(new
                {
                    Title = reader["Title"].ToString(),
                    Description = reader["Description"].ToString(),
                    Status = reader["Status"].ToString()
                });
            }

            return Ok(results);
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

public class CourtRecorddto
{
    public int CourtRecordId { get; set; }
    public string RecordCaseNumber { get; set; }
    public string RecordCaseTitle { get; set; }
    public DateOnly RecordDateFiledOCC { get; set; }
    public DateOnly RecordDateFiledReceived { get; set; }
    public string RecordTransfer { get; set; }
    public string RecordCaseStatus { get; set; }
    public string RecordNatureCase { get; set; }
    public string RecordNatureDescription { get; set; }
}

public class DirectoryDto
{
    public int DirectoryId { get; set; }
    public string DirectoryName { get; set; }
    public string DirectoryPosition { get; set; }
    public string DirectoryContact { get; set; }
    public string DirectoryEmail { get; set; }
    public string DirectoryStatus { get; set; }
}