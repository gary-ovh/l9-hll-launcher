using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using L9HLL.Launcher.Models;

namespace L9HLL.Launcher.Services
{
    public class ConfigService
    {
        public List<ServerInfo> LoadServers()
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "servers.json");

            try
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<ServerConfig>(json);
                return config?.Servers ?? new List<ServerInfo>();
            }
            catch
            {
                return new List<ServerInfo>();
            }
        }

        private class ServerConfig
        {
            [JsonPropertyName("servers")]
            public List<ServerInfo>? Servers { get; set; }
        }
    }
}