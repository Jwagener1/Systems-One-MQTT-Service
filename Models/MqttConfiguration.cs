namespace Systems_One_MQTT_Service.Models
{
    /// <summary>
    /// Configuration model for MQTT settings
    /// </summary>
    public class MqttConfiguration
    {
        public BrokerSettings Broker { get; set; } = new();
        public AuthenticationSettings Authentication { get; set; } = new();
        public ClientSettings Client { get; set; } = new();
        public TopicSettings Topics { get; set; } = new();
        public PublishingSettings Publishing { get; set; } = new();
        public LastWillSettings LastWill { get; set; } = new();
    }

    public class BrokerSettings
    {
        public string Host { get; set; } = "mqtt.bantryprop.com";
        public int Port { get; set; } = 443;
        public string Path { get; set; } = "/ws";
        public bool UseWebSockets { get; set; } = true;
        public bool UseTLS { get; set; } = true;
        public int ConnectionTimeout { get; set; } = 30000;
        public int KeepAlive { get; set; } = 60;
    }

    public class AuthenticationSettings
    {
        public string Username { get; set; } = "Admin";
        public string Password { get; set; } = "Admin";
    }

    public class ClientSettings
    {
        public string ClientIdPrefix { get; set; } = "SystemsOneMqttClient";
        public bool CleanSession { get; set; } = true;
        public int ReconnectDelay { get; set; } = 5000;
        public int MaxReconnectAttempts { get; set; } = 10;
    }

    public class TopicSettings
    {
        public string BaseTopic { get; set; } = "systems-one";
        public string StatusSuffix { get; set; } = "status";
        public string DataSuffix { get; set; } = "data";
    }

    public class PublishingSettings
    {
        public string QualityOfService { get; set; } = "AtLeastOnce";
        public bool RetainMessages { get; set; } = false;
        public bool RetainStatusMessages { get; set; } = true;
        public int PublishInterval { get; set; } = 15;
    }

    public class LastWillSettings
    {
        public bool Enabled { get; set; } = true;
        public string QualityOfService { get; set; } = "AtLeastOnce";
        public bool Retain { get; set; } = true;
        public string Message { get; set; } = "Device has gone offline unexpectedly";
    }
}