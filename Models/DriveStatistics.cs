namespace Systems_One_MQTT_Service.Models
{
    /// <summary>
    /// Represents disk drive statistics for system monitoring
    /// </summary>
    public class DriveStatistics
    {
        public string DriveLetter { get; set; } = string.Empty;
        public string DriveType { get; set; } = string.Empty;
        public long TotalSizeBytes { get; set; }
        public long UsedSpaceBytes { get; set; }
        public long FreeSpaceBytes { get; set; }
        public double PercentageUsed { get; set; }
        public string FileSystem { get; set; } = string.Empty;
        public string VolumeLabel { get; set; } = string.Empty;
        public bool IsReady { get; set; }
        
        /// <summary>
        /// Gets the total size in GB for display purposes
        /// </summary>
        public double TotalSizeGB => Math.Round(TotalSizeBytes / (1024.0 * 1024.0 * 1024.0), 2);
        
        /// <summary>
        /// Gets the free space in GB for display purposes
        /// </summary>
        public double FreeSpaceGB => Math.Round(FreeSpaceBytes / (1024.0 * 1024.0 * 1024.0), 2);
        
        /// <summary>
        /// Gets the used space in GB for display purposes
        /// </summary>
        public double UsedSpaceGB => Math.Round(UsedSpaceBytes / (1024.0 * 1024.0 * 1024.0), 2);
    }
}