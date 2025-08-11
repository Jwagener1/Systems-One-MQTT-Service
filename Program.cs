using Microsoft.Data.SqlClient;
using Systems_One_MQTT_Service;
using Systems_One_MQTT_Service.Services;

var builder = Host.CreateApplicationBuilder(args);

// Read SQL connection fields from configuration
var sqlServer = builder.Configuration["ConnectionFields:Server"] ?? "192.168.133.2,1433";
var sqlDatabase = builder.Configuration["ConnectionFields:Database"] ?? "Systems_One";
var sqlUser = builder.Configuration["ConnectionFields:UserId"] ?? "SysOne";
var sqlPassword = builder.Configuration["ConnectionFields:Password"] ?? "SysOne012!";
var sqlTrustCert = builder.Configuration["ConnectionFields:TrustServerCertificate"] ?? "True";

// Build connection string in code with safe boolean parsing
var trustServerCertificate = true; // Default to true
if (!bool.TryParse(sqlTrustCert, out trustServerCertificate))
{
    Console.WriteLine($"Warning: Invalid TrustServerCertificate value '{sqlTrustCert}', using default 'True'");
    trustServerCertificate = true;
}

var sqlBuilder = new SqlConnectionStringBuilder
{
    DataSource = sqlServer,
    InitialCatalog = sqlDatabase,
    UserID = sqlUser,
    Password = sqlPassword,
    TrustServerCertificate = trustServerCertificate
};

// Device info from config
var clientName = builder.Configuration["Device:ClientName"] ?? "PEPKOR";
var location = builder.Configuration["Device:Location"] ?? "JBH";
var station = builder.Configuration["Device:Station"] ?? "DIM";
var serialNumber = builder.Configuration["Device:SerialNumber"] ?? "UNKNOWN-DEVICE-001";

// Register services
builder.Services.AddSingleton<DataService>();
builder.Services.AddSingleton<SystemMonitoringService>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<MqttService>(provider => {
    var logger = provider.GetRequiredService<ILogger<MqttService>>();
    var configuration = provider.GetRequiredService<IConfiguration>();
    return new MqttService(logger, configuration, clientName, location, station);
});

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

try
{
    logger.LogInformation("Starting Systems One MQTT Service with System Monitoring");
    logger.LogInformation("Device Configuration - Client: {ClientName}, Location: {Location}, Station: {Station}, Serial: {SerialNumber}", 
        clientName, location, station, serialNumber);
    logger.LogInformation("Topic structure: systems-one/{client}/{location}/{station}", clientName, location, station);
    
    // Log MQTT configuration
    var mqttHost = builder.Configuration["MQTT:Broker:Host"] ?? "mqtt.bantryprop.com";
    var mqttPort = builder.Configuration["MQTT:Broker:Port"] ?? "443";
    var mqttPath = builder.Configuration["MQTT:Broker:Path"] ?? "/ws";
    logger.LogInformation("MQTT Configuration: {Host}:{Port}{Path}", mqttHost, mqttPort, mqttPath);
    
    // Test system monitoring
    var systemMonitoringService = host.Services.GetRequiredService<SystemMonitoringService>();
    var driveStats = await systemMonitoringService.GetDriveStatisticsAsync();
    var readyDrives = driveStats.Where(d => d.IsReady).ToList();
    logger.LogInformation("System monitoring initialized - found {Count} ready drives", readyDrives.Count);
    
    foreach (var drive in readyDrives)
    {
        logger.LogInformation("Drive {Letter} ({Label}): {Used:F1}% used, {FreeGB:F1} GB free of {TotalGB:F1} GB total",
            drive.DriveLetter, drive.VolumeLabel, drive.PercentageUsed, drive.FreeSpaceGB, drive.TotalSizeGB);
    }
    
    // Test database connection
    var dataService = host.Services.GetRequiredService<DataService>();
    logger.LogInformation("Testing database connection to {Server}/{Database}...", sqlServer, sqlDatabase);
    var dbConnected = await dataService.TestConnectionAsync();
    
    if (dbConnected)
    {
        logger.LogInformation("Database connection test successful");
        
        // Test MQTT connection
        var mqttService = host.Services.GetRequiredService<MqttService>();
        logger.LogInformation("Testing MQTT connection...");
        await mqttService.ConnectAndSendSampleMessageAsync();
        logger.LogInformation("MQTT test completed successfully");
    }
    else
    {
        logger.LogWarning("Database connection failed - service will continue but data operations may fail");
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Error during startup");
}

await host.RunAsync();