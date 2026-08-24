using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using L9HLL.Launcher.Models;

namespace L9HLL.Launcher.Services
{
    public class ConfigService
    {
        public static string CurrentVersion =>
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

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
            catch (Exception ex)
            {
                LogError(ex);
            }

            return new List<ServerInfo>
            {
                new ServerInfo { Name = "[L9] The Loyal Nine |#1|", Ip = "40.27.41.16", Port = 7777, Game = "hll" },
                new ServerInfo { Name = "[L9] The Loyal Nine |#2|", Ip = "40.27.41.9", Port = 7777, Game = "hll" },
                new ServerInfo { Name = "[L9] The Loyal Nine |#1| Vietnam", Ip = "69.162.103.77", Port = 7777, QueryPort = 7778, Game = "vietnam" }
            };
        }

        private class ServerConfig
        {
            [JsonPropertyName("servers")]
            public List<ServerInfo>? Servers { get; set; }
        }

        private string GetConfigDir()
        {
            var exePath = Environment.ProcessPath ?? "";
            var exeDir = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            var configDir = Path.Combine(exeDir, "Config");
            Directory.CreateDirectory(configDir);
            return configDir;
        }

        private string GetConfigPath() => Path.Combine(GetConfigDir(), "app.json");

        public AppSettings LoadSettings()
        {
            var path = GetConfigPath();
            try
            {
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    return settings ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
            return new AppSettings();
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                var path = GetConfigPath();
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        public static void LogError(Exception ex)
        {
            try
            {
                var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                var logPath = Path.Combine(exeDir, "debug.log");
                File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n");
            }
            catch { }
        }
    }

    public class AppSettings
    {
        public bool StartupOnBoot { get; set; }
        public bool StartMinimized { get; set; }
        public bool CheckUpdates { get; set; } = true;
        public bool AutoLaunchEnabled { get; set; }
        public string AutoLaunchTime { get; set; } = "22:00";
    }
}