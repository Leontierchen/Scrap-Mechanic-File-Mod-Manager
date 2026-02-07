
using Microsoft.Win32;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static Modmanager_neu.Program;

namespace Modmanager_neu
{
    [SupportedOSPlatform("windows")]
    internal class Modtool
    {
        private static readonly string gamepath = GetGamePath(); 
        private static readonly string modtool = "Mod_manager_by_leon"; // relativ zum Spielordner
        private static readonly string modversionfile = "version.txt";
        private static readonly string modspath = Path.Combine(gamepath, modtool,"mods");
        private static readonly string vanillapath = Path.Combine(gamepath, modtool, "vanilla");

        public static string GetCurrentMod()
        {
            Sonstiges.DebugText("Starte Ermittlung des aktuellen Mods...");
            string currentmod; // Kompiliert sauber
            
            if (!File.Exists(Path.Combine(gamepath,modtool,modversionfile)))
            {
                Directory.CreateDirectory(Path.Combine(gamepath, modtool, "mods"));
                Directory.CreateDirectory(Path.Combine(gamepath, modtool, "vanilla"));
            }

            string file = Path.Combine(gamepath, modtool, modversionfile);
            bool exists = File.Exists(file);
            if (exists == false)
            {
                Console.WriteLine(file);
                File.WriteAllText(file, "Installed Version\nVanilla");
                currentmod = "Vanilla";
            }
            else
            {
                string[] line = File.ReadAllLines(file);
                currentmod = line[1];
            }
            Sonstiges.DebugText("Ende Modermittlung. --> " + currentmod);
            return currentmod;
        }
        public static string GetGamePath()
        {
            Sonstiges.DebugText("Starte Ermittlung des Spielpfads...");

            string gamepath = string.Empty;
            var folders = SteamLibraryFinder.GetAllSteamGameFolders();

            foreach (var folder in folders)
            {
                gamepath = Path.Combine(folder, "Scrap Mechanic");
                Sonstiges.DebugText("Überprüfe Pfad: " + gamepath);
                if (Directory.Exists(gamepath))
                    {
                    Sonstiges.DebugText("Spielpfad gefunden: " + gamepath);
                    break;
                }
                else
                {
                    gamepath = string.Empty;
                }
            }
            if (string.IsNullOrEmpty(gamepath))
            {
                Sonstiges.DebugText("Spielpfad nicht gefunden. Nutzereingabe");
                Console.WriteLine(Localization.T("gamepath.notfound"));
                int tries = 3;
                while (tries > 0) 
                {
                    Console.WriteLine(Localization.T("gamepath.promt"));
                    string? inputPath = Console.ReadLine();
                    if (inputPath != null && Directory.Exists(inputPath) && File.Exists(Path.Combine(inputPath,"Release","ScrapMechanic.exe")))
                    {
                        gamepath = inputPath;
                        Sonstiges.DebugText("Spielpfad durch Nutzereingabe gefunden: " + gamepath);
                        break;
                    }
                    else
                    {
                        Console.WriteLine(String.Format(Localization.T("wrong.folder.input"),tries));
                        tries--;
                    }
                }
                if (IsDebug && tries == 0 && string.IsNullOrEmpty(gamepath))
                {
                    Console.WriteLine("[Debug] Alle eingabeversuche Fehlgeschlagen. Breche ab.");
                }
            }
            Sonstiges.DebugText("Ende Spielpfadermittlung. --> " + gamepath);

            config.GamePath = gamepath;
            SaveConfig(config);
            return gamepath;
        }

        internal static void AddMod() // menu option
        {
            Sonstiges.DebugText("Starte hinzufügen einer neuen Mod...");

            Console.WriteLine(Localization.T("mods.menu.new.mod.description"));
            Console.WriteLine(Localization.T("mods.menu.new.mod.name.prompt"));
            bool validname = false;
            string? modname = string.Empty;
            List<string> modslist = [];
            while (validname == false)
            {
                Console.Write(Localization.T("pointer"));
                modname = Console.ReadLine();
                if (modname != null)
                {
                    switch (modname)
                    {
                        case "Q" or "q":
                            Sonstiges.DebugText("Modnamen Eingabe abgebrochen durch Nutzer.");
                            return;
                        case "Vanilla":
                            Console.WriteLine(Localization.T("mods.menu.new.mod.name.invalid"));
                            break;
                        case "":
                            Console.WriteLine(Localization.T("mods.menu.new.mod.name.empty"));
                            break;
                        case string s when s.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0:
                            Console.WriteLine(Localization.T("mods.menu.new.mod.name.invalid"));
                            break;
                        default:
                            if (Directory.Exists($@"{modspath}\{modname}"))
                            {
                                Console.WriteLine(Localization.T("mods.menu.new.mod.name.exists"));
                                break;
                            }
                            else
                            {
                                validname = true;
                                break;
                            }
                    }
                }
                else
                {
                    Console.WriteLine(Localization.T("mods.menu.new.mod.name.invalid"));
                }
            }
            Sonstiges.DebugText("Modname akzeptiert: " + modname);
            Sonstiges.DebugText("Erstelle Directory.");
            string modnamepath = $@"{modspath}\{modname}";
            string modfiles = $@"{modnamepath}\files";
            try
            { Directory.CreateDirectory(modfiles); }
            catch (Exception ex)
            { WriteLogAndExit(8, ex.Message); } // Fehler beim Erstellen des Modverzeichnisses, wahrscheinlich ungültige Zeichen im Modnamen, obwohl vorher geprüft.

            bool done = false;
            while (done == false)
            {
                bool validpath = false;

                string type = "none";
                Console.WriteLine(Localization.T("mods.menu.new.mod.path.prompt"));
                while (validpath == false)
                {
                    Console.Write(Localization.T("pointer"));
                    string? inputpath = Console.ReadLine();
                    if (inputpath != null)
                    {
                        switch (inputpath)
                        {
                            case "Q" or "q":
                                Sonstiges.DebugText("[Debug] Modnamen Eingabe abgebrochen durch Nutzer.");
                                return;
                            case "":
                                Console.WriteLine(Localization.T("mods.menu.new.mod.path.empty"));
                                break;
                            case string s when s.IndexOfAny(Path.GetInvalidPathChars()) >= 0:
                                Console.WriteLine(Localization.T("mods.menu.new.mod.path.invalid"));
                                break;
                            default:
                                if (Directory.Exists(inputpath))
                                {
                                    type = "directory";
                                    validpath = true;
                                    break;
                                }
                                else if (File.Exists(inputpath))
                                {
                                    type = "zip";
                                    validpath = true;
                                    break;
                                }
                                else
                                {
                                    Console.WriteLine(Localization.T("mods.menu.new.mod.path.invalid"));
                                    break;
                                }
                        }
                        if (type == "zip")
                        {
                            Console.WriteLine(Localization.T("mods.menu.new.mod.extract.zip"));
                            try
                            { ZipFile.ExtractToDirectory(inputpath, modfiles, true); }
                            catch (Exception ex)
                            { WriteLogAndExit(9, ex.Message); } //Fehler beim extrahieren der zip
                            Console.WriteLine(Localization.T("mods.menu.new.mod.extract.zip.done"));
                        }
                        else if (type == "directory") 
                        {
                            Console.WriteLine(Localization.T("mods.menu.new.mod.extract.directory"));
                            Sonstiges.Filehelper.Copy(inputpath, modfiles, true, Directory.GetFiles(inputpath, "*.*", SearchOption.AllDirectories),true);
                            Console.WriteLine(Localization.T("mods.menu.new.mod.extract.directory.done"));
                        }
                        if (Menu.YesOrNoPrompt(Localization.T("mods.menu.new.mod.add.more.mods.question")) == false)
                        {
                            Sonstiges.DebugText("Mods hinzufügen fertig");
                            done = true;
                        }
                        else { Sonstiges.DebugText("noch eine mod hinzufügen, fortsetzen"); }
                    }
                    else { Console.WriteLine(Localization.T("mods.menu.new.mod.name.invalid")); }
                    modslist.Add(modfiles);
                }
                
            }
            //document all mods and files into text files inside the mod folder
            File.WriteAllLines($@"{modnamepath}\modlist.txt", [.. modslist]);
            string[] files = Directory.GetFiles(modfiles, ".", SearchOption.AllDirectories);
            File.WriteAllLines($@"{modnamepath}\contents.txt", files);
            Console.WriteLine(String.Format(Localization.T("mods.menu.new.mod.finished"), modname, Localization.TArray("mods.menu.options")[0]));
            Menu.WaitForKeypress();
        }
        internal static void SwitchMod() // menu option
        {
            //Sonstiges.DebugText("Load available mods");

            //if (!Directory.Exists(modspath))
            //{
            //    Console.WriteLine("----   " + Localization.T("mods.menu.switch.mods.no.mods") + "   ----");
            //    Menu.WaitForKeypress();
            //    return;
            //}

            //string[] dirs = Sonstiges.SortArray(Directory.GetDirectories(savebackupath));
            //if (dirs.Length == 0)
            //{
            //    Console.WriteLine("----   " + Localization.T("backup.no.backups") + "   ----");
            //    Menu.WaitForKeypress();
            //    return;
            //}

            //Console.WriteLine("\n" + Localization.T("backup.load.prompt"));

            //for (int i = 0; i < dirs.Length; i++)
            //{
            //    Console.WriteLine($"{i + 1}: " + Path.GetRelativePath(savebackupath, dirs[i]));
            //}
            //Console.WriteLine(Localization.T("menu.go.back.q"));
            //while (true)
            //{
            //    Console.Write(Localization.T("pointer"));
            //    string input = Console.ReadLine()?.Trim() ?? "";

            //    if (input.Equals("Q", StringComparison.OrdinalIgnoreCase))
            //    {
            //        Console.WriteLine(Localization.T("backup.load.canceled"));
            //        Menu.WaitForKeypress();
            //        return;
            //    }

            //    if (int.TryParse(input, out int index) && index >= 1 && index <= dirs.Length)
            //    {
            //        string backup = dirs[index - 1];

            //        // Dateien verschieben und überschreiben
            //        string[] oldpath = Directory.GetFiles(backup, "*.*", SearchOption.AllDirectories);
            //        Console.WriteLine(string.Format(Localization.T("backup.loading"), oldpath.Length, backup));

            //        Sonstiges.Filehelper.Copy(backup, savegamepath, true, oldpath, true);

            //        Console.WriteLine(string.Format(Localization.T("backup.loaded"), backup));
            //        Menu.WaitForKeypress();
            //        return;
            //    }
            //    else
            //    {
            //        Console.WriteLine(Localization.T("menu.wrong.keypress"));
            //    }
            //}
        }
        internal static void UpdateMod() // menu option
        {
            Menu.ShowPlaceholder("Mod updaten");
        }
        internal static void RenameMod() // menu option
        {
            Menu.ShowPlaceholder("Mod umbenennen");
        }
        internal static void RemoveMod() // menu option
        {
            Menu.ShowPlaceholder("Mod löschen");
        }
    }
    [SupportedOSPlatform("windows")]
    public static class SteamLibraryFinder
    {
        public static List<string> GetAllSteamGameFolders()
        {
            var gameFolders = new List<string>();

            string? steamPath = GetSteamInstallPath();
            if (steamPath == null)
                return gameFolders;

            string steamApps = Path.Combine(steamPath, "steamapps");
            AddCommonFolder(gameFolders, steamApps);

            string libraryFile = Path.Combine(steamApps, "libraryfolders.vdf");
            if (!File.Exists(libraryFile))
                return gameFolders;

            string content = File.ReadAllText(libraryFile);

            // findet Pfade wie: "path"    "D:\\SteamLibrary"
            var matches = Regex.Matches(content, "\"path\"\\s+\"([^\"]+)\"");

            foreach (Match match in matches)
            {
                string path = match.Groups[1].Value.Replace(@"\\", @"\");
                string libSteamApps = Path.Combine(path, "steamapps");
                AddCommonFolder(gameFolders, libSteamApps);
            }

            return gameFolders;
        }

        private static void AddCommonFolder(List<string> list, string steamAppsPath)
        {
            string common = Path.Combine(steamAppsPath, "common");
            if (Directory.Exists(common))
                list.Add(common);
        }
        private static string? GetSteamInstallPath()
        {
            string[] registryPaths =
            [
                @"HKEY_CURRENT_USER\Software\Valve\Steam",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam"
            ];

            foreach (var path in registryPaths)
            {
                var value = Registry.GetValue(path, "SteamPath", null) as string;
                if (!string.IsNullOrEmpty(value))
                    return value.Replace('/', '\\');
            }

            return null;
        }
    }
}
