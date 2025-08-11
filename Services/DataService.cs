using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Systems_One_MQTT_Service.Models;

namespace Systems_One_MQTT_Service.Services
{
    /// <summary>
    /// Data service for accessing SQL Server using Dapper
    /// </summary>
    public class DataService
    {
        private readonly string _connectionString;
        private readonly ILogger<DataService> _logger;
        private readonly string _tableName;

        public DataService(IConfiguration configuration, ILogger<DataService> logger)
        {
            _logger = logger;
            _tableName = configuration["ConnectionFields:Table"] ?? "ItemLog";
            
            // Build connection string from configuration fields
            var server = configuration["ConnectionFields:Server"] ?? "localhost";
            var database = configuration["ConnectionFields:Database"] ?? "Systems_One";
            var userId = configuration["ConnectionFields:UserId"] ?? "sa";
            var password = configuration["ConnectionFields:Password"] ?? "";
            var trustCert = configuration["ConnectionFields:TrustServerCertificate"] ?? "True";

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = server,
                InitialCatalog = database,
                UserID = userId,
                Password = password,
                TrustServerCertificate = bool.Parse(trustCert)
            };

            _connectionString = builder.ConnectionString;
        }

        /// <summary>
        /// Get a database connection
        /// </summary>
        private SqlConnection GetConnection() => new SqlConnection(_connectionString);

        /// <summary>
        /// Test database connectivity
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using var connection = GetConnection();
                await connection.OpenAsync();
                _logger.LogInformation("Database connection test successful");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database connection test failed");
                return false;
            }
        }

        /// <summary>
        /// Get all items from ItemLog table
        /// </summary>
        public async Task<IEnumerable<ItemLog>> GetAllItemsAsync()
        {
            try
            {
                using var connection = GetConnection();
                var sql = $"SELECT * FROM {_tableName}";
                var items = await connection.QueryAsync<ItemLog>(sql);
                _logger.LogInformation("Retrieved {Count} items from {Table}", items.Count(), _tableName);
                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving items from {Table}", _tableName);
                throw;
            }
        }

        /// <summary>
        /// Get items by status
        /// </summary>
        public async Task<IEnumerable<ItemLog>> GetItemsByStatusAsync(string status)
        {
            try
            {
                using var connection = GetConnection();
                var sql = $"SELECT * FROM {_tableName} WHERE Status = @Status";
                var items = await connection.QueryAsync<ItemLog>(sql, new { Status = status });
                _logger.LogInformation("Retrieved {Count} items with status '{Status}' from {Table}", items.Count(), status, _tableName);
                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving items by status from {Table}", _tableName);
                throw;
            }
        }

        /// <summary>
        /// Get unsent items
        /// </summary>
        public async Task<IEnumerable<ItemLog>> GetUnsentItemsAsync()
        {
            try
            {
                using var connection = GetConnection();
                var sql = $"SELECT * FROM {_tableName} WHERE Sent = 0 OR Sent IS NULL";
                var items = await connection.QueryAsync<ItemLog>(sql);
                _logger.LogInformation("Retrieved {Count} unsent items from {Table}", items.Count(), _tableName);
                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unsent items from {Table}", _tableName);
                throw;
            }
        }

        /// <summary>
        /// Mark items as sent
        /// </summary>
        public async Task<int> MarkItemsAsSentAsync(IEnumerable<int> itemIds)
        {
            try
            {
                using var connection = GetConnection();
                var sql = $"UPDATE {_tableName} SET Sent = 1 WHERE Id IN @ItemIds";
                var rowsAffected = await connection.ExecuteAsync(sql, new { ItemIds = itemIds });
                _logger.LogInformation("Marked {Count} items as sent in {Table}", rowsAffected, _tableName);
                return rowsAffected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking items as sent in {Table}", _tableName);
                throw;
            }
        }

        /// <summary>
        /// Get items created within a specific time range
        /// </summary>
        public async Task<IEnumerable<ItemLog>> GetItemsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                using var connection = GetConnection();
                var sql = $"SELECT * FROM {_tableName} WHERE ItemDateTime >= @StartDate AND ItemDateTime <= @EndDate";
                var items = await connection.QueryAsync<ItemLog>(sql, new { StartDate = startDate, EndDate = endDate });
                _logger.LogInformation("Retrieved {Count} items between {StartDate} and {EndDate} from {Table}", 
                    items.Count(), startDate, endDate, _tableName);
                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving items by date range from {Table}", _tableName);
                throw;
            }
        }

        /// <summary>
        /// Get items from the last 15 minutes
        /// </summary>
        public async Task<IEnumerable<ItemLog>> GetItemsFromLast15MinutesAsync()
        {
            try
            {
                var endTime = DateTime.Now;
                var startTime = endTime.AddMinutes(-15);
                
                using var connection = GetConnection();
                var sql = $"SELECT * FROM {_tableName} WHERE ItemDateTime >= @StartTime AND ItemDateTime <= @EndTime";
                var items = await connection.QueryAsync<ItemLog>(sql, new { StartTime = startTime, EndTime = endTime });
                _logger.LogInformation("Retrieved {Count} items from last 15 minutes from {Table}", items.Count(), _tableName);
                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving items from last 15 minutes from {Table}", _tableName);
                throw;
            }
        }

        /// <summary>
        /// Get statistics for items from the last 15 minutes
        /// </summary>
        public async Task<object> GetLast15MinutesStatisticsAsync()
        {
            try
            {
                var endTime = DateTime.Now;
                var startTime = endTime.AddMinutes(-15);
                
                using var connection = GetConnection();
                
                var totalItems = await connection.QuerySingleAsync<int>($"SELECT COUNT(*) FROM {_tableName} WHERE ItemDateTime >= @StartTime AND ItemDateTime <= @EndTime", new { StartTime = startTime, EndTime = endTime });
                var noWeight = await connection.QuerySingleAsync<int>($"SELECT COUNT(*) FROM {_tableName} WHERE ItemDateTime >= @StartTime AND ItemDateTime <= @EndTime AND NoWeight = 1", new { StartTime = startTime, EndTime = endTime });
                var noDimensions = await connection.QuerySingleAsync<int>($"SELECT COUNT(*) FROM {_tableName} WHERE ItemDateTime >= @StartTime AND ItemDateTime <= @EndTime AND NoDimension = 1", new { StartTime = startTime, EndTime = endTime });
                var sent = await connection.QuerySingleAsync<int>($"SELECT COUNT(*) FROM {_tableName} WHERE ItemDateTime >= @StartTime AND ItemDateTime <= @EndTime AND Sent = 1", new { StartTime = startTime, EndTime = endTime });
                var notSent = await connection.QuerySingleAsync<int>($"SELECT COUNT(*) FROM {_tableName} WHERE ItemDateTime >= @StartTime AND ItemDateTime <= @EndTime AND Sent = 0", new { StartTime = startTime, EndTime = endTime });
                var complete = await connection.QuerySingleAsync<int>($"SELECT COUNT(*) FROM {_tableName} WHERE ItemDateTime >= @StartTime AND ItemDateTime <= @EndTime AND Complete = 1", new { StartTime = startTime, EndTime = endTime });
                var valid = await connection.QuerySingleAsync<int>($"SELECT COUNT(*) FROM {_tableName} WHERE ItemDateTime >= @StartTime AND ItemDateTime <= @EndTime AND Valid = 1", new { StartTime = startTime, EndTime = endTime });
                var imageSent = await connection.QuerySingleAsync<int>($"SELECT COUNT(*) FROM {_tableName} WHERE ItemDateTime >= @StartTime AND ItemDateTime <= @EndTime AND ImageSent = 1", new { StartTime = startTime, EndTime = endTime });

                var success = complete; // Assuming success = complete items
                var outOfSpec = totalItems - complete; // Items that are not complete are considered out of spec

                var statistics = new
                {
                    TotalItems = totalItems,
                    NoWeight = noWeight,
                    NoDimensions = noDimensions,
                    Sent = sent,
                    NotSent = notSent,
                    Complete = complete,
                    Valid = valid,
                    Success = success,
                    OutOfSpec = outOfSpec,
                    ImageSent = imageSent,
                    TimeRange = new
                    {
                        StartTime = startTime,
                        EndTime = endTime
                    }
                };

                _logger.LogInformation("Retrieved 15-minute statistics from {Table}: Total={Total}, NoWeight={NoWeight}, NoDimensions={NoDimensions}, Success={Success}", 
                    _tableName, totalItems, noWeight, noDimensions, success);
                return statistics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving 15-minute statistics from {Table}", _tableName);
                throw;
            }
        }
    }
}