using System;
using System.ComponentModel.Design.Serialization;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Modmanager_neu.Program;

namespace Modmanager_neu
{
    internal class GitHubUpdater
    {
        private const string RepoOwner = "Leontierchen"; // <-- Deinen GitHub-User eintragen
        private const string RepoName = "Scrap-Mechanic-File-Mod-Manager";         // <-- Deinen Repository-Namen eintragen

        public static async Task<string> CheckForApplicationUpdateAsync()
        {
            try
            {
                Sonstiges.DebugText("Prüfe auf GitHub-App-Updates...");

                using var client = new HttpClient();
                // GitHub API setzt zwingend einen User-Agent voraus!
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ModManager-Updater", "1.0"));

                string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

                HttpResponseMessage response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    Sonstiges.DebugText($"GitHub-Update-Check fehlgeschlagen Status code: {response.StatusCode}");
                    return "error";
                }

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string latestVersionStr = root.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "";

                if (string.IsNullOrEmpty(latestVersionStr))
                    return "error";

                // Versionen vergleichen
                if (!Version.TryParse(Program.AppVersion, out Version? currentVersion) ||
                    !Version.TryParse(latestVersionStr, out Version? latestVersion) ||
                    latestVersion <= currentVersion)
                {
                    Sonstiges.DebugText("Anwendung ist auf dem neuesten Stand.");
                    return "false";
                }

                // Ja / Nein Abfrage beim Nutzer über deine bestehende IO-Methode
                if (!IO.YesOrNoPrompt(string.Format(Localization.T("github.updater.update.available"), Program.AppVersion, latestVersionStr)))
                {
                    Sonstiges.DebugText("Benutzer hat das Update abgelehnt.");
                    return "denied";
                }

                IO.ShowMessage("github.updater.update.downloading");

                // Suche nach einer angehängten .zip Datei im Release
                string? zipUrl = null;
                if (root.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        string name = asset.GetProperty("name").GetString() ?? "";
                        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            zipUrl = asset.GetProperty("browser_download_url").GetString();
                            break;
                        }
                    }
                }

                // Fallback auf Source-Zip, falls kein Asset manuell hochgeladen wurde
                if (string.IsNullOrEmpty(zipUrl) && root.TryGetProperty("zipball_url", out var zipball))
                {
                    zipUrl = zipball.GetString();
                }

                if (string.IsNullOrEmpty(zipUrl))
                {
                    IO.ShowMessage("github.updater.download.error");
                    IO.WaitForKeypress();
                    return "error";
                }

                // Pfade vorbereiten
                string appDir = AppContext.BaseDirectory;
                string tempDir = Path.Combine(Path.GetTempPath(), $"ModManager_Update_{Guid.NewGuid()}");
                string zipPath = Path.Combine(tempDir, "update.zip");
                string extractDir = Path.Combine(tempDir, "extracted");

                Directory.CreateDirectory(extractDir);

                // Download der ZIP-Datei
                byte[] zipBytes = await client.GetByteArrayAsync(zipUrl);
                await File.WriteAllBytesAsync(zipPath, zipBytes);

                IO.ShowMessage("github.updater.update.extracting");
                ZipFile.ExtractToDirectory(zipPath, extractDir);

                // Falls das Entpacken einen einzelnen Unterordner erzeugt hat (z. B. bei GitHub Source-Zips), den Inhalt auflösen
                string finalSourceDir = extractDir;
                var subDirs = Directory.GetDirectories(extractDir);
                if (subDirs.Length == 1 && Directory.GetFiles(extractDir).Length == 0)
                {
                    finalSourceDir = subDirs[0];
                }

                IO.ShowMessage("github.updater.update.starting");
                ApplyUpdateAndRestart(finalSourceDir, appDir);
                return "true";
            }
            catch (Exception ex)
            {
                Sonstiges.DebugText($"Fehler beim App-Update-Check: {ex.Message}");
                if (ex.Message.Contains("api.github.com:443"))
                {
                    IO.ShowMessage("github.updater.checking.nointernet");
                    return "error no internet";
                }
                return "error";
            }
        }

        private static void ApplyUpdateAndRestart(string sourceDir, string targetDir)
        {
            string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? Path.Combine(targetDir, "Modmanager_neu.exe");
            string batchScriptPath = Path.Combine(Path.GetTempPath(), "finish_modmanager_update.bat");

            // Batch-Datei erstellen: Wartet 4 Sek., kopiert alle entpackten Dateien (inkl. language/) drüber, startet neu & löscht sich selbst
            string batchContent = $@"@echo off
timeout /t 4 /nobreak > NUL
xcopy ""{sourceDir}\*"" ""{targetDir}"" /E /Y /I
start """" ""{currentExe}""
del ""%~f0""
";

            File.WriteAllText(batchScriptPath, batchContent);

            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batchScriptPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Sonstiges.DebugText("Starte Batch-Datei für Update und Neustart...");
            Process.Start(startInfo);
            
            Program.WriteLogAndExit(0);
        }
    }
}