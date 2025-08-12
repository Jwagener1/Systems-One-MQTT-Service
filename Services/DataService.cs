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
        /// Get statistics for items from the last 15 minutes using optimized single query approach
        /// </summary>
        public async Task<object> GetLast15MinutesStatisticsAsync()
        {
            try
            {
                var endTime = DateTime.Now;
                var startTime = endTime.AddMinutes(-15);
                
                using var connection = GetConnection();
                
                // Single optimized query to get all statistics at once, with NULL handling
                const string statsSql = @"
                    SELECT
                        COUNT(*) AS TotalItems,
                        ISNULL(SUM(CASE WHEN Weight = 0 OR Weight IS NULL THEN 1 ELSE 0 END), 0) AS NoWeight,
                        ISNULL(SUM(CASE WHEN (Length = 0 OR Width = 0 OR Height = 0) 
                                  OR Length IS NULL OR Width IS NULL OR Height IS NULL 
                            THEN 1 ELSE 0 END), 0) AS NoDimensions,
                        ISNULL(SUM(CASE WHEN Valid = 1 THEN 1 ELSE 0 END), 0) AS Success,
                        ISNULL(SUM(CASE WHEN ItemSpec = 1 THEN 1 ELSE 0 END), 0) AS OutOfSpec,
                        ISNULL(SUM(CASE WHEN ItemCount > 1 THEN 1 ELSE 0 END), 0) AS MoreThanOneItem,
                        ISNULL(SUM(CASE WHEN Barcode = 'NOREAD' THEN 1 ELSE 0 END), 0) AS NoReads,
                        ISNULL(SUM(CASE WHEN Barcode <> 'NOREAD' AND Barcode IS NOT NULL THEN 1 ELSE 0 END), 0) AS GoodReads,
                        ISNULL(SUM(CASE WHEN Sent = 1 THEN 1 ELSE 0 END), 0) AS Sent,
                        ISNULL(SUM(CASE WHEN Sent = 0 OR Sent IS NULL THEN 1 ELSE 0 END), 0) AS NotSent
                    FROM dbo.ItemLog
                    WHERE ItemDateTime >= @StartTime 
                      AND ItemDateTime < @EndTime";

                var result = await connection.QuerySingleAsync(statsSql, new { StartTime = startTime, EndTime = endTime });

                var statistics = new
                {
                    TotalItems = (int)result.TotalItems,
                    NoWeight = (int)result.NoWeight,
                    GoodReads = (int)result.GoodReads,
                    NoReads = (int)result.NoReads,
                    NoDimensions = (int)result.NoDimensions,
                    Success = (int)result.Success,
                    OutOfSpec = (int)result.OutOfSpec,
                    MoreThanOneItem = (int)result.MoreThanOneItem,
                    NotSent = (int)result.NotSent,
                    Sent = (int)result.Sent,
                    TimeRange = new
                    {
                        StartTime = startTime,
                        EndTime = endTime,
                        DurationMinutes = (endTime - startTime).TotalMinutes
                    }
                };

                _logger.LogInformation("Retrieved {DurationMinutes}-minute statistics from {Table}: Total={Total}, NoWeight={NoWeight}, GoodReads={GoodReads}, NoReads={NoReads}, Success={Success}", 
                    (endTime - startTime).TotalMinutes, _tableName, statistics.TotalItems, statistics.NoWeight, statistics.GoodReads, statistics.NoReads, statistics.Success);
                
                return statistics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving statistics from {Table}", _tableName);
                throw;
            }
        }
    }
}