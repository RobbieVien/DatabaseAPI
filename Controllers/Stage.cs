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
public class StageController : ControllerBase
{
    private readonly string _connectionString;

    public StageController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }

    // LogAction method (moved from LogsController)
   

    // GetLogs method (optional, if you want to retrieve logs in UserController)


    //----------------------------------------------------------------------------------------------------------------

  

}
