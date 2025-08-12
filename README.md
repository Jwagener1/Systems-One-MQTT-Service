# Systems One MQTT Service with System Monitoring

A comprehensive .NET 8 Worker Service that monitors ItemLog database statistics and publishes real-time data to MQTT brokers with integrated system health monitoring. This service provides enterprise-grade monitoring capabilities for industrial applications with configurable intervals, robust error handling, and extensive telemetry.

## ?? Features

### ?? Database Monitoring
- **Real-time ItemLog Analysis**: Continuous monitoring of database operations
- **Configurable Query Intervals**: Default 15-minute cycles with customizable timing
- **Comprehensive Statistics Tracking**:
  - Item processing success/failure rates
  - Weight and dimension validation metrics
  - Transmission status monitoring (Sent/NotSent)
  - Image processing statistics
  - Quality control metrics (Valid/OutOfSpec)

### ??? System Health Monitoring
- **Multi-Drive Storage Monitoring**:
  - Real-time disk space utilization tracking
  - Support for all drive types (Fixed, Removable, Network, CD-ROM)
  - Configurable disk space warning thresholds
  - File system and volume label detection
  - Automatic drive discovery and monitoring
- **System Information Collection**:
  - Machine identification and OS version reporting
  - Processor count and system architecture details
  - Real-time timestamp synchronization

### ?? Advanced MQTT Publishing
- **Flexible Connection Options**:
  - WebSocket (WSS/WS) and TCP transport support
  - Configurable TLS/SSL encryption
  - Custom broker authentication
- **Intelligent Topic Management**:
  - Hierarchical topic structure: `{BaseTopic}/{ClientName}/{Location}/{Station}/{Suffix}`
  - Separate data and status message channels with configurable suffixes
  - Configurable topic prefixes and suffixes
- **Enterprise-Grade Reliability**:
  - Last Will Testament for connection monitoring
  - Automatic reconnection with exponential backoff
  - Configurable Quality of Service levels
  - Message retention policies
  - Connection health monitoring

### ?? Configuration Management
- **Environment-Specific Settings**: Separate configurations for Development/Production
- **Device Identification**: Configurable serial numbers and device metadata
- **Culture-Invariant Parsing**: Robust configuration parsing across different locales
- **Hot-Reloadable Settings**: Runtime configuration updates support

## ?? Configuration

### Complete Configuration Structure

```json
{
  "ConnectionFields": {
    "Server": "192.168.1.16,1433",
    "Database": "Systems_One",
    "UserId": "SysOne",
    "Password": "SysOne012!",
    "TrustServerCertificate": "True",
    "Table": "ItemLog"
  },
  "Device": {
    "ClientName": "PEPKOR",
    "Location": "JBH",
    "Station": "DIM2",
    "SerialNumber": "00001"
  },
  "MQTT": {
    "Broker": {
      "Host": "mqtt.bantryprop.com",
      "Port": 443,
      "Path": "/ws",
      "UseWebSockets": true,
      "UseTLS": true,
      "ConnectionTimeout": 30000,
      "KeepAlive": 60
    },
    "Authentication": {
      "Username": "Admin",
      "Password": "Admin"
    },
    "Client": {
      "ClientIdPrefix": "SystemsOneMqttClient",
      "CleanSession": true,
      "ReconnectDelay": 5000,
      "MaxReconnectAttempts": 10
    },
    "Topics": {
      "BaseTopic": "systems-one",
      "StatusSuffix": "status",
      "StatisticsSuffix": "statistics",
      "StorageSuffix": "storage",
      "DataSuffix": "data"
    },
    "Publishing": {
      "QualityOfService": "AtLeastOnce",
      "RetainMessages": false,
      "RetainStatusMessages": true,
      "PublishInterval": 15
    },
    "LastWill": {
      "Enabled": true,
      "QualityOfService": "AtLeastOnce",
      "Retain": true,
      "Message": "Device has gone offline unexpectedly"
    }
  },
  "SystemMonitoring": {
    "Enabled": true,
    "DiskSpaceWarningThreshold": 80.0
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information",
      "Systems_One_MQTT_Service": "Information"
    }
  }
}
```

### ?? Configuration Reference

#### Database Connection Settings
| Setting | Description | Default |
|---------|-------------|---------|
| `Server` | SQL Server instance with port | `localhost,1433` |
| `Database` | Target database name | `Systems_One` |
| `UserId` | Database username | `SysOne` |
| `Password` | Database password | - |
| `TrustServerCertificate` | Trust server SSL certificate | `True` |
| `Table` | Target table for monitoring | `ItemLog` |

#### Device Identification
| Setting | Description | Example |
|---------|-------------|---------|
| `ClientName` | Organization/client identifier | `PEPKOR` |
| `Location` | Physical location code | `JBH` |
| `Station` | Station/machine identifier | `DIM2` |
| `SerialNumber` | Unique device serial number | `00001` |

#### MQTT Broker Configuration
| Setting | Description | Options |
|---------|-------------|---------|
| `Host` | MQTT broker hostname/IP | Any valid hostname |
| `Port` | Connection port | `443` (WSS), `1883` (MQTT), `8883` (MQTTS) |
| `Path` | WebSocket path | `/ws` for WebSocket connections |
| `UseWebSockets` | Enable WebSocket transport | `true`/`false` |
| `UseTLS` | Enable SSL/TLS encryption | `true`/`false` |
| `ConnectionTimeout` | Connection timeout (ms) | `30000` |
| `KeepAlive` | Keep-alive interval (seconds) | `60` |

#### Quality of Service Options
| Level | Description | Use Case |
|-------|-------------|----------|
| `AtMostOnce` | Fire and forget | Non-critical data |
| `AtLeastOnce` | Guaranteed delivery | Important metrics |
| `ExactlyOnce` | Exactly once delivery | Critical commands |

## ?? MQTT Payload Structure - Telegraf Optimized

### Current Payload Format (v3.0) - Telegraf-Friendly
The service now publishes data in separate, optimized streams for Telegraf ingestion:

#### 1. Status Messages
**Topic:** `systems-one/PEPKOR/JBH/DIM2/status`
```json
{
  "device_id": "00001",
  "ts": 1754916806909,
  "device_status": "online",
  "device_os_version": "Microsoft Windows NT 10.0.26100.0"
}
```

#### 2. Statistics Messages 
**Topic:** `systems-one/PEPKOR/JBH/DIM2/statistics`
```json
{
  "device_id": "00001",
  "ts": 1754919514677,
  "statistics": {
    "total_items": 264,
    "no_weight": 4,
    "good_reads": 249,
    "no_reads": 15,
    "no_dimensions": 1,
    "success": 249,
    "out_of_spec": 1,
    "more_than_one_item": 10,
    "not_sent": 0,
    "sent": 264
  }
}
```

#### 3. Storage Messages
**Topic:** `systems-one/PEPKOR/JBH/DIM2/storage`
```json
{
  "device_id": "00001",
  "ts": 1754919514677,
  "storage": {
    "C": {
      "free_gb": 191.92,
      "used_gb": 284.07,
      "total_gb": 475.91,
      "used_pct": 59.67
    },
    "E": {
      "free_gb": 14.7,
      "used_gb": 0.14,
      "total_gb": 14.84,
      "used_pct": 0.97
    }
  }
}
```

### Telegraf Integration Benefits

? **Snake_case naming** for easy field mapping  
? **Timestamp in milliseconds** (`ts`) for precise time alignment  
? **Numeric values** for metrics (no string conversion needed)  
? **Hierarchical structure** for easy json_v2 parsing  
? **Drive letters as keys** for automatic tagging  
? **Separate topics** for different metric types  

### Topic Structure
- **Status Channel**: `systems-one/PEPKOR/JBH/DIM2/status`
- **Statistics Channel**: `systems-one/PEPKOR/JBH/DIM2/statistics`
- **Storage Channel**: `systems-one/PEPKOR/JBH/DIM2/storage`

### Telegraf Configuration Example
```toml
[[inputs.mqtt_consumer]]
  servers = ["tcp://mqtt.bantryprop.com:1883"]
  topics = ["systems-one/+/+/+/status", "systems-one/+/+/+/statistics", "systems-one/+/+/+/storage"]
  data_format = "json_v2"
  
  [[inputs.mqtt_consumer.json_v2]]
    measurement_name = "device_status"
    timestamp_key = "ts"
    timestamp_format = "unix_ms"
    
    [[inputs.mqtt_consumer.json_v2.tag]]
      path = "device_id"
      
    [[inputs.mqtt_consumer.json_v2.field]]
      path = "device_status"
      type = "string"
      
    [[inputs.mqtt_consumer.json_v2.field]]
      path = "device_os_version"
      type = "string"
```

### Database Statistics Metrics
| Field | Description | Type |
|-------|-------------|------|
| `total_items` | Total items processed in interval | integer |
| `no_weight` | Items missing weight data | integer |
| `good_reads` | Items with successful barcode reads | integer |
| `no_reads` | Items with failed barcode reads (NOREAD) | integer |
| `no_dimensions` | Items missing dimension data | integer |
| `success` | Successfully validated items | integer |
| `out_of_spec` | Items outside specifications | integer |
| `more_than_one_item` | Items with count > 1 | integer |
| `not_sent` | Items not transmitted | integer |
| `sent` | Successfully transmitted items | integer |

### Storage Metrics (Per Drive)
| Field Pattern | Description | Type |
|---------------|-------------|------|
| `{drive}.free_gb` | Available free space in GB | float |
| `{drive}.used_gb` | Used space in GB | float |
| `{drive}.total_gb` | Total drive capacity in GB | float |
| `{drive}.used_pct` | Percentage of drive space used | float |

## ??? Architecture

### Core Components

```
???????????????????    ????????????????????    ???????????????????
?    Worker       ?    ?   DataService    ?    ?  SQL Database   ?
?  (BackgroundSvc)??????                  ??????   (ItemLog)     ?
???????????????????    ????????????????????    ???????????????????
         ?
         ?
???????????????????    ????????????????????    ???????????????????
?   MqttService   ??????SystemMonitoring  ??????  System APIs    ?
?                 ?    ?    Service       ?    ?  (DriveInfo)    ?
???????????????????    ????????????????????    ???????????????????
         ?
         ?
???????????????????
?  MQTT Broker    ?
? (External)      ?
???????????????????
```

### Service Classes
- **`Worker`**: Main background service orchestrating data collection and publishing
- **`DataService`**: Database operations and statistics aggregation using Dapper
- **`SystemMonitoringService`**: System health monitoring and drive statistics
- **`MqttService`**: MQTT client management with reconnection logic
- **`AppDbContext`**: Entity Framework context for future extensibility

### Data Models
- **`ItemLog`**: Database entity model for ItemLog table
- **`DriveStatistics`**: System drive information and metrics
- **`MqttConfiguration`**: Strongly-typed configuration binding

## ?? Deployment & Usage

### Prerequisites
- .NET 8 Runtime or SDK
- SQL Server access with read permissions on ItemLog table
- Network access to MQTT broker
- Windows/Linux compatible

### Installation Steps

1. **Clone and Build**
   ```bash
   git clone <repository-url>
   cd Systems-One-MQTT-Service
   dotnet build --configuration Release
   ```

2. **Configure Settings**
   ```bash
   # Update appsettings.json with your environment details
   cp appsettings.json appsettings.Production.json
   # Edit appsettings.Production.json with production settings
   ```

3. **Run Service**
   ```bash
   # Development
   dotnet run

   # Production
   dotnet run --environment Production

   # As Windows Service
   dotnet publish --configuration Release
   sc create "Systems One MQTT Service" binpath="path\to\Systems-One-MQTT-Service.exe"
   ```

### Topic Structure
- **Data Channel**: `systems-one/PEPKOR/JBH/DIM2/data`
- **Status Channel**: `systems-one/PEPKOR/JBH/DIM2/status`

### Environment Configurations
- **Development**: Uses `appsettings.Development.json` with debug logging
- **Production**: Uses `appsettings.json` with optimized logging

## ?? Monitoring & Logging

### Comprehensive Logging
The service provides detailed logging for all operations:

```
[10:00:00] Systems One MQTT Service with System Monitoring started
[10:00:01] Device Configuration - Client: PEPKOR, Location: JBH, Station: DIM2, Serial: 00001
[10:00:01] SystemMonitoringService initialized - Enabled: True, Warning Threshold: 80%
[10:00:01] Worker configured with 15 minute publish interval
[10:00:01] MQTT Service initialized with Device Serial Number: 00001
[10:00:02] MQTT Configuration: mqtt.bantryprop.com:443/ws
[10:00:02] Last Will Testament configured: {"DeviceId":"00001","Timestamp":1754908626380,"Status":"offline"}
[10:00:03] System monitoring initialized - found 2 ready drives
[10:00:03] Drive C:\ (Windows): 59.6% used, 192.1 GB free of 475.9 GB total
[10:00:04] Database connection test successful
[10:00:05] Connecting to MQTT broker at wss://mqtt.bantryprop.com:443/ws...
[10:00:06] Connected successfully to MQTT broker
[10:00:06] Published status message: online for device 00001
[10:00:07] MQTT test completed successfully
[10:00:08] Worker started - will send statistics every 15 minutes
```

### Health Monitoring
- **Database Connectivity**: Automatic connection testing and retry logic
- **MQTT Connection**: Health checks with automatic reconnection
- **Drive Space Warnings**: Configurable threshold alerts
- **Error Recovery**: Graceful error handling with retry mechanisms

## ??? Troubleshooting

### Common Issues

#### Connection Problems
- **Database**: Verify SQL Server accessibility and credentials
- **MQTT Broker**: Check network connectivity and firewall settings
- **Certificate Issues**: Validate TLS/SSL certificate configurations

#### Configuration Errors
- **JSON Syntax**: Validate JSON formatting in configuration files
- **Culture Issues**: Service uses culture-invariant parsing for international deployment
- **Missing Settings**: Service provides defaults with warning logs

#### Performance Optimization
- **Publish Intervals**: Adjust based on data volume requirements
- **QoS Levels**: Choose appropriate quality of service for your use case
- **Log Levels**: Use Information level for production, Debug for troubleshooting

### Debug Configuration
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Systems_One_MQTT_Service": "Debug"
    }
  }
}
```

## ?? Dependencies

### NuGet Packages
- **`Microsoft.Extensions.Hosting (9.0.7)`**: Background service framework
- **`Microsoft.Extensions.Hosting.WindowsServices (9.0.7)`**: Windows Service support
- **`MQTTnet (4.1.2)`**: MQTT client library
- **`Dapper (2.1.66)`**: Lightweight ORM for database operations
- **`Microsoft.EntityFrameworkCore.SqlServer (9.0.7)`**: Entity Framework support
- **`Microsoft.Data.SqlClient`**: SQL Server data provider

### System Requirements
- **.NET 8**: Runtime or SDK
- **Windows/Linux**: Cross-platform compatible
- **Memory**: Minimal footprint (~50MB working set)
- **Storage**: <100MB disk space

## ?? Performance Characteristics

- **Memory Usage**: ~45-125 MB working set depending on system monitoring scope
- **CPU Impact**: Minimal (<1% on modern systems)
- **Network Traffic**: Configurable based on publish intervals
- **Database Load**: Lightweight queries with configurable intervals
- **Disk I/O**: Read-only operations for system monitoring

## ?? Security Considerations

- **Credential Management**: Use secure credential storage in production
- **TLS Encryption**: Enable for all MQTT communications
- **Database Access**: Use read-only accounts where possible
- **Network Security**: Configure firewall rules for MQTT broker access

## ??? Roadmap

### Planned Features
- **Real-time Alerts**: Immediate notifications for critical events
- **Historical Data Export**: Long-term trend analysis capabilities
- **Web Dashboard**: Local web interface for monitoring
- **Plugin Architecture**: Extensible monitoring modules
- **Container Support**: Docker deployment options

---

**Version**: 2.0  
**Last Updated**: January 2025  
**Platform**: .NET 8  
**License**: [Add your license information]