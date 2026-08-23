using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;
using L9HLL.Launcher.Models;

namespace L9HLL.Launcher.Services
{
    public class LaunchService
    {
        private const int HLL_AppId = 686810;
        private const int VK_SPACE = 0x20;
        private const int VK_RETURN = 0x0D;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public async void LaunchServer(ServerStatus server)
        {
            var steamPath = FindSteamPath();

            if (!string.IsNullOrEmpty(steamPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = steamPath,
                    Arguments = $"-applaunch {HLL_AppId} \"+connect {server.Ip}:{server.Port}\"",
                    UseShellExecute = true
                });
            }
            else
            {
                var gamePath = FindGamePath();
                if (!string.IsNullOrEmpty(gamePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = gamePath,
                        Arguments = $"+connect {server.Ip}:{server.Port}",
                        UseShellExecute = true
                    });
                }
            }

            await AutoPressConnect();
        }

        private async Task AutoPressConnect()
        {
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(1000);

                var gameWindow = FindGameWindow();
                if (gameWindow != IntPtr.Zero && IsWindow(gameWindow))
                {
                    SetForegroundWindow(gameWindow);
                    await Task.Delay(300);

                    SendKey(VK_RETURN);
                    await Task.Delay(200);
                    SendKey(VK_RETURN);
                    return;
                }
            }
        }

        private IntPtr FindGameWindow()
        {
            try
            {
                var processes = Process.GetProcessesByName("Hell Let Loose");
                foreach (var proc in processes)
                {
                    if (proc.MainWindowHandle != IntPtr.Zero && IsWindow(proc.MainWindowHandle))
                    {
                        return proc.MainWindowHandle;
                    }
                }
            }
            catch { }

            var hllWindow = FindWindow(null, "Hell Let Loose");
            if (hllWindow != IntPtr.Zero)
                return hllWindow;

            return IntPtr.Zero;
        }

        private void SendKey(int vkCode)
        {
            try
            {
                GetWindowThreadProcessId(GetFocus(), out uint threadId);
                uint currentThreadId = GetCurrentThreadId();

                if (threadId != currentThreadId)
                {
                    AttachThreadInput(threadId, currentThreadId, true);
                }

                keybd_event((byte)vkCode, 0, 0, IntPtr.Zero);
                keybd_event((byte)vkCode, 0, KEYEVENTF_KEYUP, IntPtr.Zero);

                if (threadId != currentThreadId)
                {
                    AttachThreadInput(threadId, currentThreadId, false);
                }
            }
            catch { }
        }

        private static string? FindGamePath()
        {
            var keys = new[]
            {
                @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var baseKey in keys)
            {
                var root = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(baseKey);
                if (root == null) continue;

                foreach (var subKey in root.GetSubKeyNames())
                {
                    try
                    {
                        using var sub = root.OpenSubKey(subKey);
                        var displayName = sub?.GetValue("DisplayName") as string;
                        if (displayName == null || !displayName.Contains("Hell Let Loose")) continue;

                        var installPath = sub.GetValue("InstallLocation") as string
                                        ?? sub.GetValue("InstallPath") as string;

                        if (!string.IsNullOrEmpty(installPath))
                        {
                            var trimmed = installPath.TrimEnd('\\');
                            var gameExe = Path.Combine(trimmed, "Hell Let Loose.exe");
                            if (File.Exists(gameExe))
                                return gameExe;
                        }
                    }
                    catch { }
                }
            }

            return null;
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