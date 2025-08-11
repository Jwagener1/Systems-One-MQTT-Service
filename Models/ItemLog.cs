namespace Systems_One_MQTT_Service.Models
{
    /// <summary>
    /// Represents an item log entry from the ItemLog table
    /// </summary>
    public class ItemLog
    {
        public int Id { get; set; }
        public DateTime ItemDateTime { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public decimal? Length { get; set; }
        public decimal? Width { get; set; }
        public decimal? Height { get; set; }
        public decimal? Weight { get; set; }
        public decimal? BoxVolume { get; set; }
        public decimal? LiquidVolume { get; set; }
        public bool NoDimension { get; set; }
        public bool NoWeight { get; set; }
        public bool Sent { get; set; }
        public bool ImageSent { get; set; }
        public bool Valid { get; set; }
        public bool Complete { get; set; }
        public string? ItemSpec { get; set; }
        public int? ItemCount { get; set; }
        public int? LegacyId { get; set; }
    }
}