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

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        private const uint WM_CLOSE = 0x0010;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_MOUSEMOVE = 0x0200;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        public async void LaunchServer(ServerStatus server, bool autoTeam = false)
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

            var gameWindow = await AutoPressConnect(isVietnam);

            if (autoTeam && gameWindow != IntPtr.Zero)
            {
                _ = Task.Run(() => TryAutoJoinTeam(gameWindow));
            }
        }

        private async Task TryAutoJoinTeam(IntPtr gameWindow)
        {
            try
            {
                int width = 0, height = 0;
                RECT rect;
                for (int i = 0; i < 30; i++)
                {
                    if (GetClientRect(gameWindow, out rect))
                    {
                        width = rect.Right - rect.Left;
                        height = rect.Bottom - rect.Top;
                        if (width > 0 && height > 0) break;
                    }
                    await Task.Delay(2000);
                }
                if (width <= 0 || height <= 0)
                {
                    ConfigService.Log("AutoJoinTeam: could not read game window size");
                    return;
                }

                var cfg = new ConfigService().LoadSettings();
                if (!cfg.AutoTeamEnabled)
                {
                    ConfigService.Log("AutoJoinTeam: not enabled");
                    return;
                }

                var key = $"{width}x{height}";
                if (cfg.TeamCalibrations == null || !cfg.TeamCalibrations.TryGetValue(key, out var cal))
                {
                    ConfigService.Log($"AutoJoinTeam: no calibration for resolution {key}, skipping");
                    return;
                }

                var pref = cfg.AutoTeamPreference ?? "random";
                bool isAxis = pref == "axis" || (pref == "random" && Random.Shared.Next(2) == 0);
                float xPct = isAxis ? cal.AxisX : cal.AlliesX;
                float yPct = isAxis ? cal.AxisY : cal.AlliesY;

                ConfigService.Log($"AutoJoinTeam: joining {(isAxis ? "Axis" : "Allies")} team (calibration {key})");
                await AutoJoinTeam(gameWindow, xPct, yPct);
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
            }
        }

        private async Task<IntPtr> AutoPressConnect(bool isVietnam)
        {
            try
            {
                await Task.Delay(30000);

                for (int i = 0; i < 150; i++)
                {
                    var gameWindow = FindGameWindow(isVietnam);
                    if (gameWindow != IntPtr.Zero && IsWindow(gameWindow))
                    {
                        ConfigService.Log("AutoPressConnect: game window found, sending keys via PostMessage");
                        SetForegroundWindow(gameWindow);
                        await Task.Delay(3000);

                        await PostKeyboardInput(gameWindow, VK_SPACE, 5, 800);
                        await PostKeyboardInput(gameWindow, VK_RETURN, 5, 800);
                        await PostKeyboardInput(gameWindow, VK_ESCAPE, 5, 800);
                        ConfigService.Log("AutoPressConnect: keys sent, done");
                        return gameWindow;
                    }
                    await Task.Delay(1000);
                }

                ConfigService.Log("AutoPressConnect: timed out, game window never found");
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
            }
            return IntPtr.Zero;
        }

        private async Task PostKeyboardInput(IntPtr hWnd, int vkCode, int count, int delayMs)
        {
            for (int i = 0; i < count; i++)
            {
                PostMessage(hWnd, WM_KEYDOWN, (IntPtr)vkCode, IntPtr.Zero);
                PostMessage(hWnd, WM_KEYUP, (IntPtr)vkCode, IntPtr.Zero);
                await Task.Delay(delayMs);
            }
        }

        public async Task AutoJoinTeam(IntPtr gameWindow, float xPct, float yPct)
        {
            try
            {
                ConfigService.Log("AutoJoinTeam: waiting for team selection screen...");
                await Task.Delay(20000);

                for (int i = 0; i < 120; i++)
                {
                    if (!IsWindow(gameWindow))
                    {
                        ConfigService.Log("AutoJoinTeam: game window gone");
                        return;
                    }

                    RECT rect;
                    if (!GetClientRect(gameWindow, out rect))
                        continue;

                    int width = rect.Right - rect.Left;
                    int height = rect.Bottom - rect.Top;

                    if (width <= 0 || height <= 0)
                    {
                        await Task.Delay(2000);
                        continue;
                    }

                    int clickX = (int)(width * xPct);
                    int clickY = (int)(height * yPct);

                    ConfigService.Log($"AutoJoinTeam: clicking at {clickX},{clickY} (client area {width}x{height})");
                    SetForegroundWindow(gameWindow);
                    await Task.Delay(500);
                    PostMessage(gameWindow, WM_MOUSEMOVE, IntPtr.Zero, MakeLParam(clickX, clickY));
                    await Task.Delay(200);
                    PostMessage(gameWindow, WM_LBUTTONDOWN, IntPtr.Zero, MakeLParam(clickX, clickY));
                    await Task.Delay(200);
                    PostMessage(gameWindow, WM_LBUTTONUP, IntPtr.Zero, MakeLParam(clickX, clickY));
                    ConfigService.Log("AutoJoinTeam: click sent");
                    return;
                }

                ConfigService.Log("AutoJoinTeam: timed out");
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
            }
        }

        private IntPtr MakeLParam(int x, int y)
        {
            return (IntPtr)((y << 16) | (x & 0xFFFF));
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
            return FindGameWindowInternal(isVietnam);
        }

        public IntPtr FindGameWindowPublic(bool isVietnam)
        {
            return FindGameWindowInternal(isVietnam);
        }

        private static IntPtr FindGameWindowInternal(bool isVietnam)
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
                            if (proc.HasExited) continue;

                            ConfigService.Log($"CloseGame: sending WM_CLOSE to {name} (PID {proc.Id})");
                            var hwnd = proc.MainWindowHandle;
                            if (hwnd != IntPtr.Zero)
                            {
                                SendMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

                                if (proc.WaitForExit(10000))
                                {
                                    ConfigService.Log($"CloseGame: {name} exited gracefully");
                                    continue;
                                }
                            }

                            ConfigService.Log($"CloseGame: {name} did not exit, forcing kill");
                            proc.Kill();
                            proc.WaitForExit(3000);
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