using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Windows;

namespace EarTrumpet.UI.Helpers
{
    /// <summary>
    /// "Check for updates" tray menu item: looks at the latest GitHub release of this fork, and if it is newer than
    /// the running build, downloads it and swaps the files in place after the app exits.
    /// </summary>
    public static class SelfUpdater
    {
        private const string LatestReleaseUrl = "https://api.github.com/repos/Shin0Impact/FlyoutTrumpet/releases/latest";
        private const string Caption = "FlyoutTrumpet";

        // The CI build writes its run number to build.txt next to the exe. No file (e.g. a local build) counts as 0.
        private static int CurrentBuild
        {
            get
            {
                try
                {
                    var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "build.txt");
                    return File.Exists(path) && int.TryParse(File.ReadAllText(path).Trim(), out var build) ? build : 0;
                }
                catch
                {
                    return 0;
                }
            }
        }

        public static async void CheckAndUpdate()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                string zipPath;
                using (var http = new HttpClient())
                {
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("FlyoutTrumpet-updater");

                    var release = JObject.Parse(await http.GetStringAsync(LatestReleaseUrl));
                    var tag = (string)release["tag_name"] ?? "";
                    var latest = int.TryParse(new string(tag.Where(char.IsDigit).ToArray()), out var n) ? n : 0;
                    var asset = release["assets"]?.FirstOrDefault(a => ((string)a["name"]).EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

                    if (asset == null || latest <= CurrentBuild)
                    {
                        MessageBox.Show($"You're up to date (build {CurrentBuild}).", Caption, MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    var answer = MessageBox.Show($"Build {latest} is available (you have build {CurrentBuild}).\n\nInstall it now? FlyoutTrumpet will restart.",
                        Caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (answer != MessageBoxResult.Yes) return;

                    zipPath = Path.Combine(Path.GetTempPath(), "FlyoutTrumpet-update.zip");
                    File.WriteAllBytes(zipPath, await http.GetByteArrayAsync((string)asset["browser_download_url"]));
                }

                InstallAndRestart(zipPath);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"SelfUpdater: {ex}");
                MessageBox.Show("Couldn't update: " + ex.Message, Caption, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static void InstallAndRestart(string zipPath)
        {
            var exePath = Process.GetCurrentProcess().MainModule.FileName;
            var appDir = Path.GetDirectoryName(exePath);
            var scriptPath = Path.Combine(Path.GetTempPath(), "FlyoutTrumpet-update.ps1");

            // Runs after we exit (our files are locked while we run): unpack, copy over the install folder, start again.
            File.WriteAllText(scriptPath, @"
param($zip, $dir, $exe, $procId)
Wait-Process -Id $procId -ErrorAction SilentlyContinue
$tmp = Join-Path $env:TEMP 'FlyoutTrumpet-update'
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
Expand-Archive -Path $zip -DestinationPath $tmp -Force
Copy-Item (Join-Path $tmp '*') $dir -Recurse -Force
Start-Process $exe
");

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\" -zip \"{zipPath}\" -dir \"{appDir}\" -exe \"{exePath}\" -procId {Process.GetCurrentProcess().Id}",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            Application.Current.Shutdown();
        }
    }
}
