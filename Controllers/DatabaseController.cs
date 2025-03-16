using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Text.Json.Serialization;
using Dapper;
using Mysqlx.Crud;

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

        // First, get the user ID and role
        using var cmd = new MySqlCommand("SELECT user_id, user_Role FROM ManageUsers WHERE user_Name = @USERNAME AND user_Pass = @PASSWORD", connection);
        cmd.Parameters.AddWithValue("@USERNAME", user.user_Name);
        cmd.Parameters.AddWithValue("@PASSWORD", user.user_Pass);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int userId = Convert.ToInt32(reader["user_id"]);
            string role = reader["user_Role"]?.ToString() ?? "Unknown";

            // Close the reader before executing another command
            await reader.CloseAsync();

            // Log the successful login
            await LogAction("Login", "ManageUsers", userId, user.user_Name);

            return Ok(new { Success = true, Role = role, UserId = userId });
        }

        return Unauthorized("Incorrect username or password");
    }

    //----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    //ADD

    //User
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

        // Log the action
        await LogAction($"User {user.UserName} has been added.", "ManageUsers", 0, "Admin");

        return Ok("User added successfully.");
    }


    //CATEGORY
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

        // Log the action
        await LogAction($"Category {category.CategoryLegalCase} has been added.", "Category", 0);

        return Ok("Category added successfully.");
    }

    //TASKS
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

            // Log the action
            await LogAction($"Task {tasks.ScheduleTaskTitle} has been added.", "Tasks", 0, "Admin");

            return Ok("Task added successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return StatusCode(500, "An error occurred while adding the task.");
        }
    }

    //HEARING
    [HttpPost("AddHearing")]
    public async Task<IActionResult> AddHearing([FromBody] Hearingdto hearing)
    {
        if (hearing == null || string.IsNullOrWhiteSpace(hearing.HearingCaseTitle) ||
            string.IsNullOrWhiteSpace(hearing.HearingCaseNumber))
        {
            return BadRequest("Invalid Hearing data.");
        }

        hearing.HearingCaseDate = DateTime.Now.ToString("yyyy-MM-dd");
        hearing.HearingCaseTime = DateTime.Now.ToString("HH:mm:ss");

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        try
        {
            string insertQuery = @"INSERT INTO Hearing (hearing_Case_Title, hearing_Case_Num, hearing_Case_Date, hearing_Case_Time, hearing_case_status)
                           VALUES (@CaseTitle, @CaseNumber, @CaseDate, @CaseTime, @CaseStatus)";

            using var insertCmd = new MySqlCommand(insertQuery, con);
            insertCmd.Parameters.AddWithValue("@CaseTitle", hearing.HearingCaseTitle.Trim());
            insertCmd.Parameters.AddWithValue("@CaseNumber", hearing.HearingCaseNumber.Trim());
            insertCmd.Parameters.AddWithValue("@CaseDate", hearing.HearingCaseDate);
            insertCmd.Parameters.AddWithValue("@CaseTime", hearing.HearingCaseTime);
            insertCmd.Parameters.AddWithValue("@CaseStatus", hearing.HearingCaseStatus);

            await insertCmd.ExecuteNonQueryAsync();

            // Log the action
            await LogAction($"Hearing {hearing.HearingCaseTitle} has been added.", "Hearing", 0);

            return Ok("Hearing added successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return StatusCode(500, "An error occurred while adding the hearing.");
        }
    }


    //DIRECTORY
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

        // Log the action
        await LogAction($"Directory {directory.DirectoryName} has been added.", "Directory", 0);

        return Ok("Directory added successfully.");
    }

    //COURTRECORD
    [HttpPost("AddCourtRecord")]
    public async Task<IActionResult> AddCourtRecord([FromBody] CourtRecorddto courtrecord, [FromHeader(Name = "UserName")] string userName = "System")
    {
        if (courtrecord == null || string.IsNullOrWhiteSpace(courtrecord.RecordCaseNumber))
        {
            return BadRequest("Invalid court record data.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        try
        {
            Console.WriteLine("Attempting to add new court record.");

            string duplicateQuery = "SELECT COUNT(*) FROM COURTRECORD WHERE rec_Case_Number = @CaseNumber";
            using var duplicateCmd = new MySqlCommand(duplicateQuery, con);
            duplicateCmd.Parameters.AddWithValue("@CaseNumber", courtrecord.RecordCaseNumber.Trim());

            var duplicateCount = Convert.ToInt32(await duplicateCmd.ExecuteScalarAsync());

            if (duplicateCount > 0)
            {
                return Conflict("A court record with the same case number already exists.");
            }

            object occDateValue = !string.IsNullOrEmpty(courtrecord.RecordDateFiledOCC) ? courtrecord.RecordDateFiledOCC : DBNull.Value;
            object receivedDateValue = !string.IsNullOrEmpty(courtrecord.RecordDateFiledReceived) ? courtrecord.RecordDateFiledReceived : DBNull.Value;

            string insertQuery = @"INSERT INTO COURTRECORD (
                        rec_Case_Number,
                        rec_Case_Title,
                        rec_Date_Filed_Occ,
                        rec_Date_Filed_Received,
                        rec_Transferred,
                        rec_Case_Status,
                        rec_Nature_Case,
                        rec_Nature_Descrip,
                        rec_Time_Inputted,
                        rec_Date_Inputted)
                    VALUES (
                        @CaseNumber,
                        @CaseTitle,
                        @RecordDateFiledOcc,
                        @RecordDateFiledReceived,
                        @RecordTransferred,
                        @RecordCaseStatus,
                        @RecordNatureCase,
                        @RecordNatureDescription,
                        CURRENT_TIME(),
                        CURRENT_DATE());
                    SELECT LAST_INSERT_ID();";

            using var insertCmd = new MySqlCommand(insertQuery, con);
            insertCmd.Parameters.AddWithValue("@CaseNumber", courtrecord.RecordCaseNumber.Trim());
            insertCmd.Parameters.AddWithValue("@CaseTitle", courtrecord.RecordCaseTitle);
            insertCmd.Parameters.AddWithValue("@RecordDateFiledOcc", occDateValue);
            insertCmd.Parameters.AddWithValue("@RecordDateFiledReceived", receivedDateValue);
            insertCmd.Parameters.AddWithValue("@RecordTransferred", courtrecord.RecordTransfer ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("@RecordCaseStatus", courtrecord.RecordCaseStatus ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("@RecordNatureCase", courtrecord.RecordNatureCase ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("@RecordNatureDescription", courtrecord.RecordNatureDescription ?? (object)DBNull.Value);

            int newRecordId = Convert.ToInt32(await insertCmd.ExecuteScalarAsync());

            if (newRecordId > 0)
            {
                await LogAction($"Added {courtrecord.RecordCaseTitle} successfully.", "COURTRECORD", newRecordId, userName);
                return Ok(new { Message = $"Added {courtrecord.RecordCaseTitle} successfully.", RecordId = newRecordId });
            }
            else
            {
                return StatusCode(500, "Failed to add the court record.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding court record: {ex.Message}");
            return StatusCode(500, new { Message = "An error occurred while adding the court record.", ErrorDetails = ex.Message });
        }
    }



    //REPORT SIDE

    //ADDCaseColumn
    [HttpPost("AddNatureCaseColumn")]
    public async Task<IActionResult> AddNatureCaseColumn()
    {
        string addColumnQuery = @"
        ALTER TABLE Report 
        ADD COLUMN IF NOT EXISTS Report_NatureCase NCHAR(50) NULL;";
        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.ExecuteAsync(addColumnQuery);
                return Ok("Column added successfully");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    //AddForeignKeyConstraint
    [HttpPost("AddRequiredColumns")]
    public async Task<IActionResult> AddRequiredColumns()
    {
        string addColumnsQuery = @"
        ALTER TABLE Report 
        ADD COLUMN IF NOT EXISTS CourtRecord_LinkId INT NULL,
        ADD COLUMN IF NOT EXISTS CaseCount INT NOT NULL DEFAULT 1;";

        string addForeignKeyQuery = @"
        ALTER TABLE Report 
        ADD CONSTRAINT FK_Report_CourtRecord
        FOREIGN KEY (CourtRecord_LinkId)
        REFERENCES COURTRECORD (courtRecord_Id);";

        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                // Add columns
                await connection.ExecuteAsync(addColumnsQuery);

                // Check if foreign key already exists
                var checkForeignKeyQuery = @"
                SELECT CONSTRAINT_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
                WHERE TABLE_NAME = 'Report' AND CONSTRAINT_NAME = 'FK_Report_CourtRecord';";
                var foreignKeyExists = await connection.QueryFirstOrDefaultAsync<string>(checkForeignKeyQuery);

                // Add foreign key if it doesn't exist
                if (foreignKeyExists == null)
                {
                    await connection.ExecuteAsync(addForeignKeyQuery);
                }

                return Ok("Required columns and constraints added successfully");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    //ETO WALA LANG TESTING LANG FOR REPORT
    [HttpPost("PopulateReportTable")]
    public async Task<IActionResult> PopulateReportTable()
    {
        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                // First, get all available nature cases from COURTRECORD that aren't yet in Report table
                string selectQuery = @"
                SELECT cr.rec_Nature_Case, cr.courtRecord_Id
                FROM COURTRECORD cr
                WHERE cr.rec_Nature_Case IS NOT NULL
                AND NOT EXISTS (
                    SELECT 1 FROM Report r
                    WHERE r.CourtRecord_LinkId = cr.courtRecord_Id
                );";

                var records = await connection.QueryAsync<dynamic>(selectQuery);
                int processedCount = 0;

                foreach (var record in records)
                {
                    // Check if this nature case already exists in Report table
                    string checkExistingQuery = @"
                    SELECT Report_Id, CaseCount 
                    FROM Report 
                    WHERE Report_NatureCase = @natureCase;";

                    var existingReport = await connection.QueryFirstOrDefaultAsync<dynamic>(
                        checkExistingQuery,
                        new { natureCase = record.rec_Nature_Case });

                    if (existingReport == null)
                    {
                        // Insert new record if nature case doesn't exist
                        string insertQuery = @"
                        INSERT INTO Report (Report_NatureCase, CourtRecord_LinkId, CaseCount)
                        VALUES (@natureCase, @courtRecordId, 1);";

                        await connection.ExecuteAsync(
                            insertQuery,
                            new
                            {
                                natureCase = record.rec_Nature_Case,
                                courtRecordId = record.courtRecord_Id
                            });
                    }
                    else
                    {
                        // Update existing record to increment the case count
                        string updateQuery = @"
                        UPDATE Report
                        SET CaseCount = CaseCount + 1
                        WHERE Report_Id = @reportId;";

                        await connection.ExecuteAsync(
                            updateQuery,
                            new { reportId = existingReport.Report_Id });
                    }

                    processedCount++;
                }

                return Ok($"{processedCount} court records processed. Report table updated with case counts.");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    //--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    //DELETE

    //USER

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

    //CATEGORY

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

    //HEARING
    [HttpDelete("DeleteHearing/{id}")]
    public async Task<IActionResult> DeleteHearing(int id, [FromHeader(Name = "UserName")] string userName = "System")
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
            // Log the deletion
            await LogAction("Delete", "Hearing", id, userName);

            return Ok("Hearing has been deleted successfully.");
        }
        else
        {
            return NotFound("No hearing found with the selected ID.");
        }
    }

    //TASK
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

    //COURTRECORD
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

    //DIRECTORY
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

    //UPDATE OR UPDATE

    //Users
    [HttpPut("UserEdit/{id}")]
    public async Task<IActionResult> UserEdit(int id, [FromBody] UserDto user, [FromHeader(Name = "UserRole")] string userRole, [FromHeader(Name = "UserName")] string userName)
    {
        // Check if the user has the required role
        if (userRole != "Admin" && userRole != "ChiefAdmin")
        {
            return StatusCode(403, "Only Admin and ChiefAdmin roles can edit users.");
        }

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
            // Log the user edit action
            await LogAction("Update", "ManageUsers", id, userName ?? "Unknown");

            return Ok("User updated successfully.");
        }

        return NotFound("No user found with the specified ID.");
    }

    //CATEGORIES
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

    //COURTRECORD
    [HttpPut("UpdateCourtRecord/{id}")]
    public async Task<IActionResult> UpdateCourtRecord(int id, [FromBody] CourtRecorddto courtrecord)
    {
        if (id <= 0 || courtrecord == null || string.IsNullOrWhiteSpace(courtrecord.RecordCaseNumber))
        {
            return BadRequest("Invalid court record data or ID.");
        }

        courtrecord.CourtRecordId = id;

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        try
        {
            Console.WriteLine($"Attempting to update court record with ID: {id}");

            string columnName = "id";
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

            string existsQuery = $"SELECT COUNT(*) FROM COURTRECORD WHERE {columnName} = @Id";
            using var existsCmd = new MySqlCommand(existsQuery, con);
            existsCmd.Parameters.AddWithValue("@Id", id);

            var existingCount = Convert.ToInt32(await existsCmd.ExecuteScalarAsync());

            if (existingCount == 0)
            {
                return NotFound($"Court record with ID {id} not found.");
            }

            string duplicateQuery = $"SELECT COUNT(*) FROM COURTRECORD WHERE rec_Case_Number = @CaseNumber AND {columnName} != @Id";
            using var duplicateCmd = new MySqlCommand(duplicateQuery, con);
            duplicateCmd.Parameters.AddWithValue("@CaseNumber", courtrecord.RecordCaseNumber.Trim());
            duplicateCmd.Parameters.AddWithValue("@Id", id);

            var duplicateCount = Convert.ToInt32(await duplicateCmd.ExecuteScalarAsync());

            if (duplicateCount > 0)
            {
                return Conflict("Another court record with the same case number already exists.");
            }

            // Process date and time values
            object occDateValue = !string.IsNullOrEmpty(courtrecord.RecordDateFiledOCC)
                ? courtrecord.RecordDateFiledOCC
                : DBNull.Value;

            object receivedDateValue = !string.IsNullOrEmpty(courtrecord.RecordDateFiledReceived)
                ? courtrecord.RecordDateFiledReceived
                : DBNull.Value;

            string updateQuery = $@"UPDATE COURTRECORD 
                 SET rec_Case_Number = @CaseNumber,
                     rec_Case_Title = @CaseTitle,
                     rec_Date_Filed_Occ = @RecordDateFiledOcc,
                     rec_Date_Filed_Received = @RecordDateFiledReceived,
                     rec_Transferred = @RecordTransferred,
                     rec_Case_Status = @RecordCaseStatus,
                     rec_Nature_Case = @RecordNatureCase,
                     rec_Nature_Descrip = @RecordNatureDescription,
                     rec_Time_Inputted = @RecordTimeInputted,
                     rec_Date_Inputted = @RecordDateInputted
                 WHERE {columnName} = @Id";

            using var updateCmd = new MySqlCommand(updateQuery, con);
            updateCmd.Parameters.AddWithValue("@Id", id);
            updateCmd.Parameters.AddWithValue("@CaseNumber", courtrecord.RecordCaseNumber.Trim());
            updateCmd.Parameters.AddWithValue("@CaseTitle", courtrecord.RecordCaseTitle);
            updateCmd.Parameters.AddWithValue("@RecordDateFiledOcc", occDateValue);
            updateCmd.Parameters.AddWithValue("@RecordDateFiledReceived", receivedDateValue);
            updateCmd.Parameters.AddWithValue("@RecordTransferred", !string.IsNullOrEmpty(courtrecord.RecordTransfer) ? courtrecord.RecordTransfer : DBNull.Value);
            updateCmd.Parameters.AddWithValue("@RecordCaseStatus", !string.IsNullOrEmpty(courtrecord.RecordCaseStatus) ? courtrecord.RecordCaseStatus : DBNull.Value);
            updateCmd.Parameters.AddWithValue("@RecordNatureCase", !string.IsNullOrEmpty(courtrecord.RecordNatureCase) ? courtrecord.RecordNatureCase : DBNull.Value);
            updateCmd.Parameters.AddWithValue("@RecordNatureDescription", !string.IsNullOrEmpty(courtrecord.RecordNatureDescription) ? courtrecord.RecordNatureDescription : DBNull.Value);
            updateCmd.Parameters.AddWithValue("@RecordTimeInputted", !string.IsNullOrEmpty(courtrecord.RecordTimeInputted) ? courtrecord.RecordTimeInputted : DBNull.Value);
            updateCmd.Parameters.AddWithValue("@RecordDateInputted", !string.IsNullOrEmpty(courtrecord.RecordDateInputted) ? courtrecord.RecordDateInputted : DBNull.Value);

            int rowsAffected = await updateCmd.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                return Ok(new
                {
                    Message = $"Court record with ID {id} updated successfully."
                });
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
            return StatusCode(500, new
            {
                Message = "An error occurred while updating the court record.",
                ErrorDetails = ex.Message
            });
        }
    }

    //DIRECTORY
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

    //COURTHEARING
    [HttpPut("UpdateCourtHearing/{id}")]
    public async Task<IActionResult> UpdateCourtHearing(int id, [FromBody] Hearingdto hearing, [FromHeader(Name = "UserName")] string userName = "System")
    {
        Console.WriteLine($"Incoming ID from URL: {id}");
        Console.WriteLine($"Incoming Hearing ID: {hearing?.HearingId}");

        if (hearing == null)
        {
            return BadRequest("Invalid hearing data.");
        }

        if (hearing.HearingId == 0)
        {
            hearing.HearingId = id;
        }

        if (id != hearing.HearingId)
        {
            Console.WriteLine("ID mismatch detected.");
            return BadRequest("ID mismatch.");
        }

        string query = @"UPDATE Hearing 
                 SET hearing_Case_Title = @CaseTitle, 
                     hearing_Case_Num = @CaseNumber, 
                     hearing_case_status = @CaseStatus 
                 WHERE hearing_Id = @HearingId";
        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                var existingHearing = await connection.QueryFirstOrDefaultAsync<Hearingdto>(
                    "SELECT * FROM Hearing WHERE hearing_Id = @HearingId",
                    new { HearingId = id });

                if (existingHearing == null)
                {
                    return NotFound("Hearing not found.");
                }

                var result = await connection.ExecuteAsync(query, new
                {
                    CaseTitle = hearing.HearingCaseTitle ?? existingHearing.HearingCaseTitle,
                    CaseNumber = hearing.HearingCaseNumber ?? existingHearing.HearingCaseNumber,
                    CaseStatus = hearing.HearingCaseStatus ?? existingHearing.HearingCaseStatus,
                    HearingId = id
                });

                if (result > 0)
                {
                    // Log the successful update
                    await LogAction("Update", "Hearing", id, userName);

                    return Ok("Hearing updated successfully.");
                }

                return StatusCode(500, "No changes were made.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return StatusCode(500, new { message = $"Error updating hearing: {ex.Message}" });
        }
    }

    //------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    //DATAGRIDVIEW GETTING THE DATA

    //GETUsers
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

    //GetCategories
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

    //GETTasks
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

    //GETHearing
    [HttpGet("GetHearing")]
    public async Task<IActionResult> GetHearing()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT hearing_Id, hearing_Case_Title, hearing_Case_Num, hearing_Case_Date, hearing_Case_Time, hearing_case_status FROM Hearing";
        using var cmd = new MySqlCommand(query, con);

        using var reader = await cmd.ExecuteReaderAsync();

        var hearings = new List<Hearingdto>();
        while (await reader.ReadAsync())
        {
            hearings.Add(new Hearingdto
            {
                HearingId = Convert.ToInt32(reader["hearing_Id"]),
                HearingCaseTitle = reader["hearing_Case_Title"]?.ToString(),
                HearingCaseNumber = reader["hearing_Case_Num"]?.ToString(),
                HearingCaseDate = reader["hearing_Case_Date"]?.ToString() ?? string.Empty,
                HearingCaseTime = reader["hearing_Case_Time"]?.ToString() ?? string.Empty,
                HearingCaseStatus = reader["hearing_case_status"]?.ToString()
            });
        }

        return Ok(hearings);
    }

    //GETReport
    [HttpGet("GetReportData")]
    public async Task<IActionResult> GetReportData()
    {
        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // The INSERT statement cannot be run together with a SELECT in the same query
                // So we need to split them

                // First, make sure all nature cases from COURTRECORD are in the Report table
                string insertQuery = @"
                INSERT INTO Report (Report_NatureCase, CaseCount)
                SELECT 
                    cr.rec_Nature_Case, 
                    COUNT(cr.courtRecord_Id) AS CaseCount
                FROM COURTRECORD cr
                WHERE cr.rec_Nature_Case IS NOT NULL
                    AND NOT EXISTS (
                        SELECT 1 FROM Report r 
                        WHERE r.Report_NatureCase = cr.rec_Nature_Case
                    )
                GROUP BY cr.rec_Nature_Case;";

                await connection.ExecuteAsync(insertQuery);

                // Update case counts for existing records
                string updateQuery = @"
                UPDATE Report r
                JOIN (
                    SELECT 
                        rec_Nature_Case, 
                        COUNT(courtRecord_Id) AS ActualCount
                    FROM COURTRECORD
                    WHERE rec_Nature_Case IS NOT NULL
                    GROUP BY rec_Nature_Case
                ) counts ON r.Report_NatureCase = counts.rec_Nature_Case
                SET r.CaseCount = counts.ActualCount;";

                await connection.ExecuteAsync(updateQuery);

                // Now retrieve the updated report data
                string selectQuery = @"
                SELECT 
                    r.Report_Id,
                    r.Report_NatureCase,
                    r.CaseCount,
                    r.CourtRecord_LinkId
                FROM Report r
                ORDER BY r.CaseCount DESC;";

                var results = await connection.QueryAsync(selectQuery);
                return Ok(results);
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Message = "Error fetching report data",
                ErrorDetails = ex.Message,
                InnerException = ex.InnerException?.Message,
                StackTrace = ex.StackTrace
            });
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


    //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    //COUNTING

    //COUNT-USERS
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

    //COUNT-TASKS
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

    //COUNT-HEARING
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

    //UPCOMING-COUNT-TASKS
    [HttpGet("UpcomingTasks")]
    public async Task<IActionResult> UpcomingTasks()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT COUNT(*) FROM Tasks WHERE sched_date > CURDATE()";
        using var cmd = new MySqlCommand(query, con);

        try
        {
            int UpcomingTasksCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(UpcomingTasksCount);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    //DUETODAY-COUNT-TASKS
    [HttpGet("DueTodayTasks")]
    public async Task<IActionResult> DueTodayTasks()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT COUNT(*) FROM Tasks WHERE sched_date = CURDATE()";
        using var cmd = new MySqlCommand(query, con);

        try
        {
            int DueTodayTasksCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(DueTodayTasksCount);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    //OVERDUE-COUNT-TASKS
    [HttpGet("OverDueTasks")]
    public async Task<IActionResult> OverDueTasks()
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string query = "SELECT COUNT(*) FROM Tasks WHERE sched_date < CURDATE()";
        using var cmd = new MySqlCommand(query, con);

        try
        {
            int OverDueTasksCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(OverDueTasksCount);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    //COUNT-ACTIVE-CASE-RECORDS
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

    //COUNT-DISPOSED-CASE-RECORDS
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

    //COUNT-ARCHIVED-CASE-RECORDS
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

    //NOTIFICATIONS-COUNTS
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

    //-----------------------------------------------------------------------------------------------------------------------------------------------------------------

    //FILTERING DATA

    //FILTER-COURT-RECORD
    [HttpGet("FilterRecords")]
    public async Task<ActionResult<IEnumerable<CourtRecorddto>>> GetFilteredRecords([FromQuery] string selectedFilter = "All")
    {
        string modifiedConnectionString = _connectionString;

        if (!modifiedConnectionString.Contains("Allow Zero Datetime=true"))
        {
            var connBuilder = new MySqlConnectionStringBuilder(modifiedConnectionString)
            {
                AllowZeroDateTime = true,
                ConvertZeroDateTime = true
            };
            modifiedConnectionString = connBuilder.ConnectionString;
        }

        await using var con = new MySqlConnection(modifiedConnectionString);
        await con.OpenAsync();

        string query = selectedFilter switch
        {
            "Today" => @"
    SELECT 
        courtRecord_Id,
        rec_Case_Number, 
        rec_Case_Title, 
        rec_Date_Inputted,
        rec_Time_Inputted,
        rec_Date_Filed_Occ,
        rec_Date_Filed_Received,
        rec_Transferred,
        rec_Case_Status,
        rec_Nature_Case,
        rec_Nature_Descrip
    FROM COURTRECORD
    WHERE DATE(rec_Date_Inputted) = CURDATE()",
            _ => @"
    SELECT 
        courtRecord_Id,
        rec_Case_Number, 
        rec_Case_Title, 
        rec_Date_Inputted,
        rec_Time_Inputted,
        rec_Date_Filed_Occ,
        rec_Date_Filed_Received,
        rec_Transferred,
        rec_Case_Status,
        rec_Nature_Case,
        rec_Nature_Descrip
    FROM COURTRECORD"
        };

        await using var cmd = new MySqlCommand(query, con);

        try
        {
            var results = new List<CourtRecorddto>();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var courtRecord = new CourtRecorddto
                {
                    CourtRecordId = reader.IsDBNull(reader.GetOrdinal("courtRecord_Id")) ? 0 : reader.GetInt32(reader.GetOrdinal("courtRecord_Id")),
                    RecordCaseNumber = reader.IsDBNull(reader.GetOrdinal("rec_Case_Number")) ? string.Empty : reader.GetString(reader.GetOrdinal("rec_Case_Number")),
                    RecordCaseTitle = reader.IsDBNull(reader.GetOrdinal("rec_Case_Title")) ? string.Empty : reader.GetString(reader.GetOrdinal("rec_Case_Title")),
                    RecordTransfer = reader.IsDBNull(reader.GetOrdinal("rec_Transferred")) ? string.Empty : reader.GetString(reader.GetOrdinal("rec_Transferred")),
                    RecordCaseStatus = reader.IsDBNull(reader.GetOrdinal("rec_Case_Status")) ? string.Empty : reader.GetString(reader.GetOrdinal("rec_Case_Status")),
                    RecordNatureCase = reader.IsDBNull(reader.GetOrdinal("rec_Nature_Case")) ? string.Empty : reader.GetString(reader.GetOrdinal("rec_Nature_Case")),
                    RecordNatureDescription = reader.IsDBNull(reader.GetOrdinal("rec_Nature_Descrip")) ? string.Empty : reader.GetString(reader.GetOrdinal("rec_Nature_Descrip"))
                };

                // Handle rec_Date_Inputted as string
                try
                {
                    var dateInputted = reader.GetValue(reader.GetOrdinal("rec_Date_Inputted"));
                    if (dateInputted != DBNull.Value && dateInputted != null)
                    {
                        DateTime dt = Convert.ToDateTime(dateInputted);
                        courtRecord.RecordDateInputted = dt.ToString("yyyy-MM-dd");
                    }
                    else
                    {
                        courtRecord.RecordDateInputted = string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Date Parsing Error: {ex.Message}");
                    courtRecord.RecordDateInputted = string.Empty;
                }

                // Handle rec_Time_Inputted as string
                try
                {
                    var timeInputted = reader.GetValue(reader.GetOrdinal("rec_Time_Inputted"));
                    if (timeInputted != DBNull.Value && timeInputted != null)
                    {
                        if (timeInputted is TimeSpan timeSpan)
                        {
                            courtRecord.RecordTimeInputted = timeSpan.ToString(@"hh\:mm\:ss");
                        }
                        else if (timeInputted is DateTime dateTime)
                        {
                            courtRecord.RecordTimeInputted = dateTime.ToString("HH:mm:ss");
                        }
                        else
                        {
                            courtRecord.RecordTimeInputted = timeInputted.ToString();
                        }
                    }
                    else
                    {
                        courtRecord.RecordTimeInputted = string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Time Parsing Error: {ex.Message}");
                    courtRecord.RecordTimeInputted = string.Empty;
                }

                // Handle rec_Date_Filed_Occ as string
                try
                {
                    var dateFiledOCC = reader.GetValue(reader.GetOrdinal("rec_Date_Filed_Occ"));
                    if (dateFiledOCC != DBNull.Value && dateFiledOCC != null)
                    {
                        DateTime dt = Convert.ToDateTime(dateFiledOCC);
                        courtRecord.RecordDateFiledOCC = dt.ToString("yyyy-MM-dd");
                    }
                    else
                    {
                        courtRecord.RecordDateFiledOCC = null;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error Parsing rec_Date_Filed_Occ: {ex.Message}");
                    courtRecord.RecordDateFiledOCC = null;
                }

                // Handle rec_Date_Filed_Received as string
                try
                {
                    var dateFiledReceived = reader.GetValue(reader.GetOrdinal("rec_Date_Filed_Received"));
                    if (dateFiledReceived != DBNull.Value && dateFiledReceived != null)
                    {
                        DateTime dt = Convert.ToDateTime(dateFiledReceived);
                        courtRecord.RecordDateFiledReceived = dt.ToString("yyyy-MM-dd");
                    }
                    else
                    {
                        courtRecord.RecordDateFiledReceived = null;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error Parsing rec_Date_Filed_Received: {ex.Message}");
                    courtRecord.RecordDateFiledReceived = null;
                }

                results.Add(courtRecord);
            }

            return Ok(results);
        }
        catch (Exception ex)
        {
            string innerExceptionMessage = ex.InnerException != null ? ex.InnerException.Message : "No inner exception";
            return StatusCode(500, new
            {
                Message = "Error filtering records",
                ErrorDetails = ex.Message,
                InnerException = innerExceptionMessage,
                StackTrace = ex.StackTrace
            });
        }
    }

    //FILTER-COURT-HEARINGS
    [HttpGet("FilterHearings")]
    public async Task<IActionResult> FilterHearings(string All)
    {
        string query;

        switch (All)
        {
            case "Today":
                query = @"SELECT hearing_Id AS HearingId, hearing_Case_Title AS HearingCaseTitle, 
                         hearing_Case_Num AS HearingCaseNumber, hearing_Case_Date AS HearingCaseDate, 
                         TIME_FORMAT(hearing_Case_Time, '%H:%i:%s') AS HearingCaseTime, hearing_case_status AS HearingCaseStatus
                  FROM Hearing 
                  WHERE hearing_Case_Date = CURDATE()";
                break;
            case "This Week":
                query = @"SELECT hearing_Id AS HearingId, hearing_Case_Title AS HearingCaseTitle, 
                         hearing_Case_Num AS HearingCaseNumber, hearing_Case_Date AS HearingCaseDate, 
                         TIME_FORMAT(hearing_Case_Time, '%H:%i:%s') AS HearingCaseTime, hearing_case_status AS HearingCaseStatus
                  FROM Hearing 
                  WHERE YEARWEEK(hearing_Case_Date, 1) = YEARWEEK(CURDATE(), 1)";
                break;
            default:
                query = @"SELECT hearing_Id AS HearingId, hearing_Case_Title AS HearingCaseTitle, 
                         hearing_Case_Num AS HearingCaseNumber, hearing_Case_Date AS HearingCaseDate, 
                         TIME_FORMAT(hearing_Case_Time, '%H:%i:%s') AS HearingCaseTime, hearing_case_status AS HearingCaseStatus
                  FROM Hearing";
                break;
        }

        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                var hearings = await connection.QueryAsync<Hearingdto>(query);
                return Ok(hearings);
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error filtering hearings: {ex.Message}" });
        }
    }



    //----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------


    //LOGS

    // Single unified logging method
    private async Task<int>LogAction(string action, string tableName, int recordId, string userName = "Unknown")
    {
        using var connection = new MySqlConnection(_connectionString);
        var sql = "INSERT INTO Logs (Action, TableName, RecordId, UserName) VALUES (@Action, @TableName, @RecordId, @UserName)";

        return await connection.ExecuteAsync(sql, new
        {
            Action = action,
            TableName = tableName,
            RecordId = recordId,
            UserName = userName
        });
    }

   

    [HttpGet("GetLogs")]
    public async Task<ActionResult<IEnumerable<LogsDto>>>GetLogs()
    {
        using var connection = new MySqlConnection(_connectionString);
        var sql = "SELECT * FROM Logs ORDER BY Timestamp DESC";
        var logs = await connection.QueryAsync<LogsDto>(sql);
        return Ok(logs);
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

    //USER
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

    //CATEGORY
    public class Categorydto
    {
        public int CategoryId { get; set; }
        public string CategoryLegalCase { get; set; }
        public string CategoryRepublicAct { get; set; }
        public string CategoryNatureCase { get; set; }
    }

    //TASK
    public class Tasksdto
    {
        public int ScheduleId { get; set; }
        public string ScheduleTaskTitle { get; set; }
        public string ScheduleTaskDescription { get; set; }
        public DateTime ScheduleDate { get; set; }
        public string ScheduleStatus { get; set; }
    }

    //HEARING
    public class Hearingdto
    {
        public int HearingId { get; set; }
        public string HearingCaseTitle { get; set; }
        public string HearingCaseNumber { get; set; }
        public string HearingCaseStatus { get; set; }
        public string HearingCaseDate { get; set; } = string.Empty; // Format: "yyyy-MM-dd"
        public string HearingCaseTime { get; set; } = string.Empty; // Format: "HH:mm:ss"
}

    //COURTRECORD
    public class CourtRecorddto
{
    public int CourtRecordId { get; set; }
    public string RecordCaseNumber { get; set; } = string.Empty;
    public string RecordCaseTitle { get; set; } = string.Empty;
    public string RecordDateInputted { get; set; } = string.Empty; // Format: "yyyy-MM-dd"
    public string RecordTimeInputted { get; set; } = string.Empty; // Format: "HH:mm:ss"
    public string? RecordDateFiledOCC { get; set; }  // Format: "yyyy-MM-dd" 
    public string? RecordDateFiledReceived { get; set; }  // Format: "yyyy-MM-dd"
    public string RecordTransfer { get; set; } = string.Empty;
    public string RecordCaseStatus { get; set; } = string.Empty;
    public string RecordNatureCase { get; set; } = string.Empty;
    public string RecordNatureDescription { get; set; } = string.Empty;
}

    //DIRECTORY
    public class DirectoryDto
    {
        public int DirectoryId { get; set; }
        public string DirectoryName { get; set; }
        public string DirectoryPosition { get; set; }
        public string DirectoryContact { get; set; }
        public string DirectoryEmail { get; set; }
        public string DirectoryStatus { get; set; }
    }

    //NOT-USED-YET-REPORT
    public class ReportDto
{
    public int ReportId { get; set; }
    public string ReportNatureCase { get; set; }
    public string DirectoryPosition { get; set; }
    public int CourtRecord_LinkId { get; set; }

}

    //LOGS-DTO
    public class LogsDto
    {
        public int LogId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public int RecordId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
