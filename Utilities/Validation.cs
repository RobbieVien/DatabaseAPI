using Dapper;
using MySqlConnector;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace DatabaseAPI.Utilities
{
    public static class Validation
    {
        private static string _connectionString;

        public static void Initialize(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        }

        public static (bool IsValid, string Message) ValidateTask(DateTime schedDate, int schedNotify)
        {
            int daysDifference = (schedDate - DateTime.UtcNow.Date).Days;
            if (schedNotify < 0 || schedNotify > daysDifference)
            {
                return (false, "sched_notify must be between 0 and the number of days until sched_date.");
            }
            return (true, "Valid sched_notify");
        }
    }
}