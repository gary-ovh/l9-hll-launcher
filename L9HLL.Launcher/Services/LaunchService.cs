using System.Diagnostics;
using System.IO;
using L9HLL.Launcher.Models;

namespace L9HLL.Launcher.Services
{
    public class LaunchService
    {
        private const int HLL_AppId = 437620;

        public void LaunchServer(ServerStatus server)
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
}