using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Systems_One_MQTT_Service;

var builder = Host.CreateApplicationBuilder(args);

// Read SQL connection fields from configuration
var sqlServer = builder.Configuration["ConnectionFields:Server"] ?? "localhost";
var sqlDatabase = builder.Configuration["ConnectionFields:Database"] ?? "MyDb";
var sqlUser = builder.Configuration["ConnectionFields:UserId"] ?? "sa";
var sqlPassword = builder.Configuration["ConnectionFields:Password"] ?? "your_password";
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

// Add EF Core SQL Server DbContext registration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(sqlBuilder.ConnectionString));

// Device info from config
var clientName = builder.Configuration["Device:ClientName"] ?? "PEPKOR";
var location = builder.Configuration["Device:Location"] ?? "JBH";
var station = builder.Configuration["Device:Station"] ?? "DIM";

// Register services
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
    var mqttService = host.Services.GetRequiredService<MqttService>();
    logger.LogInformation("Testing MQTT connection to mqtt.bantryprop.com/ws...");
    await mqttService.ConnectAndSendSampleMessageAsync();
    logger.LogInformation("MQTT test completed successfully");
}
catch (Exception ex)
{
    logger.LogError(ex, "Error during startup");
}

// Run the host (this will keep the Worker running)
await host.RunAsync();