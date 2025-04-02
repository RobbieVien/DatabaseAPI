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
public class CategoryController : ControllerBase
{
    private readonly string _connectionString;

    public CategoryController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }

    [HttpPost("AddCategory")]
    public async Task<IActionResult> AddCategory([FromBody] Categorydto category)
    {
        if (category == null || string.IsNullOrWhiteSpace(category.CategoryLegalCase))
        {
            return BadRequest("Invalid category data.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        string insertQuery = @"INSERT INTO Category (cat_legalcase, cat_republicAct, cat_natureCase)
                           VALUES (@LegalCase, @RepublicAct, @NatureCase)";

        int rowsAffected = await con.ExecuteAsync(insertQuery, new
        {
            LegalCase = category.CategoryLegalCase,
            RepublicAct = category.CategoryRepublicAct,
            NatureCase = category.CategoryNatureCase
        });

        if (rowsAffected > 0)
        {
            int newCategoryId = await con.ExecuteScalarAsync<int>("SELECT LAST_INSERT_ID()");

            string categoryDetails = $"Legal Case: {category.CategoryLegalCase}, Republic Act: {category.CategoryRepublicAct}, Nature Case: {category.CategoryNatureCase}";

            await Logger.LogAction("Add", "Category", newCategoryId, "System", categoryDetails);

            category.CategoryId = newCategoryId;
            return Ok(category);
        }

        return BadRequest("Failed to add category.");
    }



    //CATEGORIES
    [HttpPut("EditCategory/{id}")]
    public async Task<IActionResult> EditCategory(
      int id,
      [FromBody] EditCategoryRequest request,
      [FromHeader(Name = "UserName")] string userName)
    {
        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        // Fetch old category details
        string selectQuery = @"SELECT 
        cat_legalcase AS CategoryLegalCase,
        cat_republicAct AS CategoryRepublicAct,
        cat_natureCase AS CategoryNatureCase 
        FROM Category 
        WHERE cat_Id = @Id";

        var oldCategory = await con.QueryFirstOrDefaultAsync<Categorydto>(selectQuery, new { Id = id });

        if (oldCategory == null)
        {
            Console.WriteLine($"No category found for ID: {id}");
            return NotFound("No category found with the specified ID.");
        }

        // Ensure null values are handled
        oldCategory.CategoryLegalCase ??= "";
        oldCategory.CategoryRepublicAct ??= "";
        oldCategory.CategoryNatureCase ??= "";

        request.Category.CategoryLegalCase ??= "";
        request.Category.CategoryRepublicAct ??= "";
        request.Category.CategoryNatureCase ??= "";

        // Update database
        string updateQuery = @"UPDATE Category
            SET cat_legalcase = @LegalCase,
                cat_republicAct = @RepublicAct,
                cat_natureCase = @NatureCase
            WHERE cat_Id = @Id";

        int rowsAffected = await con.ExecuteAsync(updateQuery, new
        {
            LegalCase = request.Category.CategoryLegalCase,
            RepublicAct = request.Category.CategoryRepublicAct,
            NatureCase = request.Category.CategoryNatureCase,
            Id = id
        });

        if (rowsAffected > 0)
        {
            Console.WriteLine($"Category {id} updated successfully by {userName}.");

            // Collect change details
            List<string> changes = new List<string>();

            if (oldCategory.CategoryLegalCase != request.Category.CategoryLegalCase)
            {
                changes.Add($"Updated Legal Case: \"{oldCategory.CategoryLegalCase}\" → \"{request.Category.CategoryLegalCase}\"");
            }
            if (oldCategory.CategoryRepublicAct != request.Category.CategoryRepublicAct)
            {
                changes.Add($"Updated Republic Act: \"{oldCategory.CategoryRepublicAct}\" → \"{request.Category.CategoryRepublicAct}\"");
            }
            if (oldCategory.CategoryNatureCase != request.Category.CategoryNatureCase)
            {
                changes.Add($"Updated Nature Case: \"{oldCategory.CategoryNatureCase}\" → \"{request.Category.CategoryNatureCase}\"");
            }

            if (changes.Count > 0)
            {
                string logMessage = $"Updated category (ID: {id})";
                string details = string.Join(", ", changes);
                await Logger.LogAction(logMessage, "Category", id, userName, details);
            }
            request.Category.CategoryId = id;
            return Ok(request.Category);
        }

        return NotFound("No category found with the specified ID.");
    }

    [HttpDelete("DeleteCategory/{id}")]
    public async Task<IActionResult> DeleteCategory(
        int id,
        [FromHeader(Name = "UserName")] string userName)
    {
        if (id <= 0)
        {
            return BadRequest("Invalid category ID.");
        }

        using var con = new MySqlConnection(_connectionString);
        await con.OpenAsync();

        // Retrieve the category details before deletion
        string selectQuery = @"SELECT 
        cat_Id AS CategoryId,
        cat_legalcase AS CategoryLegalCase,
        cat_republicAct AS CategoryRepublicAct,
        cat_natureCase AS CategoryNatureCase 
        FROM Category WHERE cat_Id = @CategoryId";

        var category = await con.QueryFirstOrDefaultAsync<Categorydto>(selectQuery, new { CategoryId = id });

        if (category == null)
        {
            Console.WriteLine($"No category found for ID: {id}");
            return NotFound("No category found with the specified ID.");
        }

        // Delete the category
        string deleteQuery = "DELETE FROM Category WHERE cat_Id = @CategoryId";
        int rowsAffected = await con.ExecuteAsync(deleteQuery, new { CategoryId = id });

        if (rowsAffected > 0)
        {
            Console.WriteLine($"Category {id} deleted successfully by {userName}.");

            // Collect deletion details
            string details = $"Deleted category (ID: {id}): " +
                             $"Legal Case: \"{category.CategoryLegalCase}\", " +
                             $"Republic Act: \"{category.CategoryRepublicAct}\", " +
                             $"Nature Case: \"{category.CategoryNatureCase}\"";

            await Logger.LogAction("Deleted category", "Category", id, userName, details);

            return Ok(new
            {
                Message = "Category deleted successfully.",
                DeletedData = category
            });
        }

        return StatusCode(500, "An error occurred while deleting the category.");
    }



    //ForTheDatagridview
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

    [HttpGet("search")]
    public async Task<IActionResult> SearchCategories([FromQuery] string query)
    {
        using var connection = new MySqlConnection(_connectionString);
        string sql = @"SELECT * FROM Category 
                   WHERE cat_legalcase LIKE @Query
                      OR cat_republicAct LIKE @Query
                      OR cat_natureCase LIKE @Query";

        var categories = await connection.QueryAsync<Categorydto>(sql, new { Query = $"%{query}%" });

        return Ok(categories);
    }

}
