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

// Build connection string in code
var sqlBuilder = new SqlConnectionStringBuilder
{
    DataSource = sqlServer,
    InitialCatalog = sqlDatabase,
    UserID = sqlUser,
    Password = sqlPassword,
    TrustServerCertificate = bool.Parse(sqlTrustCert)
};

// Device info from config
var clientName = builder.Configuration["Device:ClientName"] ?? "PEPKOR";
var location = builder.Configuration["Device:Location"] ?? "JBH";
var station = builder.Configuration["Device:Station"] ?? "DIM";

// Register services
builder.Services.AddSingleton<DataService>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<MqttService>(provider => {
    var logger = provider.GetRequiredService<ILogger<MqttService>>();
    return new MqttService(logger, clientName, location, station);
});

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

try
{
    logger.LogInformation("Starting Systems One MQTT Service");
    logger.LogInformation("Topic structure: systems-one/{client}/{location}/{station}", clientName, location, station);
    
    // Test database connection
    var dataService = host.Services.GetRequiredService<DataService>();
    logger.LogInformation("Testing database connection to {Server}/{Database}...", sqlServer, sqlDatabase);
    var dbConnected = await dataService.TestConnectionAsync();
    
    if (dbConnected)
    {
        logger.LogInformation("Database connection test successful");
        
        // Test MQTT connection
        var mqttService = host.Services.GetRequiredService<MqttService>();
        logger.LogInformation("Testing MQTT connection to mqtt.bantryprop.com/ws...");
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