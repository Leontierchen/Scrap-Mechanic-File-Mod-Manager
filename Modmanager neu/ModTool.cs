
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
        private static readonly string contentsfile = "contents.txt";
        private static readonly string modlistsfile = "modlist.txt";
        public static string GetCurrentMod()
        {
            Sonstiges.DebugText("Starte Ermittlung des aktuellen Mods...");
            string currentmod; // Kompiliert sauber
            
            if (!Path.Exists(Path.Combine(gamepath, modtool)))
            {
                Directory.CreateDirectory(modspath);
                Directory.CreateDirectory(vanillapath);
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
            Console.WriteLine(Localization.T("menu.go.back.q"));
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
                Console.WriteLine(Localization.T("menu.go.back.q"));
                while (validpath == false)
                {
                    Console.Write(Localization.T("pointer"));
                    string? inputpath = Console.ReadLine();
                    if (inputpath != null)
                    {
                        Console.WriteLine();
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
                        modslist.Add(inputpath);
                        if (Menu.YesOrNoPrompt(Localization.T("mods.menu.new.mod.add.more.mods.question")) == false)
                        {
                            Sonstiges.DebugText("Mods hinzufügen fertig");
                            done = true;
                        }
                        else { Sonstiges.DebugText("noch eine mod hinzufügen, fortsetzen"); }
                    }
                    else { Console.WriteLine(Localization.T("mods.menu.new.mod.name.invalid")); }
                    
                }
                
            }
            //document all mods and files into text files inside the mod folder
            File.WriteAllLines(Path.Combine(modnamepath,modlistsfile), [.. modslist]);
            string[] files = Directory.GetFiles(modfiles, ".", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                files[i] = (Path.GetRelativePath(modfiles, files[i]));
            }
            
            File.WriteAllLines(Path.Combine(modnamepath, contentsfile), files);
            Console.WriteLine(String.Format(Localization.T("mods.menu.new.mod.finished"), modname, Localization.TArray("mods.menu.options")[0]));
            Menu.WaitForKeypress();
        }
        internal static void SwitchMod() // menu option
        {
            Sonstiges.DebugText("Load available mods");
            string activemod = GetCurrentMod(); //only the name
            if (!Directory.Exists(modspath))
            {
                Console.WriteLine("----   " + Localization.T("mods.menu.change.no.mods") + "   ----");
                Menu.WaitForKeypress();
                return;
            }

            string[] dirs = Directory.GetDirectories(modspath);
            if (dirs.Length == 0)
            {
                Console.WriteLine("----   " + Localization.T("mods.menu.change.no.mods") + "   ----");
                Menu.WaitForKeypress();
                return;
            }

            Console.WriteLine("\n" + Localization.T("mods.menu.change.active.mod.prompt"));
            if (activemod != "Vanilla")
                Console.WriteLine("0: " + Localization.T("modspath.menu.disable.mods"));
            for (int i = 0; i < dirs.Length; i++)
            {
                Console.WriteLine($"{i + 1}: " + Path.GetRelativePath(modspath, dirs[i]));
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

                if (int.TryParse(input, out int index) && index >= 0 && index <= dirs.Length)
                {
                    
                    if (index == 0 & activemod != "Vanilla") //vanilla restore
                    {
                        ModToVanilla(activemod);
                        try
                        {
                            File.WriteAllText(Path.Combine(gamepath, modtool, modversionfile), $"Installed Version\nVanilla");
                        }
                        catch (Exception ex)
                        {
                            WriteLogAndExit(10, ex.Message); //write file error
                        }
                    }
                    else
                    {
                        string newmoddir = dirs[index - 1]; //full path to the mod selected
                        string newmod = Path.GetRelativePath(modspath, newmoddir); //only the name
                        if (newmod == activemod)
                        {
                            Console.WriteLine(Localization.T("mods.menu.change.same"));
                        }
                        else if (activemod == "Vanilla")
                        {
                            VanillaToMod(newmod);
                        }
                        else
                        {
                            ModToVanilla(activemod);
                            VanillaToMod(newmod);
                        }
                        try
                        {
                            File.WriteAllText(Path.Combine(gamepath, modtool, modversionfile), $"Installed Version\n{newmod}");
                        }
                        catch (Exception ex)
                        {
                            WriteLogAndExit(10, ex.Message); //write file error
                        }
                    }
                    break;
                }
                else
                {
                    Console.WriteLine(Localization.T("menu.wrong.keypress"));
                }
            }
            Menu.WaitForKeypress();
            return;
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
        static void VanillaToMod(string modpath)
        {
            modpath = Path.Combine(modspath, modpath);
            Sonstiges.DebugText("Vanilla To Mod");
            //vanillabackup
            Sonstiges.DebugText($"Lese Datei: {Path.Combine(modpath, contentsfile)}");
            string[] modfiles = File.ReadAllLines(Path.Combine(modpath, contentsfile)); //relative paths
            Console.WriteLine(Localization.T("mods.menu.vanillatomod.start"));
            List<string> vanillafiles = [];
            Sonstiges.DebugText("Erstelle Database für Vanillafiles, die gesichert werden müssen");
            foreach (string item in modfiles)
            {
                bool exists = File.Exists(Path.Combine(gamepath, item));
                if (exists == true)
                {
                    vanillafiles.Add(item); 
                }
            }
            Sonstiges.DebugText($"Database mit {vanillafiles.Count} Dateien erstellt. Starte Kopiervorgang");
            if (!Directory.Exists(vanillapath))
                Directory.CreateDirectory(vanillapath);
            Sonstiges.DebugText($"Schreibe Datei: {Path.Combine(vanillapath, contentsfile)}");
            try
            {
                File.WriteAllLines(Path.Combine(vanillapath, contentsfile), [.. vanillafiles]); //relative paths
            }
            catch (Exception ex)
            {
                WriteLogAndExit(10, ex.Message); //write file error
            }
            Sonstiges.Filehelper.Move(gamepath, Path.Combine(vanillapath, "files"), false, [.. vanillafiles], true, true);
            Console.WriteLine(Localization.T("mods.menu.vanillatomod.vanillasaved"));
            //mod install
            Sonstiges.Filehelper.Move(Path.Combine(modpath, "files"), gamepath, true, modfiles, true, true);
            Console.WriteLine(Localization.T("mods.menu.vanillatomod.done"));
        }
        static void ModToVanilla(string currentmod)
        {
            Sonstiges.DebugText("Mod To Vanilla");
            //mod uninstall
            string? modpath = Path.Combine(modspath, currentmod);
            if (modpath != null & Path.Exists(modpath))
            {
                Console.WriteLine(Localization.T("mods.menu.modtovanilla.start"));
                Sonstiges.DebugText($"Lese Datei: {Path.Combine(modpath, contentsfile)}");
                string[] modfiles = File.ReadAllLines(Path.Combine(modpath, contentsfile)); //relative paths
                Sonstiges.DebugText($"Lese Datei: {Path.Combine(vanillapath, contentsfile)}");
                string[] vanillafiles = File.ReadAllLines(Path.Combine(vanillapath, contentsfile)); //relative paths
                Sonstiges.Filehelper.Move(gamepath, Path.Combine(modpath,"files"), true, modfiles, true, true);
                Console.WriteLine(Localization.T("mods.menu.modtovanilla.moduninstall.done"));
                Sonstiges.Filehelper.Move(Path.Combine(vanillapath,"files"), gamepath, true, vanillafiles, true, true);
                Console.WriteLine(Localization.T("mods.menu.modtovanilla.vanillarecovery.done"));
            }
                
        }
    }
    
}
