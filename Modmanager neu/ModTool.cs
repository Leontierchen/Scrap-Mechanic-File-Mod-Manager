using System.ComponentModel.Design;
using System.IO.Compression;
using System.Runtime.Versioning;
using static Modmanager_neu.Program;

namespace Modmanager_neu
{
    [SupportedOSPlatform("windows")]
    internal class Modtool
    {
        private static readonly string gamepath = GetGamePath();
        private static readonly string modtool = "Mod_manager_by_leon"; // relativ zum Spielordner
        private static readonly string modversionfile = "version.txt";
        private static readonly string modspath = Path.Combine(gamepath, modtool, "mods");
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
        public static void SetCurrentMod(string option)
        {
            Sonstiges.DebugText("Setze aktuellen Mod auf: " + option);
            try
            {
                File.WriteAllText(Path.Combine(gamepath, modtool, modversionfile), $"Installed Version\n{option}");
            }
            catch (Exception ex)
            {
                WriteLogAndExit(10, ex.Message); //write file error
            }
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
                    if (inputPath != null && Directory.Exists(inputPath) && File.Exists(Path.Combine(inputPath, "Release", "ScrapMechanic.exe")))
                    {
                        gamepath = inputPath;
                        Sonstiges.DebugText("Spielpfad durch Nutzereingabe gefunden: " + gamepath);
                        break;
                    }
                    else
                    {
                        Console.WriteLine(String.Format(Localization.T("wrong.folder.input"), tries));
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
                            Sonstiges.Filehelper.Copy(inputpath, modfiles, true, Directory.GetFiles(inputpath, "*.*", SearchOption.AllDirectories), true);
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
            File.WriteAllLines(Path.Combine(modnamepath, modlistsfile), [.. modslist]);
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
            Sonstiges.DebugText("Starte Wechsel des aktiven Mods...");
            string activemod = GetCurrentMod(); //gibt den aktuell aktiven mod zurück, oder "Vanilla" wenn kein mod aktiv ist
            string option = Picker("mods.menu.change.active.mod.prompt", modspath, activemod, true); //gibt den ausgewählten mod zurück
            Sonstiges.DebugText($"Activemod: {activemod} ... Option: {option}");
            if (string.IsNullOrEmpty(option)||option == "exit")
                return; 
            else if (option == "no enries")
            {
                Console.WriteLine(Localization.T("mods.menu.no.mods"));
                Menu.WaitForKeypress();
                return;
            }
            else
            {
                if (option == "Vanilla" & activemod != "Vanilla") //vanilla restore
                {
                    ModToVanilla(activemod);
                    try
                    {
                        SetCurrentMod("Vanilla");
                    }
                    catch (Exception ex)
                    {
                        WriteLogAndExit(10, ex.Message); //write file error
                    }
                }
                else
                {
                    if (activemod == "Vanilla")
                    {
                        VanillaToMod(option);
                    }
                    else
                    {
                        ModToVanilla(activemod);
                        VanillaToMod(option);
                    }
                    SetCurrentMod(option);
                }
            }
            Menu.WaitForKeypress();
            return;
        }
        internal static void UpdateMod() // menu option
        {
            //string activetmod = GetCurrentMod();
            //string option = Picker("mods.menu.update.mod.prompt", modspath, activetmod, false, true, true);
            //if (string.IsNullOrEmpty(option) || option == "exit")
            //    return;
            //else if (option == "no enries")
            //{
            //    Console.WriteLine(Localization.T("mods.menu.no.mods"));
            //}
            //else
            //{
            //    if (option == "all")
            //    {
            //        string[] mods = Directory.GetDirectories(modspath);
            //        foreach (string mod in mods)
            //        {
            //            string modname = Path.GetRelativePath(modspath, mod);
            //            Console.WriteLine(string.Format(Localization.T("mods.menu.update.mod.progress"), modname));
            //            ModToVanilla(modname);
            //            VanillaToMod(modname);
            //            Console.WriteLine(string.Format(Localization.T("mods.menu.update.mod.done"), modname));
            //        }
            //        Console.WriteLine(Localization.T("mods.menu.update.mod.all.finished"));
            //    }
            //    else
            //    {
            //        Console.WriteLine(string.Format(Localization.T("mods.menu.update.mod.progress"), option));
            //        ModToVanilla(option);
            //        VanillaToMod(option);
            //        Console.WriteLine(string.Format(Localization.T("mods.menu.update.mod.done"), option));
            //    }















            //}
            Menu.WaitForKeypress();
            return;
        }
        internal static void RenameMod() // menu option
        {
            string activemod = GetCurrentMod();
            string option = Picker("mods.menu.rename.mod.prompt", modspath, activemod, false, false, true);
            string? newname = string.Empty;
            if (string.IsNullOrEmpty(option) || option == "exit") //Abfrage, ob gültige Option ausgewählt wurde, oder ob Nutzer zurückgehen wollte
                return;
            else if (option == "no enries") // Abfrage, ob überhaupt Mods zum umbenennen vorhanden sind
            {
                Console.WriteLine(Localization.T("mods.menu.no.mods"));
                Menu.WaitForKeypress();
                return;
            }
            else
            {
                Console.WriteLine(Localization.T("mods.menu.rename.mod.name.prompt"));
                Console.WriteLine(Localization.T("menu.go.back.q"));
                bool validname = false;
                while (validname == false) // Eingabeschleife für neuen Modnamen, prüft auf ungültige Zeichen, leeren Namen, "Vanilla" und bestehende Modnamen. Abbruch mit Q möglich.
                {
                    Console.Write(Localization.T("pointer"));
                    newname = Console.ReadLine();
                    if (newname != null)
                    {
                        switch (newname)
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
                                if (Directory.Exists($@"{modspath}\{newname}"))
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
                Sonstiges.DebugText("Neuer Modname akzeptiert: " + newname);
                try
                {
                    if (newname != null)
                        Directory.Move(Path.Combine(modspath, option), Path.Combine(modspath, newname));
                    else
                        WriteLogAndExit(11, "Neuer Modname ist null trotz prüfung."); 
                    Console.WriteLine(string.Format(Localization.T("mods.menu.rename.mod.done"), option, newname));
                }
                catch (Exception ex)
                { WriteLogAndExit(11, ex.Message); } //Fehler beim umbenennen des Modordners
                if (activemod != "Vanilla")
                {
                    SetCurrentMod(newname!);
                }
            }
        }
        internal static void RemoveMod() // menu option
        {
            Sonstiges.DebugText("Starte Entfernen eines Mods...");
            string activemod = GetCurrentMod();
            string option = Picker("mods.menu.remove.mod.prompt",modspath,activemod,false,true,true);
            Sonstiges.DebugText($"Activemod: {activemod} ... Option: {option}");
            if (string.IsNullOrEmpty(option)||option=="exit")
                return;
            else
            {
                if (option == "all")
                { 
                    if (Menu.YesOrNoPrompt(Localization.T("mods.menu.remove.mod.all.warn")))
                    {
                        if (activemod != "Vanilla")
                        {
                            ModToVanilla(activemod);
                            SetCurrentMod("Vanilla");
                        }
                        string[] mods = Directory.GetDirectories(modspath);
                        foreach (string mod in mods)
                        {
                            Console.WriteLine(string.Format(Localization.T("mods.menu.remove.mod.progress"),Path.GetRelativePath(modspath,mod)));
                            Sonstiges.Filehelper.DeleteDirectory(mod, true);
                            Console.WriteLine(string.Format(Localization.T("mods.menu.remove.mod.done"), Path.GetRelativePath(modspath, mod)));
                        }
                        Console.WriteLine(Localization.T("mods.menu.remove.mod.all.finished"));
                        Menu.WaitForKeypress();
                        return;
                    }
                    else
                    {
                        Menu.WaitForKeypress();
                        return;
                    }
                }
                else
                {
                    if (option == activemod)

                    {
                        if (Menu.YesOrNoPrompt(Localization.T("mods.menu.remove.mod.active.warn")))
                        {
                            ModToVanilla(activemod);
                            SetCurrentMod("Vanilla");
                        }
                        else
                        {
                            Menu.WaitForKeypress();
                            return;
                        }
                    }
                    string modpath = Path.Combine(modspath, option);
                    Console.WriteLine(string.Format(Localization.T("mods.menu.remove.mod.progress"), option));
                    Sonstiges.Filehelper.DeleteDirectory(modpath, true);
                    Console.WriteLine(string.Format(Localization.T("mods.menu.remove.mod.done"), option));
                    Menu.WaitForKeypress();
                    return;
                }
            }
        }
        static void VanillaToMod(string modname)
        {

            Sonstiges.DebugText($"Vanilla To Mod. Modname: {modname}");
            string modpath = Path.Combine(modspath, modname);
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
            if (modpath != null && Path.Exists(modpath))
            {
                Console.WriteLine(Localization.T("mods.menu.modtovanilla.start"));
                Sonstiges.DebugText($"Lese Datei: {Path.Combine(modpath, contentsfile)}");
                string[] modfiles = File.ReadAllLines(Path.Combine(modpath, contentsfile)); //relative paths
                Sonstiges.DebugText($"Lese Datei: {Path.Combine(vanillapath, contentsfile)}");
                string[] vanillafiles = File.ReadAllLines(Path.Combine(vanillapath, contentsfile)); //relative paths
                Sonstiges.Filehelper.Move(gamepath, Path.Combine(modpath, "files"), true, modfiles, true, true);
                Console.WriteLine(Localization.T("mods.menu.modtovanilla.moduninstall.done"));
                Sonstiges.Filehelper.Move(Path.Combine(vanillapath, "files"), gamepath, true, vanillafiles, true, true);
                Console.WriteLine(Localization.T("mods.menu.modtovanilla.vanillarecovery.done"));
            }
        }
        /// <summary>
        /// Gibt dem Nutzer eine Liste an Optionen zurück, die sich im angegebenen Verzeichnis befinden. Je nach Parametern können auch eine "Vanilla" und eine "All" Option angezeigt werden. Die "Vanilla" Option ermöglicht es, zum Vanilla Zustand zurückzukehren, während die "All" Option alle verfügbaren Mods auswählt. Der aktuell aktive Mod wird ebenfalls berücksichtigt, um zu verhindern, dass er erneut ausgewählt wird. Der Nutzer kann auch jederzeit mit "Q" abbrechen und zum vorherigen Menü zurückkehren.
        /// </summary>
        /// <param name="question"></param> 
        /// <param name="directory"></param>
        /// <param name="activeoption"></param>
        /// <param name="allowvanilla"></param>
        /// <param name="allowall"></param>
        /// <returns>Name der Option, die gewählt werden kann, Exit, All, no entries wenn das Verzeichnis nicht existiert oder Leer ist</returns>
        internal static string Picker(string question, string directory, string? activeoption = null, bool allowvanilla = false, bool allowall = false, bool allowsame = false)
        {
            Sonstiges.DebugText("Picker active");
            string option = string.Empty;
            if (!Directory.Exists(directory))
            {
                return "no entries"; //keine einträge vorhanden, da Ordner nicht existiert
            }

            string[] dirs = Directory.GetDirectories(directory); //full paths
            if (dirs.Length == 0)
            {
                return "no entries"; //keine einträge vorhanden, da Ordner leer ist
            }

            Console.WriteLine("\n" + Localization.T(question)); //Frage, welcher Mod zurückgegeben werden soll
            if (activeoption != "Vanilla" & allowvanilla)
                Console.WriteLine("0: " + Localization.T("modspath.menu.disable.mods")); //vanilla option, only if a mod is active and vanilla is allowed
            for (int i = 0; i < dirs.Length; i++)
            {
                dirs[i] = Path.GetRelativePath(modspath, dirs[i]); //only the name of the mod
                Console.WriteLine($"{i + 1}: " + dirs[i]);
            }
            if (allowall)
                Console.WriteLine(Localization.T("picker.all.a")); ; //all option, only if allowed
            Console.WriteLine(Localization.T("menu.go.back.q"));
            while (true)
            {
                Console.Write(Localization.T("pointer"));
                string input = Console.ReadLine()?.Trim() ?? "";

                if (input.Equals("Q", StringComparison.OrdinalIgnoreCase)) // user wants to go back/cancel
                {
                    Console.WriteLine(Localization.T("picker.canceled"));
                    Menu.WaitForKeypress();
                    return "exit";
                }
                else if (input.Equals("A", StringComparison.OrdinalIgnoreCase)) // all option, only if allowed
                {
                    if (allowall)
                    {
                        return "all";
                    }
                    else
                    {
                        Console.WriteLine(Localization.T("menu.wrong.keypress"));
                        continue;
                    }
                }
                
                if (int.TryParse(input, out int index) && index >= 0 && index <= dirs.Length) // valid index
                {
                    if (index == 0 && activeoption != "Vanilla" && allowvanilla) // vanilla option selected
                    {
                        option = "Vanilla";
                    }
                    else if (index == 0) // vanilla option selected but not allowed or already active
                    {
                        Console.WriteLine(Localization.T("menu.wrong.keypress"));
                        continue; // ask for input again
                    }
                    else // mod option selected
                    {
                        option = dirs[index - 1];
                    }

                    if (option == activeoption && allowsame == false) // selected option is already active, and selecting the same option is not allowed
                    {
                        Console.WriteLine(Localization.T("picker.option.same"));
                        continue; // ask for input again
                    }

                    break; // valid option selected, exit loop
                }
                else // invalid input
                {
                    Console.WriteLine(Localization.T("menu.wrong.keypress"));
                }
            }
            Sonstiges.DebugText($"Modpicker return: {option}");
            return option; //returns the selected option
        }
    }
}
