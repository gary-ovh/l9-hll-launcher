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

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int Type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public int mouseData;
            public int dwFlags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public short wVk;
            public short wScan;
            public int dwFlags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public int uMsg;
            public short wParamL;
            public short wParamH;
        }

        private const int INPUT_KEYBOARD = 1;
        private const int KEYEVENTF_KEYUP = 0x0002;
        private const int VK_SPACE = 0x20;
        private const int VK_RETURN = 0x0D;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_F1 = 0x70;

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, ref INPUT pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

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
            // Wait at least 15 seconds for game to launch through Steam
            for (int i = 0; i < 15; i++)
            {
                await Task.Delay(1000);
            }

            // Now poll for the game window and send keys
            for (int i = 0; i < 90; i++)
            {
                var gameWindow = FindGameWindow();
                if (gameWindow != IntPtr.Zero && IsWindow(gameWindow))
                {
                    SetForegroundWindow(gameWindow);
                    await Task.Delay(1000);

                    // Send multiple keys with longer pauses
                    for (int j = 0; j < 5; j++)
                    {
                        SendKeyboardInput(VK_SPACE);
                        await Task.Delay(800);
                    }
                    for (int j = 0; j < 5; j++)
                    {
                        SendKeyboardInput(VK_RETURN);
                        await Task.Delay(800);
                    }
                    for (int j = 0; j < 5; j++)
                    {
                        SendKeyboardInput(VK_ESCAPE);
                        await Task.Delay(800);
                    }
                    return;
                }
                await Task.Delay(1000);
            }
        }

        private void SendKeyboardInput(int vkCode)
        {
            INPUT inputDown = new INPUT();
            inputDown.Type = INPUT_KEYBOARD;
            inputDown.U.ki.wVk = (short)vkCode;
            inputDown.U.ki.dwFlags = 0;

            INPUT inputUp = new INPUT();
            inputUp.Type = INPUT_KEYBOARD;
            inputUp.U.ki.wVk = (short)vkCode;
            inputUp.U.ki.dwFlags = KEYEVENTF_KEYUP;

            SendInput(1, ref inputDown, Marshal.SizeOf(typeof(INPUT)));
            SendInput(1, ref inputUp, Marshal.SizeOf(typeof(INPUT)));
        }

        private IntPtr FindGameWindow()
        {
            try
            {
                var processes = Process.GetProcessesByName("HLL");
                foreach (var proc in processes)
                {
                    if (proc.MainWindowHandle != IntPtr.Zero && IsWindow(proc.MainWindowHandle))
                    {
                        return proc.MainWindowHandle;
                    }
                }
            }
            catch { }

            try
            {
                var launchProcesses = Process.GetProcessesByName("Launch_HLL");
                foreach (var proc in launchProcesses)
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