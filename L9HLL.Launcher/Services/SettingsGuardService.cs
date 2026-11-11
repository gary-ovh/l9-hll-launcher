using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace L9HLL.Launcher.Services
{
    public class SettingsGuardService
    {
        private const string RestoreFileName = "settings-restore.json";
        private const string BackupsDirName = "backups";

        private string BaseDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "L9HLLLauncher");

        private string RestoreFilePath => Path.Combine(BaseDir, RestoreFileName);

        private sealed class RestoreRecord
        {
            public string Game { get; set; } = "";
            public string Path { get; set; } = "";
            public string OriginalBase64 { get; set; } = "";
            public string Sha256 { get; set; } = "";
            public string Section { get; set; } = "";
            public string BackupDir { get; set; } = "";
            public string CreatedAt { get; set; } = "";
        }

        public bool IsSeededProfileActive => File.Exists(RestoreFilePath);

        public string? ActiveGame
        {
            get
            {
                try
                {
                    if (!File.Exists(RestoreFilePath)) return null;
                    var rec = JsonSerializer.Deserialize<RestoreRecord>(File.ReadAllText(RestoreFilePath));
                    return rec?.Game;
                }
                catch { return null; }
            }
        }

        public void BackupAndApply(string game)
        {
            var (settingsPath, section) = GetSettingsInfo(game);
            if (!File.Exists(settingsPath))
            {
                ConfigService.Log($"SettingsGuard: settings file not found at {settingsPath}, skipping profile");
                return;
            }

            if (IsSeededProfileActive)
            {
                ConfigService.Log($"SettingsGuard: a restore record already exists, overwriting for {game}");
            }

            var raw = File.ReadAllBytes(settingsPath);
            var sha256 = Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant();

            Directory.CreateDirectory(BaseDir);
            var backupDir = Path.Combine(BaseDir, BackupsDirName,
                $"settings-{DateTime.Now:yyyyMMdd-HHmmss}-{game}");
            Directory.CreateDirectory(backupDir);
            File.WriteAllBytes(Path.Combine(backupDir, "GameUserSettings.ini"), raw);

            var record = new RestoreRecord
            {
                Game = game,
                Path = settingsPath,
                OriginalBase64 = Convert.ToBase64String(raw),
                Sha256 = sha256,
                Section = section,
                BackupDir = backupDir,
                CreatedAt = DateTime.Now.ToString("o")
            };

            var tmpPath = RestoreFilePath + ".tmp";
            File.WriteAllText(tmpPath, JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmpPath, RestoreFilePath, overwrite: true);
            ConfigService.Log($"SettingsGuard: backed up {game} settings ({raw.Length} bytes, sha256={sha256[..16]}...) to {backupDir}");

            try
            {
                var modified = ApplySeededProfile(raw, section);
                File.WriteAllBytes(settingsPath, modified);
                ConfigService.Log($"SettingsGuard: applied seeded profile (1920x1080 windowed 20fps) to {game}");
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
                ConfigService.Log("SettingsGuard: failed to apply profile, restoring immediately");
                RestoreInternal(settingsPath, raw);
            }
        }

        public string? Restore()
        {
            if (!File.Exists(RestoreFilePath)) return null;

            RestoreRecord record;
            try
            {
                record = JsonSerializer.Deserialize<RestoreRecord>(File.ReadAllText(RestoreFilePath))!;
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
                return "Could not read restore record. Backup kept at " + RestoreFilePath;
            }

            if (IsGameRunning(record.Game))
            {
                ConfigService.Log("SettingsGuard: game still running, cannot restore yet");
                return "Game is still running. Close the game first, then restore.";
            }

            var original = Convert.FromBase64String(record.OriginalBase64);
            var actualSha = Convert.ToHexString(SHA256.HashData(original)).ToLowerInvariant();
            if (actualSha != record.Sha256)
            {
                ConfigService.Log($"SettingsGuard: backup integrity check FAILED ({actualSha[..16]} != {record.Sha256[..16]}), keeping record");
                return "Backup integrity check failed. Record kept for manual recovery.";
            }

            try
            {
                RestoreInternal(record.Path, original);
                ConfigService.Log($"SettingsGuard: restored {record.Game} settings ({original.Length} bytes)");
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
                return $"Failed to write settings back: {ex.Message}. Backup kept.";
            }

            try { File.Delete(RestoreFilePath); } catch { }
            ConfigService.Log("SettingsGuard: restore record removed");
            return null;
        }

        public string? TryRestoreIfIdle()
        {
            if (!IsSeededProfileActive) return null;
            var game = ActiveGame;
            if (game == null) return null;
            if (IsGameRunning(game)) return null;
            return Restore();
        }

        private void RestoreInternal(string path, byte[] original)
        {
            File.WriteAllBytes(path, original);
            var actual = File.ReadAllBytes(path);
            if (!actual.SequenceEqual(original))
                throw new IOException("Settings file verification failed after restore");
        }

        private static (string path, string section) GetSettingsInfo(string game)
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return game == "vietnam"
                ? (Path.Combine(local, "HLLVietnam", "Saved", "Config", "Windows", "GameUserSettings.ini"),
                   "/Script/HLLVietnam.ShooterGameUserSettings")
                : (Path.Combine(local, "HLL", "Saved", "Config", "WindowsNoEditor", "GameUserSettings.ini"),
                   "/Script/HLL.ShooterGameUserSettings");
        }

        private static bool IsGameRunning(string game)
        {
            string[] names = game == "vietnam"
                ? new[] { "HLLVietnam-Win64-Shipping", "HLLVietnam" }
                : new[] { "HLL-Win64-Shipping", "HLL" };

            foreach (var name in names)
            {
                try
                {
                    if (Process.GetProcessesByName(name).Length > 0) return true;
                }
                catch { }
            }
            return false;
        }

        private static byte[] ApplySeededProfile(byte[] raw, string section)
        {
            string text;
            Encoding encoding;
            if (raw.Length >= 2 && raw[0] == 0xFF && raw[1] == 0xFE)
            {
                encoding = Encoding.Unicode;
                text = encoding.GetString(raw, 2, raw.Length - 2);
            }
            else if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF)
            {
                encoding = new UTF8Encoding(true);
                text = new UTF8Encoding(false).GetString(raw, 3, raw.Length - 3);
            }
            else
            {
                encoding = new UTF8Encoding(false);
                text = encoding.GetString(raw);
            }

            var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
            string ending = text.Contains("\r\n") ? "\r\n" : "\n";

            var sectionMarker = $"[{section}]";
            int sectionStart = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimEnd('\r');
                if (trimmed == sectionMarker)
                {
                    sectionStart = i;
                    break;
                }
            }

            if (sectionStart < 0)
            {
                throw new IOException($"Section [{section}] not found in settings file");
            }

            int sectionEnd = lines.Length;
            for (int i = sectionStart + 1; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith('['))
                {
                    sectionEnd = i;
                    break;
                }
            }

            var values = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ResolutionSizeX"] = "1920",
                ["ResolutionSizeY"] = "1080",
                ["LastUserConfirmedResolutionSizeX"] = "1920",
                ["LastUserConfirmedResolutionSizeY"] = "1080",
                ["DesiredScreenWidth"] = "1920",
                ["DesiredScreenHeight"] = "1080",
                ["LastUserConfirmedDesiredScreenWidth"] = "1920",
                ["LastUserConfirmedDesiredScreenHeight"] = "1080",
                ["FullscreenMode"] = "2",
                ["LastConfirmedFullscreenMode"] = "2",
                ["PreferredFullscreenMode"] = "2",
                ["FrameRateLimit"] = "20.000000"
            };

            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = sectionStart + 1; i < sectionEnd; i++)
            {
                var line = lines[i];
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var key = line[..eq].Trim();
                if (values.TryGetValue(key, out var val))
                {
                    lines[i] = key + "=" + val;
                    seen.Add(key);
                }
            }

            var toAppend = values.Where(kv => !seen.Contains(kv.Key))
                .Select(kv => kv.Key + "=" + kv.Value)
                .ToArray();

            if (toAppend.Length > 0)
            {
                var newLines = new string[lines.Length + toAppend.Length];
                Array.Copy(lines, 0, newLines, 0, sectionEnd);
                for (int i = 0; i < toAppend.Length; i++)
                    newLines[sectionEnd + i] = toAppend[i];
                Array.Copy(lines, sectionEnd, newLines, sectionEnd + toAppend.Length, lines.Length - sectionEnd);
                lines = newLines;
            }

            var result = string.Join(ending, lines);

            var body = encoding.GetBytes(result);
            var preamble = encoding.GetPreamble();
            var bytes = preamble.Length > 0 ? preamble.Concat(body).ToArray() : body;

            return bytes;
        }
    }
}
