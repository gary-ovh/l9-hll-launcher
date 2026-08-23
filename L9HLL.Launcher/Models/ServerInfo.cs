using System.Text.Json.Serialization;

namespace L9HLL.Launcher.Models
{
    public class ServerInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("ip")]
        public string Ip { get; set; } = string.Empty;
        [JsonPropertyName("port")]
        public int Port { get; set; }
        [JsonPropertyName("game")]
        public string Game { get; set; } = "hll";
    }
}