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
            var exePath = Environment.ProcessPath ?? "";
            var exeDir = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            var configPath = Path.Combine(exeDir, "Config", "servers.json");

            try
            {
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<ServerConfig>(json);
                    return config?.Servers ?? new List<ServerInfo>();
                }
            }
            catch
            {
            }

            return new List<ServerInfo>
            {
                new ServerInfo { Name = "[L9] The Loyal Nine |#1|", Ip = "40.27.41.16", Port = 7777 },
                new ServerInfo { Name = "[L9] The Loyal Nine |#2|", Ip = "40.27.41.9", Port = 7777 }
            };
        }

        private class ServerConfig
        {
            [JsonPropertyName("servers")]
            public List<ServerInfo>? Servers { get; set; }
        }
    }
}