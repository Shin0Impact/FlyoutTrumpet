using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace EarTrumpet.UI.Helpers
{
    // Start-with-Windows for the portable (unpackaged) build, via HKCU Run.
    public static class StartupHelper
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppKey = @"Software\FlyoutTrumpet";
        private const string ValueName = "FlyoutTrumpet";

        private static string ExePath => Process.GetCurrentProcess().MainModule.FileName;

        public static bool IsEnabled
        {
            get
            {
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    {
                        return k?.GetValue(ValueName) != null;
                    }
                }
                catch { return false; }
            }
        }

        public static void SetEnabled(bool enabled)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (enabled) k.SetValue(ValueName, "\"" + ExePath + "\"");
                    else k.DeleteValue(ValueName, false);
                }
            }
            catch (Exception ex) { Trace.WriteLine(ex); }
        }

        public static void Toggle() => SetEnabled(!IsEnabled);

        // On first run enable it; afterwards just keep the path current (moves/updates).
        public static void Initialize()
        {
            try
            {
                using (var app = Registry.CurrentUser.CreateSubKey(AppKey))
                {
                    if (app.GetValue("StartupInitialized") == null)
                    {
                        app.SetValue("StartupInitialized", 1);
                        SetEnabled(true);
                    }
                    else if (IsEnabled)
                    {
                        SetEnabled(true);
                    }
                }
            }
            catch (Exception ex) { Trace.WriteLine(ex); }
        }
    }
}
