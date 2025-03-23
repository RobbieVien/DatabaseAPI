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

        public static async Task<int> LogAction(string action, string tableName, int recordId, string userName = "Unknown", string details = "")
        {
            if (string.IsNullOrEmpty(_connectionString))
                throw new System.Exception("Logger is not initialized with a connection string.");

            using var connection = new MySqlConnection(_connectionString);
            var sql = "INSERT INTO Logs (Action, TableName, RecordId, UserName, Details, Timestamp) VALUES (@Action, @TableName, @RecordId, @UserName, @Details, NOW())";
            return await connection.ExecuteAsync(sql, new
            {
                Action = action,
                TableName = tableName,
                RecordId = recordId,
                UserName = userName,
                Details = details
            });
        }
    }
}
