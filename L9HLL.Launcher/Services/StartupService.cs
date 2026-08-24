using System;
using Microsoft.Win32;

namespace L9HLL.Launcher.Services
{
    public static class StartupService
    {
        private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RegistryName = "L9HLL_Launcher";

        public static bool IsInStartup()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, false);
                return key?.GetValue(RegistryName) != null;
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
                return false;
            }
        }

        public static void AddToStartup()
        {
            try
            {
                var exePath = Environment.ProcessPath ?? "";
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
                key?.SetValue(RegistryName, exePath);
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
            }
        }

        public static void RemoveFromStartup()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
                key?.DeleteValue(RegistryName, false);
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
            }
        }
    }
}