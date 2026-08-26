using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Threading;
using L9HLL.Launcher.Dialogs;
using L9HLL.Launcher.Services;

namespace L9HLL.Launcher.Services
{
    public class UpdateService : IDisposable
    {
        public event Action<string, string>? UpdateAvailable;

        private readonly HttpClient _http;
        private readonly DispatcherTimer _timer;
        private readonly ConfigService _config;
        private readonly Action<string> _onStatus;
        private bool _isChecking;

        private bool CheckUpdates => _config.LoadSettings().CheckUpdates;

        public UpdateService(ConfigService config, Action<string> onStatus)
        {
            _http = new HttpClient();
            _http.Timeout = TimeSpan.FromSeconds(15);
            _config = config;
            _onStatus = onStatus;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromHours(1) };
            _timer.Tick += async (s, e) => { _timer.Stop(); await CheckForUpdate(); _timer.Start(); };
            _timer.Start();

            _ = Task.Run(CheckForUpdate);
        }

        public async Task ForceCheckAsync()
        {
            if (_isChecking) return;
            await CheckForUpdate();
        }

        private async Task CheckForUpdate()
        {
            if (!CheckUpdates) return;
            if (_isChecking) return;
            _isChecking = true;

            try
            {
                _http.DefaultRequestHeaders.Add("User-Agent", "L9HLL-Launcher");
                var response = await _http.GetStringAsync(
                    "https://api.github.com/repos/gary-ovh/l9-hll-launcher/releases/latest");
                var release = JsonSerializer.Deserialize<GitHubRelease>(response);

                if (release == null || release.Assets == null || release.Assets.Count == 0) return;

                var latest = release.TagName.TrimStart('v');
                var current = ConfigService.CurrentVersion;

                if (string.IsNullOrWhiteSpace(latest) || string.IsNullOrWhiteSpace(current)) return;

                var currentParsed = System.Version.TryParse(current, out var cv) ? cv : null;
                var latestParsed = System.Version.TryParse(latest, out var lv) ? lv : null;

                if (currentParsed != null && latestParsed != null && latestParsed > currentParsed)
                {
                    var asset = release.Assets.Find(a => a.Name.EndsWith(".zip"));
                    if (asset != null && !string.IsNullOrEmpty(asset.BrowserDownloadUrl))
                    {
                        UpdateAvailable?.Invoke(latest, asset.BrowserDownloadUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
            }
            finally
            {
                _isChecking = false;
            }
        }

        public static async Task DownloadAndUpdate(string zipUrl, UpdateProgressDialog progressDialog)
        {
            try
            {
                progressDialog.SetStatus("Downloading update...");
                progressDialog.SetProgress(1);

                var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(120);
                http.DefaultRequestHeaders.Add("User-Agent", "L9HLL-Launcher");

                var tempZip = Path.Combine(Path.GetTempPath(), "L9HLL_Launcher_Update.zip");
                var tempExe = Path.Combine(Path.GetTempPath(), "L9HLL.Launcher_new.exe");

                var response = await http.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead);
                var stream = await response.Content.ReadAsStreamAsync();

                using (stream)
                using (var fileStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[65536];
                    var downloaded = 0L;
                    var totalMs = 0L;

                    while (true)
                    {
                        var startMs = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                        var read = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (read == 0) break;

                        fileStream.Write(buffer, 0, read);
                        downloaded += read;
                        totalMs += DateTimeOffset.Now.ToUnixTimeMilliseconds() - startMs;

                        if (progressDialog.Cancelled)
                        {
                            File.Delete(tempZip);
                            return;
                        }

                        // Estimate progress from download speed
                        if (totalMs > 1000)
                        {
                            var speed = downloaded / (totalMs / 1000.0); // bytes per second
                            // Assume remaining based on initial speed (cap at 80% until complete)
                            var estimatedTotal = speed * 60 > downloaded ? speed * 60 : downloaded * 2;
                            var pct = (int)(Math.Min(downloaded, estimatedTotal) * 80.0 / Math.Max(estimatedTotal, 1));
                            progressDialog.SetProgress(Math.Max(pct, 2));
                        }
                    }
                }

                if (progressDialog.Cancelled)
                {
                    File.Delete(tempZip);
                    return;
                }

                progressDialog.SetStatus("Extracting update...");
                progressDialog.SetProgress(55);

                using (var archive = ZipFile.OpenRead(tempZip))
                {
                    var exeEntry = archive.Entries.FirstOrDefault(e =>
                        e.FullName.EndsWith("L9HLL.Launcher.exe"));

                    if (exeEntry == null)
                    {
                        File.Delete(tempZip);
                        progressDialog.SetStatus("Error: update file not found in archive");
                        progressDialog.Cancelled = true;
                        return;
                    }

                    using (var exeStream = new FileStream(tempExe, FileMode.Create))
                    {
                        exeEntry.Open().CopyTo(exeStream);
                    }
                }

                if (progressDialog.Cancelled)
                {
                    File.Delete(tempZip);
                    File.Delete(tempExe);
                    return;
                }

                progressDialog.SetStatus("Preparing restart...");
                progressDialog.SetProgress(75);

                var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                var oldExePath = Path.Combine(exeDir, "L9HLL.Launcher.exe");
                var batLines = new[]
                {
                    "@echo off",
                    "setlocal",
                    $"set OLD_EXE={exeDir}\\L9HLL.Launcher.exe",
                    $"set NEW_EXE={tempExe}",
                    $"set ZIP_PATH={tempZip}",
                    "",
                    ":wait_for_exit",
                    "tasklist /FI \"IMAGENAME eq L9HLL.Launcher.exe\" 2>nul | find /i /n \"L9HLL.Launcher.exe\">nul",
                    "if not errorlevel 1 goto wait_for_exit",
                    "",
                    "timeout /t 2 /nobreak >nul",
                    "taskkill /F /IM L9HLL.Launcher.exe >nul 2>&1",
                    "",
                    "copy /Y \"%NEW_EXE%\" \"%OLD_EXE%\"",
                    "if not errorlevel 1 (",
                    "    start \"\" \"%OLD_EXE%\"",
                    ")",
                    "del \"%NEW_EXE%\"",
                    "del \"%ZIP_PATH%\"",
                    "del \"%OLD_EXE:exe=bat%\"",
                    "exit"
                };
                var finalBatPath = oldExePath.Replace(".exe", ".bat");
                File.WriteAllLines(finalBatPath, batLines);

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{finalBatPath}\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi);

                progressDialog.SetStatus("Relaunching...");
                progressDialog.SetProgress(100);
                await Task.Delay(500);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    System.Windows.Application.Current.Shutdown());
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
                progressDialog?.SetStatus($"Error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _timer.Stop();
            _http.Dispose();
        }

        private class GitHubRelease
        {
            [JsonPropertyName("tag_name")]
            public string TagName { get; set; } = "";

            [JsonPropertyName("assets")]
            public List<Asset> Assets { get; set; } = new();
        }

        private class Asset
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";

            [JsonPropertyName("browser_download_url")]
            public string BrowserDownloadUrl { get; set; } = "";
        }
    }
}