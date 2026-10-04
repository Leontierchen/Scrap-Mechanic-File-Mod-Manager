using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using static Modmanager_neu.Program;

namespace Modmanager_neu
{
    [SupportedOSPlatform("windows")]
    internal class WorkshopCacheManager
    {
        
        private const string GameProcessName = "ScrapMechanic";
        private const string SteamProcessName = "steam";
        private const int SmShutdownTimeoutSec = 10;
        private const int SteamShutdownTimeoutSec = 15;
        private const int SteamStartupTimeoutSec = 30;

        /// <summary>
        /// Startet den Workshop Cache Manager mit Benutzerabfrage.
        /// </summary>
        
       
        public static void ExecuteMode(int mode)
        {
            try
            {
                // 1. Locate Steam
                string? steamPath = SteamLibraryFinder.GetSteamInstallPath();
                if (string.IsNullOrEmpty(steamPath))
                {
                    IO.ShowMessage("workshop.cache.manager.steam.not.found");
                    return;
                }
                Sonstiges.DebugText($"Steam path located: {steamPath}");

                // 2. Find workshop directory for App ID
                string? workshopContentDir = FindWorkshopContentDir(steamPath);
                if (string.IsNullOrEmpty(workshopContentDir))
                {
                    IO.ShowMessage("workshop.cache.manager.workshop.not.found");
                    return;
                }
                Sonstiges.DebugText($"Workshop content dir: {workshopContentDir}");

                if (mode == 1)
                {
                    // Option 1: Clear Cache Only
                    ClearCacheMode(workshopContentDir);
                }
                else if (mode == 2)
                {
                    // Option 2: Full Wipe
                    FullWipeMode(steamPath, workshopContentDir);
                }
            }
            catch (Exception ex)
            {
                IO.ShowMessage("workshop.cache.manager.error", [ex.Message]);
            }
        }

        private static void ClearCacheMode(string workshopContentDir)
        {
            Sonstiges.DebugText("Workshop cache cleaner started...");
            StopScrapMechanicProcess();

            int deletedCount = 0;
            IO.ShowMessage("workshop.cache.manager.scanning");
            var modDirs = Directory.GetDirectories(workshopContentDir);
            IO.ShowMessage("workshop.cache.manager.deleting.cache");
            for (int i = 0; i < modDirs.Length; i++)
            {
                string cacheDir = Path.Combine(modDirs[i], "Cache");
                if (Directory.Exists(cacheDir))
                {
                    try
                    {
                        Sonstiges.Filehelper.DeleteDirectory(cacheDir, useProgressBar: false);
                        deletedCount++;
                        Sonstiges.DebugText($"Cache wiped: {Path.GetFileName(modDirs[i])}\\Cache");
                    }
                    catch (Exception ex)
                    {
                        Sonstiges.DebugText($"Warning: Could not delete {Path.GetFileName(modDirs[i])}\\Cache ({ex.Message})");
                    }
                }

                Sonstiges.ProgressBar.Draw(i + 1, modDirs.Length);
            }

            IO.ShowMessage("workshop.cache.manager.cache.cleared", [deletedCount.ToString(), modDirs.Length.ToString()]);

            IO.WaitForKeypress();
        }

        private static void FullWipeMode(string steamPath, string workshopContentDir)
        {
            Sonstiges.DebugText("Workshop full wipe started...");
            StopScrapMechanicProcess();
            StopSteamProcess();

            Sonstiges.DebugText("Deleting workshop ACF and content directory...");
            string workshopAcf = Path.Combine(steamPath, "steamapps", "workshop", $"appworkshop_{AppId}.acf");

            // Delete workshop ACF
            if (File.Exists(workshopAcf))
            {
                try
                {
                    File.Delete(workshopAcf);
                    IO.ShowMessage("workshop.cache.manager.acf.removed");
                }
                catch (Exception ex)
                {
                    WriteLogAndExit(7, ex.Message);
                }
            }
            
            // Delete workshop content directory
            if (Directory.Exists(workshopContentDir))
            {
                IO.ShowMessage("workshop.cache.manager.content.removing");
                try
                {
                    Sonstiges.Filehelper.DeleteDirectory(workshopContentDir, useProgressBar: true);
                    IO.ShowMessage("workshop.cache.manager.content.removed");
                }
                catch (Exception ex)
                {
                    WriteLogAndExit(13, ex.Message);
                }
            }

            StartSteamAndWait(Path.Combine(steamPath, "steam.exe"));
            Menu.StartGame();
        }

        private static string? FindWorkshopContentDir(string steamPath)
        {
            string vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");

            if (!File.Exists(vdfPath))
                IO.ShowMessage("workshop.cache.manager.vdf.not.found");

            string? libraryPath = null;

            try
            {
                string vdfContent = File.ReadAllText(vdfPath);
                string? currentPath = null;

                foreach (string line in vdfContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
                {
                    // Parse "path" entries
                    var pathMatch = Regex.Match(line, @"""path""\s+""([^""]+)""");
                    if (pathMatch.Success)
                    {
                        currentPath = pathMatch.Groups[1].Value.Replace("\\\\", "\\");
                    }

                    // Check if current path contains our AppId
                    if (currentPath != null && line.Contains($"\"{AppId}\""))
                    {
                        libraryPath = currentPath;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(libraryPath))
                    libraryPath = steamPath; // Fallback to main Steam path
            }
            catch (Exception ex)
            {
                Sonstiges.DebugText($"VDF parsing error: {ex.Message}");
                libraryPath = steamPath;
            }

            return Path.Combine(libraryPath, "steamapps", "workshop", "content", AppId);
        }

        private static void StopScrapMechanicProcess()
        {
            Sonstiges.DebugText("Checking for running Scrap Mechanic process...");
            var process = Process.GetProcessesByName(GameProcessName).FirstOrDefault();
            if (process != null)
            {
                IO.ShowMessage("workshop.cache.manager.closing.game");
                process.CloseMainWindow();

                var stopwatch = Stopwatch.StartNew();
                while (Process.GetProcessesByName(GameProcessName).Length > 0 && 
                       stopwatch.Elapsed.TotalSeconds < SmShutdownTimeoutSec)
                {
                    Thread.Sleep(500);
                }

                if (Process.GetProcessesByName(GameProcessName).Length > 0)
                {
                    Sonstiges.DebugText("Game did not exit cleanly; forcing termination...");
                    foreach (var p in Process.GetProcessesByName(GameProcessName))
                    {
                        p.Kill();
                    }
                }

                IO.ShowMessage("workshop.cache.manager.game.closed");
            }
        }

        private static void StopSteamProcess()
        {
            Sonstiges.DebugText("Checking for running Steam process...");
            var process = Process.GetProcessesByName(SteamProcessName).FirstOrDefault();
            if (process != null)
            {
                IO.ShowMessage("workshop.cache.manager.closing.steam");

                try
                {
                    Process.Start("steam://exit");
                }
                catch { }

                var stopwatch = Stopwatch.StartNew();
                while (Process.GetProcessesByName(SteamProcessName).Length > 0 && 
                       stopwatch.Elapsed.TotalSeconds < SteamShutdownTimeoutSec)
                {
                    Thread.Sleep(500);
                }

                if (Process.GetProcessesByName(SteamProcessName).Length > 0)
                {
                    Sonstiges.DebugText("Steam took too long to exit; forcing termination...");
                    foreach (var p in Process.GetProcessesByName(SteamProcessName))
                    {
                        p.Kill();
                    }
                }

                IO.ShowMessage("workshop.cache.manager.steam.closed");
            }
        }

        private static void StartSteamAndWait(string steamExePath)
        {
            IO.ShowMessage("workshop.cache.manager.starting.steam");

            if (File.Exists(steamExePath))
            {
                Process.Start(steamExePath);
            }
            else
            {
                Process.Start("steam://open");
            }

            Sonstiges.DebugText("Waiting for Steam to log in and initialize...");

            var stopwatch = Stopwatch.StartNew();
            bool isReady = false;

            while (stopwatch.Elapsed.TotalSeconds < SteamStartupTimeoutSec)
            {
                try
                {
                    using (RegistryKey ?key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess"))
                    {
                        if (key != null)
                        {
                            var activeUser = key.GetValue("ActiveUser");
                            var pid = key.GetValue("pid");

                            if (activeUser != null && pid != null && 
                                (int)activeUser > 0 && (int)pid > 0)
                            {
                                if (Process.GetProcessById((int)pid) != null)
                                {
                                    isReady = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch { }

                Thread.Sleep(500);
            }

            if (!isReady)
                //throw new TimeoutException(string.Format(Localization.T("workshop.cache.manager.steam.timeout"), SteamStartupTimeoutSec));
                IO.ShowMessage("workshop.cache.manager.steam.timeout", [SteamStartupTimeoutSec.ToString()]);
            else
                IO.ShowMessage("workshop.cache.manager.steam.ready");

        }
    }
}
