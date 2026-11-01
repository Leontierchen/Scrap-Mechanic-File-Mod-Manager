using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace Modmanager_neu
{
    internal class GitHubUpdater
    {
        private const string RepoOwner = "Leontierchen"; // <-- Deinen GitHub-User eintragen
        private const string RepoName = "Scrap-Mechanic-File-Mod-Manager";         // <-- Deinen Repository-Namen eintragen

        public static async Task CheckForApplicationUpdateAsync()
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
                    return;
                }

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string latestVersionStr = root.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "";

                if (string.IsNullOrEmpty(latestVersionStr))
                    return;

                // Versionen vergleichen
                if (!Version.TryParse(Program.AppVersion, out Version? currentVersion) ||
                    !Version.TryParse(latestVersionStr, out Version? latestVersion) ||
                    latestVersion <= currentVersion)
                {
                    Sonstiges.DebugText("Anwendung ist auf dem neuesten Stand.");
                    return;
                }

                // Ja / Nein Abfrage beim Nutzer über deine bestehende IO-Methode
                string promptText = $"Ein neues Update für den Modmanager ist verfügbar (v{latestVersionStr})! Möchtest du es jetzt installieren?";
                if (!IO.YesOrNoPrompt(promptText))
                {
                    Sonstiges.DebugText("Benutzer hat das Update abgelehnt.");
                    return;
                }

                Console.WriteLine("\nLade Update herunter...");

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
                    Console.WriteLine("Kein gültiges ZIP-Archiv im Release gefunden.");
                    IO.WaitForKeypress();
                    return;
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

                Console.WriteLine("Entpacke Update...");
                ZipFile.ExtractToDirectory(zipPath, extractDir);

                // Falls das Entpacken einen einzelnen Unterordner erzeugt hat (z. B. bei GitHub Source-Zips), den Inhalt auflösen
                string finalSourceDir = extractDir;
                var subDirs = Directory.GetDirectories(extractDir);
                if (subDirs.Length == 1 && Directory.GetFiles(extractDir).Length == 0)
                {
                    finalSourceDir = subDirs[0];
                }

                Console.WriteLine("Starte Update-Vorgang und starte neu...");
                ApplyUpdateAndRestart(finalSourceDir, appDir);
            }
            catch (Exception ex)
            {
                Sonstiges.DebugText($"Fehler beim App-Update-Check: {ex.Message}");
            }
        }

        private static void ApplyUpdateAndRestart(string sourceDir, string targetDir)
        {
            string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? Path.Combine(targetDir, "Modmanager_neu.exe");
            string batchScriptPath = Path.Combine(Path.GetTempPath(), "finish_modmanager_update.bat");

            // Batch-Datei erstellen: Wartet 2 Sek., kopiert alle entpackten Dateien (inkl. language/) drüber, startet neu & löscht sich selbst
            string batchContent = $@"@echo off
timeout /t 2 /nobreak > NUL
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

            Process.Start(startInfo);

            // Beendet das Programm sauber über dein Log-System, um Dateisperren sofort aufzuheben
            Program.WriteLogAndExit(0, "App Update wird angewendet");
        }
    }
}