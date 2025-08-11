using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Systems_One_MQTT_Service.Models;
using System.Globalization;

namespace Systems_One_MQTT_Service.Services
{
    /// <summary>
    /// Service for monitoring system health including disk space, memory, and other system metrics
    /// </summary>
    public class SystemMonitoringService
    {
        private readonly ILogger<SystemMonitoringService> _logger;
        private readonly IConfiguration _configuration;
        private readonly bool _enableSystemMonitoring;
        private readonly double _diskSpaceWarningThreshold;

        public SystemMonitoringService(IConfiguration configuration, ILogger<SystemMonitoringService> logger)
        {
            _logger = logger;
            _configuration = configuration;
            _enableSystemMonitoring = bool.Parse(configuration["SystemMonitoring:Enabled"] ?? "true");
            
            // Use culture-invariant parsing for the threshold value
            var thresholdString = configuration["SystemMonitoring:DiskSpaceWarningThreshold"] ?? "80.0";
            if (!double.TryParse(thresholdString, NumberStyles.Float, CultureInfo.InvariantCulture, out _diskSpaceWarningThreshold))
            {
                _logger.LogWarning("Invalid DiskSpaceWarningThreshold value '{Value}', using default 80.0", thresholdString);
                _diskSpaceWarningThreshold = 80.0;
            }
            
            _logger.LogInformation("SystemMonitoringService initialized - Enabled: {Enabled}, Warning Threshold: {Threshold}%", 
                _enableSystemMonitoring, _diskSpaceWarningThreshold);
        }

        /// <summary>
        /// Gets disk space statistics for all available drives
        /// </summary>
        public async Task<IEnumerable<DriveStatistics>> GetDriveStatisticsAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!_enableSystemMonitoring)
                    {
                        _logger.LogDebug("System monitoring is disabled");
                        return Enumerable.Empty<DriveStatistics>();
                    }

                    var driveStats = new List<DriveStatistics>();
                    var drives = DriveInfo.GetDrives();

                    foreach (var drive in drives)
                    {
                        try
                        {
                            var stats = new DriveStatistics
                            {
                                DriveLetter = drive.Name,
                                DriveType = drive.DriveType.ToString(),
                                IsReady = drive.IsReady
                            };

                            if (drive.IsReady)
                            {
                                stats.TotalSizeBytes = drive.TotalSize;
                                stats.FreeSpaceBytes = drive.AvailableFreeSpace;
                                stats.UsedSpaceBytes = stats.TotalSizeBytes - stats.FreeSpaceBytes;
                                stats.PercentageUsed = stats.TotalSizeBytes > 0 
                                    ? Math.Round((double)stats.UsedSpaceBytes / stats.TotalSizeBytes * 100, 2) 
                                    : 0;
                                stats.FileSystem = drive.DriveFormat;
                                stats.VolumeLabel = string.IsNullOrEmpty(drive.VolumeLabel) 
                                    ? "Unlabeled" 
                                    : drive.VolumeLabel;

                                // Log warning if disk space usage is high
                                if (stats.PercentageUsed >= _diskSpaceWarningThreshold)
                                {
                                    _logger.LogWarning("Drive {Drive} is {Percentage:F1}% full ({UsedGB:F1} GB / {TotalGB:F1} GB)",
                                        stats.DriveLetter, stats.PercentageUsed, stats.UsedSpaceGB, stats.TotalSizeGB);
                                }
                            }
                            else
                            {
                                _logger.LogDebug("Drive {Drive} is not ready", drive.Name);
                            }

                            driveStats.Add(stats);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error getting statistics for drive {Drive}", drive.Name);
                        }
                    }

                    _logger.LogInformation("Retrieved statistics for {Count} drives", driveStats.Count);
                    return driveStats;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving drive statistics");
                    return Enumerable.Empty<DriveStatistics>();
                }
            });
        }

        /// <summary>
        /// Gets system memory statistics
        /// </summary>
        public object GetMemoryStatistics()
        {
            try
            {
                if (!_enableSystemMonitoring)
                {
                    return new { };
                }

                var process = System.Diagnostics.Process.GetCurrentProcess();
                var workingSetMemory = process.WorkingSet64;
                var totalManagedMemory = GC.GetTotalMemory(false);

                return new
                {
                    WorkingSetBytes = workingSetMemory,
                    WorkingSetMB = Math.Round(workingSetMemory / (1024.0 * 1024.0), 2),
                    ManagedMemoryBytes = totalManagedMemory,
                    ManagedMemoryMB = Math.Round(totalManagedMemory / (1024.0 * 1024.0), 2)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving memory statistics");
                return new { };
            }
        }

        /// <summary>
        /// Gets comprehensive system health statistics
        /// </summary>
        public async Task<object> GetSystemHealthStatisticsAsync()
        {
            try
            {
                var driveStats = await GetDriveStatisticsAsync();
                var memoryStats = GetMemoryStatistics();

                var systemHealth = new
                {
                    Drives = driveStats.Where(d => d.IsReady).ToList(),
                    Memory = memoryStats,
                    Timestamp = DateTime.UtcNow,
                    MachineName = Environment.MachineName,
                    OSVersion = Environment.OSVersion.ToString(),
                    ProcessorCount = Environment.ProcessorCount
                };

                _logger.LogInformation("Retrieved system health statistics: {DriveCount} drives, Machine: {MachineName}",
                    driveStats.Count(d => d.IsReady), Environment.MachineName);

                return systemHealth;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving system health statistics");
                throw;
            }
        }

        /// <summary>
        /// Creates MQTT sensor readings for drive statistics
        /// </summary>
        public IEnumerable<object> CreateDriveSensorReadings(IEnumerable<DriveStatistics> driveStats)
        {
            var readings = new List<object>();

            foreach (var drive in driveStats.Where(d => d.IsReady))
            {
                var driveName = drive.DriveLetter.Replace(":", "").Replace("\\", "");
                
                readings.AddRange(new[]
                {
                    new { SensorId = $"Drive_{driveName}_UsedPercentage", Value = drive.PercentageUsed, Unit = "percent" },
                    new { SensorId = $"Drive_{driveName}_FreeSpaceGB", Value = drive.FreeSpaceGB, Unit = "GB" },
                    new { SensorId = $"Drive_{driveName}_UsedSpaceGB", Value = drive.UsedSpaceGB, Unit = "GB" },
                    new { SensorId = $"Drive_{driveName}_TotalSpaceGB", Value = drive.TotalSizeGB, Unit = "GB" }
                });
            }

            return readings;
        }
    }
}