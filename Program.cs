using Systems_One_MQTT_Service;

var builder = Host.CreateApplicationBuilder(args);

// MQTT Topic Structure Configuration
// Format: systems-one/client/location/station
var clientName = "PEPKOR"; // Name of the client
var location = "JBH";   // Location identifier
var station = "DIM";  // Specific station or device location

// Register services
builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<MqttService>(provider => {
    var logger = provider.GetRequiredService<ILogger<MqttService>>();
    // Pass the topic structure components to the MqttService
    return new MqttService(logger, clientName, location, station);
});

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

try
{
    logger.LogInformation("Starting Systems One MQTT Service");
    logger.LogInformation("Topic structure: systems-one/{client}/{location}/{station}", clientName, location, station);
    
    // Test MQTT connection
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