using System.Runtime.Versioning;
using System.Text.Json;
using static Modmanager_neu.Program;
using static Modmanager_neu.Sonstiges;

namespace Modmanager_neu
{
    [SupportedOSPlatform("windows")]
    internal class BackupManager
    {
        public static readonly string steamuser = Steam.GetSteamUser();
        public static readonly string savegamepath = Path.Combine(steamuser, "Save");
        public static readonly string savebackupath = Path.Combine(Directory.GetCurrentDirectory(), "Save backup");

        private class FileDeduplication
        {
            public string? RelativePath { get; set; }
            public long FileSize { get; set; }
            public DateTime LastWriteTime { get; set; }
            public string? ReferencedBackup { get; set; }
        }

        private class BackupManifest
        {
            public string? BackupName { get; set; }
            public List<FileDeduplication> DuplicatedFiles { get; set; } = [];
        }

        public static void CreateBackup() // menu option
        {
            Sonstiges.DebugText("Starting backup creation...");
            
            string date = Convert.ToString(DateTime.Now).Replace(':', '.');
            string newbackupdirectory = Path.Combine(savebackupath, $"save({date})"); // where to save the backup
            
            // Stelle sicher, dass das Backup-Verzeichnis existiert
            if (!Directory.Exists(newbackupdirectory))
            {
                Directory.CreateDirectory(newbackupdirectory);
            }
            
            string[] savedata = Directory.GetFiles(savegamepath, "*.*", SearchOption.AllDirectories);
            

            var deduplicatedFiles = new List<FileDeduplication>();
            var filesToCopy = new List<string>();

            // Überprüfe jede Datei auf Duplikate
            for (int i = 0; i < savedata.Length; i++)
            {
                
                var fileInfo = new FileInfo(savedata[i]);
                var relPath = Path.GetRelativePath(savegamepath, savedata[i]);
                
                var duplicate = FindDuplicateInBackups(relPath, fileInfo.Length, fileInfo.LastWriteTime);
                
                if (duplicate != null)
                {
                    deduplicatedFiles.Add(new FileDeduplication
                    {
                        RelativePath = relPath,
                        FileSize = fileInfo.Length,
                        LastWriteTime = fileInfo.LastWriteTime,
                        ReferencedBackup = duplicate
                    });
                    Sonstiges.DebugText($"Datei {relPath} existiert bereits in {duplicate}");
                }
                else
                {
                    filesToCopy.Add(savedata[i]);
                }
            }

            // Kopiere nur neue Dateien
            if (filesToCopy.Count > 0)
            {
                Console.WriteLine("\n" + String.Format(Localization.T("backup.creating"), filesToCopy.Count, newbackupdirectory));
                Sonstiges.Filehelper.Copy(savegamepath, newbackupdirectory, true, [.. filesToCopy], true);
            }

            // Speichere Manifest mit duplizierten Dateien
            if (deduplicatedFiles.Count > 0)
            {
                Console.WriteLine("\n" + String.Format(Localization.T("backup.create.duplicates.optimized"), deduplicatedFiles.Count));
                SaveBackupManifest(newbackupdirectory, new BackupManifest
                {
                    BackupName = Path.GetFileName(newbackupdirectory),
                    DuplicatedFiles = deduplicatedFiles
                });
                
            }
           
            Console.WriteLine(Localization.T("backup.created"));
            Sonstiges.DebugText("Backup creation completed.");
            Menu.WaitForKeypress();
        }
        public static void LoadBackup() // menu option auf neue methode picker umstellen
        {
            Sonstiges.DebugText("Load Backups");

            if (!Directory.Exists(savebackupath))
            {
                Console.WriteLine("----   " + Localization.T("backup.no.backups") + "   ----");
                Menu.WaitForKeypress();
                return;
            }

            string[] dirs = Sonstiges.SortArray(Directory.GetDirectories(savebackupath));
            if (dirs.Length == 0)
            {
                Console.WriteLine("----   " + Localization.T("backup.no.backups") + "   ----");
                Menu.WaitForKeypress();
                return;
            }

            Console.WriteLine("\n" + Localization.T("backup.load.prompt"));

            for (int i = 0; i < dirs.Length; i++)
            {
                Console.WriteLine($"{i + 1}: " + Path.GetRelativePath(savebackupath, dirs[i]));
            }
            Console.WriteLine(Localization.T("menu.go.back.q"));
            while (true)
            {
                Console.Write(Localization.T("pointer"));
                string input = Console.ReadLine()?.Trim() ?? "";

                if (input.Equals("Q", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(Localization.T("picker.canceled"));
                    Menu.WaitForKeypress();
                    return;
                }

                if (int.TryParse(input, out int index) && index >= 1 && index <= dirs.Length)
                {
                    string backup = dirs[index - 1];
                    string[] backupFiles = [.. Directory.GetFiles(backup, "*.*", SearchOption.AllDirectories).Where(f => !f.EndsWith("manifest.json"))];

                    Console.WriteLine(string.Format(Localization.T("backup.loading"), backupFiles.Length, Path.GetRelativePath(savebackupath, backup)));

                    // Kopiere alle Dateien aus dem Backup
                    Sonstiges.Filehelper.Copy(backup, savegamepath, true, backupFiles, true);

                    // Laden der duplizierten Dateien aus Manifest
                    var manifest = LoadBackupManifest(backup);
                    if (manifest?.DuplicatedFiles.Count > 0)
                    {
                        Console.WriteLine(string.Format(Localization.T("backup.load.duplicates.optimized"), manifest?.DuplicatedFiles.Count));
                        if (manifest != null)
                            LoadDuplicatedFiles(manifest, savegamepath);
                    }

                    Console.WriteLine(string.Format(Localization.T("backup.loaded"), Path.GetRelativePath(savebackupath, backup)));
                    Menu.WaitForKeypress();
                    return;
                }
                else
                {
                    Console.WriteLine(Localization.T("menu.wrong.keypress"));
                }
            }
        }

        public static void DeleteBackup() // menu option
        {
            Sonstiges.DebugText("Delete Backups"); // auf neue methode picker umstellen

            if (!Directory.Exists(savebackupath))
            {
                Console.WriteLine("----   " + Localization.T("backup.no.backups") + "   ----");
                Menu.WaitForKeypress();
                return;
            }

            string[] dirs = Sonstiges.SortArray(Directory.GetDirectories(savebackupath));
            if (dirs.Length == 0)
            {
                Console.WriteLine("----   " + Localization.T("backup.no.backups") + "   ----");
                Menu.WaitForKeypress();
                return;
            }

            Console.WriteLine("\n" + Localization.T("backup.delete.prompt"));

            for (int i = 0; i < dirs.Length; i++)
            {
                Console.WriteLine($"{i + 1}: " + Path.GetRelativePath(savebackupath, dirs[i]));
            }
            Console.WriteLine(Localization.T("backup.delete.all.a")); //nicht mehr verfügbar
            Console.WriteLine(Localization.T("menu.go.back.q"));
            while (true)
            {
                Console.Write(Localization.T("pointer"));
                string input = Console.ReadLine()?.Trim() ?? "";

                if (input.Equals("Q", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(Localization.T("picker.canceled"));
                    Menu.WaitForKeypress();
                    return;
                }
                if (input.Equals("A", StringComparison.OrdinalIgnoreCase))
                {
                    if (Menu.YesOrNoPrompt(Localization.T("delete.all.prompt")))
                    {
                        foreach (var backup in dirs)
                            Sonstiges.Filehelper.DeleteDirectory(backup, true);

                        Console.WriteLine(Localization.T("delete.all.done"));
                        Menu.WaitForKeypress();
                        return;
                    }
                    else
                        return;
                }
                if (int.TryParse(input, out int index) && index >= 1 && index <= dirs.Length)
                {
                    string backup = dirs[index - 1];

                    string[] oldpath = Directory.GetFiles(backup, "*.*", SearchOption.AllDirectories);
                    Console.WriteLine(string.Format(Localization.T("backup.deleting"), backup, oldpath.Length));

                    Sonstiges.Filehelper.DeleteDirectory(backup, true);

                    Console.WriteLine(string.Format(Localization.T("backup.deleted"), backup));
                    Menu.WaitForKeypress();
                    return;
                }
                else
                {
                    Console.WriteLine(Localization.T("menu.wrong.keypress"));
                }
            }
        }

        public static string GetLastBackupName()
        {
            if (!Directory.Exists(savebackupath))
                return "Kein Backup vorhanden, bitte erstelle eins";

            var dirs = Directory.GetDirectories(savebackupath);
            if (dirs.Length == 0)
                return "Kein Backup vorhanden, bitte erstelle eins";

            var sorted = Sonstiges.SortArray(dirs);
            if (sorted == null || sorted.Length == 0)
                return "Kein Backup vorhanden, bitte erstelle eins";

            return Path.GetFileName(sorted[0]);
        }

        // ========== Hilfsmethoden für Deduplication ==========

        private static string? FindDuplicateInBackups(string relativePath, long fileSize, DateTime lastWriteTime)
        {
            if (!Directory.Exists(savebackupath))
                return null;

            var backups = Directory.GetDirectories(savebackupath);
            
            foreach (var backup in backups)
            {
                string filePath = Path.Combine(backup, relativePath);
                
                if (File.Exists(filePath))
                {
                    var info = new FileInfo(filePath);
                    // Vergleiche Größe und Datum (mit 1 Sekunde Toleranz)
                    if (info.Length == fileSize && 
                        Math.Abs((info.LastWriteTime - lastWriteTime).TotalSeconds) < 1)
                    {
                        return backup;
                    }
                }
            }

            return null;
        }

        private static void SaveBackupManifest(string backupPath, BackupManifest manifest)
        {
            DebugText("Saving backup manifest...");
            try
            {
                // Stelle sicher, dass das Verzeichnis existiert
                if (!Directory.Exists(backupPath))
                {
                    Directory.CreateDirectory(backupPath);
                }

                string manifestPath = Path.Combine(backupPath, "manifest.json");
                JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = true };
                var options = jsonSerializerOptions;
                string json = JsonSerializer.Serialize(manifest, options);
                File.WriteAllText(manifestPath, json);
                Sonstiges.DebugText($"Manifest gespeichert: {manifestPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format(Localization.T("manifest.save.error"), ex.Message));
            }
        }

        private static BackupManifest? LoadBackupManifest(string backupPath)
        {
            DebugText("Loading backup manifest...");
            try
            {
                string manifestPath = Path.Combine(backupPath, "manifest.json");
                if (!File.Exists(manifestPath))
                    return null;

                string json = File.ReadAllText(manifestPath);
                return JsonSerializer.Deserialize<BackupManifest>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format(Localization.T("manifest.load.error"), ex.Message));
                return null;
            }
        }

        private static void LoadDuplicatedFiles(BackupManifest manifest, string targetPath)
        {
            int totalFiles = manifest.DuplicatedFiles.Count;
            
            for (int i = 0; i < totalFiles; i++)
            {
                Sonstiges.ProgressBar.Draw(i + 1, totalFiles);
                
                var dupFile = manifest.DuplicatedFiles[i];
                
                try
                {
                    if (dupFile.ReferencedBackup == null || dupFile.RelativePath == null)
                        continue;
                    string sourcePath = Path.Combine(dupFile.ReferencedBackup, dupFile.RelativePath);
                    string targetFilePath = Path.Combine(targetPath, dupFile.RelativePath);
                    
                    // Stelle sicher, dass das Verzeichnis existiert
                    string? targetDir = Path.GetDirectoryName(targetFilePath);
                    if (targetDir != null && !Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }
                    
                    if (File.Exists(sourcePath))
                        File.Copy(sourcePath, targetFilePath, true);
                    else
                    {
                        Console.WriteLine(String.Format(Localization.T("loadduplicatedfiles.file.not.found"), sourcePath));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(String.Format(Localization.T("loadduplicatedfiles.load.error"), dupFile.RelativePath, ex.Message));
                }
            }
        }
    }
}
