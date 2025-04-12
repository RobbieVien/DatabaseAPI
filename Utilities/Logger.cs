using Dapper;
using MySqlConnector;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace DatabaseAPI.Utilities
{
    public static class Logger
    {
        private static string _connectionString;

        public static void Initialize(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        }

        public static async Task<int> LogAction(HttpContext httpContext, string action, string tableName, int recordId, string details = "")
        {
            if (string.IsNullOrEmpty(_connectionString))
                throw new Exception("Logger is not initialized with a connection string.");

            var userName = httpContext.Session.GetString("UserName") ?? "Unknown";

            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                await connection.ExecuteAsync("SET time_zone = '+08:00';");

                var sql = "INSERT INTO Logs (Action, TableName, RecordId, UserName, Details, Timestamp) VALUES (@Action, @TableName, @RecordId, @UserName, @Details, NOW())";
                var result = await connection.ExecuteAsync(sql, new
                {
                    Action = action,
                    TableName = tableName,
                    RecordId = recordId,
                    UserName = userName,
                    Details = details
                });

                // Call cleanup in a separate connection to prevent packet issues
                using (var cleanupConnection = new MySqlConnection(_connectionString))
                {
                    await cleanupConnection.OpenAsync();
                    await cleanupConnection.ExecuteAsync("CALL CleanupOldLogs();");
                }

                return result;
            }
        }


        public static async Task<int> LogLogin(string userName, string details = "")
        {
            if (string.IsNullOrEmpty(_connectionString))
                throw new Exception("Logger is not initialized with a connection string.");

            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                await connection.ExecuteAsync("SET time_zone = '+08:00';");

                var sql = "INSERT INTO Logs (Action, TableName, RecordId, UserName, Details, Timestamp) VALUES (@Action, @TableName, @RecordId, @UserName, @Details, NOW())";
                var result = await connection.ExecuteAsync(sql, new
                {
                    Action = "Login",
                    TableName = "Users",
                    RecordId = 0,  // No specific record ID for login actions
                    UserName = userName,
                    Details = details
                });

                return result;
            }
        }

        public static async Task<int> LogActionAdd(HttpContext httpContext, string action, string tableName, string details = "")
        {
            if (string.IsNullOrEmpty(_connectionString))
                throw new Exception("Logger is not initialized with a connection string.");

            var userName = httpContext.Session.GetString("UserName") ?? "Unknown";

            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                await connection.ExecuteAsync("SET time_zone = '+08:00';");

                var sql = "INSERT INTO Logs (Action, TableName,  UserName, Details, Timestamp) VALUES (@Action, @TableName,  @UserName, @Details, NOW())";
                var result = await connection.ExecuteAsync(sql, new
                {
                    Action = action,
                    TableName = tableName,
                    UserName = userName,
                    Details = details
                });

                // Call cleanup in a separate connection to prevent packet issues
                using (var cleanupConnection = new MySqlConnection(_connectionString))
                {
                    await cleanupConnection.OpenAsync();
                    await cleanupConnection.ExecuteAsync("CALL CleanupOldLogs();");
                }

                return result;
            }
        }

    }
}
