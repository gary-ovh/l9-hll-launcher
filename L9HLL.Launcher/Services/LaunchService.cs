using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using L9HLL.Launcher.Models;

namespace L9HLL.Launcher.Services
{
    public class LaunchService
    {
        private const int HLL_AppId = 437620;

        public void LaunchServer(ServerStatus server)
        {
            var steamPath = FindSteamPath();

            if (!string.IsNullOrEmpty(steamPath))
            {
                var connectArg = $"-connect {server.Ip}:{server.Port} -skipintro";
                var steamCmd = $"-applaunch {HLL_AppId} {connectArg}";

                Process.Start(new ProcessStartInfo
                {
                    FileName = steamPath,
                    Arguments = steamCmd,
                    UseShellExecute = true
                });
            }
            else
            {
                var connectArg = $"-connect\\{server.Ip}:{server.Port}/-skipintro";
                var steamUri = $"steam://run/{HLL_AppId}/{connectArg}";

                Process.Start(new ProcessStartInfo
                {
                    FileName = steamUri,
                    UseShellExecute = true
                });
            }
        }

        private static string? FindSteamPath()
        {
            var installPath = (string?)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null)
                           ?? (string?)Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "InstallPath", null);

            if (!string.IsNullOrEmpty(installPath))
            {
                var steamExe = Path.Combine(installPath, "Steam.exe");
                if (File.Exists(steamExe))
                    return steamExe;
            }

            return null;
        }
    }
}