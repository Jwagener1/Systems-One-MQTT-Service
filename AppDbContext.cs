using Microsoft.EntityFrameworkCore;

namespace Systems_One_MQTT_Service
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        // DbSets will be added here later
    }
}       
