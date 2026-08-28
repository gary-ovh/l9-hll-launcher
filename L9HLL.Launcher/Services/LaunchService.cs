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
        private const int Vietnam_AppId = 3079210;

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

        public async void LaunchServer(ServerStatus server)
        {
            var steamPath = FindSteamPath();
            bool isVietnam = server.Game == "vietnam";

            try
            {
                if (!string.IsNullOrEmpty(steamPath))
                {
                    int appId = isVietnam ? Vietnam_AppId : HLL_AppId;
                    string cmd = isVietnam ? "open" : "+connect";

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = steamPath,
                        Arguments = $"-applaunch {appId} \"{cmd} {server.Ip}:{server.Port}\"",
                        UseShellExecute = true
                    });
                }
                else
                {
                    var gamePath = FindGamePath(isVietnam);
                    if (!string.IsNullOrEmpty(gamePath))
                    {
                        string cmd = isVietnam ? "open" : "+connect";
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = gamePath,
                            Arguments = $"{cmd} {server.Ip}:{server.Port}",
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
            }

            await AutoPressConnect(isVietnam);
        }

        private async Task AutoPressConnect(bool isVietnam)
        {
            try
            {
                await Task.Delay(30000);

                for (int i = 0; i < 150; i++)
                {
                    var gameWindow = FindGameWindow(isVietnam);
                    if (gameWindow != IntPtr.Zero && IsWindow(gameWindow))
                    {
                        ConfigService.Log($"AutoPressConnect: game window found, sending keys");
                        SetForegroundWindow(gameWindow);
                        await Task.Delay(3000);

                        for (int retry = 0; retry < 3; retry++)
                        {
                            ConfigService.Log($"AutoPressConnect: key send attempt {retry + 1}/3");
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
                            await Task.Delay(5000);

                            var stillSameWindow = FindGameWindow(isVietnam);
                            if (stillSameWindow != IntPtr.Zero && IsWindow(stillSameWindow))
                            {
                                SetForegroundWindow(stillSameWindow);
                                await Task.Delay(2000);
                            }
                            else
                            {
                                break;
                            }
                        }
                        return;
                    }
                    await Task.Delay(1000);
                }

                ConfigService.Log("AutoPressConnect: timed out, game window never found");
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
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

        private IntPtr FindGameWindow(bool isVietnam)
        {
            try
            {
                string[] processNames = isVietnam
                    ? new[] { "HLLVietnam-Win64-Shipping", "HLLVietnam" }
                    : new[] { "HLL-Win64-Shipping", "HLL" };

                foreach (var procName in processNames)
                {
                    var processes = Process.GetProcessesByName(procName);
                    foreach (var proc in processes)
                    {
                        if (proc.MainWindowHandle != IntPtr.Zero && IsWindow(proc.MainWindowHandle))
                            return proc.MainWindowHandle;
                    }
                }
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
            }

            var windowName = isVietnam ? "Hell Let Loose - Vietnam" : "Hell Let Loose";
            var hllWindow = FindWindow(null, windowName);
            if (hllWindow != IntPtr.Zero)
                return hllWindow;

            return IntPtr.Zero;
        }

        private static string? FindGamePath(bool isVietnam)
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

                try
                {
                    foreach (var subKey in root.GetSubKeyNames())
                    {
                        try
                        {
                            using var sub = root.OpenSubKey(subKey);
                            var displayName = sub?.GetValue("DisplayName") as string;
                            if (displayName == null) continue;

                            if (isVietnam && !displayName.Contains("Vietnam")) continue;
                            if (!isVietnam && !displayName.Contains("Hell Let Loose")) continue;

                            var installPath = sub.GetValue("InstallLocation") as string
                                            ?? sub.GetValue("InstallPath") as string;

                            if (!string.IsNullOrEmpty(installPath))
                            {
                                var trimmed = installPath.TrimEnd('\\');
                                var gameExe = Path.Combine(trimmed, isVietnam ? "HLLVietnam.exe" : "Hell Let Loose.exe");
                                if (File.Exists(gameExe))
                                    return gameExe;
                            }
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    ConfigService.LogError(ex);
                }
            }

            return null;
        }

        public void CloseGame()
        {
            try
            {
                string[] processNames =
                {
                    "HLL-Win64-Shipping", "HLL",
                    "HLLVietnam-Win64-Shipping", "HLLVietnam"
                };

                foreach (var name in processNames)
                {
                    try
                    {
                        var processes = Process.GetProcessesByName(name);
                        foreach (var proc in processes)
                        {
                            if (!proc.HasExited)
                            {
                                proc.Kill();
                                proc.WaitForExit(3000);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ConfigService.LogError(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
            }
        }

        private static string? FindSteamPath()
        {
            try
            {
                var installPath = (string?)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null)
                               ?? (string?)Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "InstallPath", null);

                if (!string.IsNullOrEmpty(installPath))
                {
                    var steamExe = Path.Combine(installPath, "Steam.exe");
                    if (File.Exists(steamExe))
                        return steamExe;
                }
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
            }

            return null;
        }
    }
}