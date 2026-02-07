using System.Runtime.Versioning;
using static Modmanager_neu.Program;

namespace Modmanager_neu
{
    [SupportedOSPlatform("windows")]
    internal class BackupManager
    {
        public static readonly string steamuser = Steam.GetSteamUser();
        public static readonly string savegamepath = Path.Combine(steamuser, "Save");
        public static readonly string savebackupath = Path.Combine(Directory.GetCurrentDirectory(), "Save backup");
        public static void CreateBackup() // menu option
        {
            Sonstiges.DebugText("Starting backup creation...");
            
            string date = Convert.ToString(DateTime.Now).Replace(':', '.');
            string newbackupdirectory = Path.Combine(savebackupath, $"save({ date})"); // where to save the backup
            //dateien verschieben und überschreiben
            string[] savedata = Directory.GetFiles(savegamepath, "*.*", SearchOption.AllDirectories); // alle dateien im save ordner
            Console.WriteLine("\n" + String.Format(Localization.T("backup.creating"), savedata.Length, newbackupdirectory));

            Sonstiges.Filehelper.Copy(savegamepath,newbackupdirectory,true,savedata,true);
           
            Console.WriteLine(Localization.T("backup.created"));
            Sonstiges.DebugText(" Backup creation completed.");
            Menu.WaitForKeypress();
        }
        public static void LoadBackup() // menu option
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
                    Console.WriteLine(Localization.T("backup.load.canceled"));
                    Menu.WaitForKeypress();
                    return;
                }

                if (int.TryParse(input, out int index) && index >= 1 && index <= dirs.Length)
                {
                    string backup = dirs[index - 1];

                    // Dateien verschieben und überschreiben
                    string[] oldpath = Directory.GetFiles(backup, "*.*", SearchOption.AllDirectories);
                    Console.WriteLine(string.Format(Localization.T("backup.loading"), oldpath.Length, backup));

                    Sonstiges.Filehelper.Copy(backup, savegamepath, true, oldpath, true);

                    Console.WriteLine(string.Format(Localization.T("backup.loaded"), backup));
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
            Sonstiges.DebugText("Delete Backups");

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
            Console.WriteLine(Localization.T("backup.delete.all.a"));
            Console.WriteLine(Localization.T("menu.go.back.q"));
            while (true)
            {
                Console.Write(Localization.T("pointer"));
                string input = Console.ReadLine()?.Trim() ?? "";

                if (input.Equals("Q", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(Localization.T("backup.delete.canceled"));
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

                    // Dateien löschen
                    string[] oldpath = Directory.GetFiles(backup, "*.*", SearchOption.AllDirectories);
                    Console.WriteLine(string.Format(Localization.T("backup.deleting"), backup, oldpath.Length));

                    Sonstiges.Filehelper.DeleteDirectory(backup,true);

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

            // Nur Ordnername zurückgeben (ohne Pfad)
            return Path.GetFileName(sorted[0]);
        }

    }
}
