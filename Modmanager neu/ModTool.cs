using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using static Modmanager_neu.Program;

namespace Modmanager_neu
{
    [SupportedOSPlatform("windows")]
    internal class Modtool
    {
        public static readonly string gamepath = GetGamePath();
        public static readonly string modtool = "Mod_manager_by_leon"; // relativ zum Spielordner
        public static readonly string modversionfile = "version.txt";
        public static readonly string modspath = Path.Combine(gamepath, modtool, "mods");
        public static readonly string vanillapath = Path.Combine(gamepath, modtool, "vanilla");
        public static readonly string contentsfile = "contents.txt";
        public static readonly string modlistsfile = "modlist.txt";
        public static readonly string sourcesigfile = "sourcesig.txt";

        public static readonly string defaultmodspath = Path.Combine(gamepath, modtool, "default_mods");
        
        public static string GetGamePath()
        {
            Sonstiges.DebugText("Starte Ermittlung des Spielpfads...");

            string gamepath = string.Empty;
            var folders = SteamLibraryFinder.GetAllSteamGameFolders();

            if (folders != null)
            {
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
            }
            if (string.IsNullOrEmpty(gamepath))
            {
                Sonstiges.DebugText("Spielpfad nicht gefunden. Nutzereingabe");
                IO.ShowMessage("gamepath.notfound");
                int tries = 3;
                while (tries > 0)
                {
                    IO.ShowMessage("gamepath.promt");
                    string? inputPath = IO.Handleinput();
                    if (inputPath != null && Directory.Exists(inputPath) && File.Exists(Path.Combine(inputPath, "Release", "ScrapMechanic.exe")))
                    {
                        gamepath = inputPath;
                        Sonstiges.DebugText("Spielpfad durch Nutzereingabe gefunden: " + gamepath);
                        break;
                    }
                    else
                    {
                        IO.ShowMessage("wrong.folder.input", [tries.ToString()]);
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
        internal static void AddMod(bool update = false, string updatename = "", string[]? updatepaths = null) // menu option
        {
            Sonstiges.DebugText("Starte hinzufügen einer neuen Mod...");
            string? modname = string.Empty;
            List<string> modslist = [];
            if (update == false)
            {
                IO.ShowMessage("mods.menu.new.mod.description");
                IO.ShowMessage("mods.menu.new.mod.name.prompt");
                bool validname = false;
                while (validname == false)
                {
                    modname = IO.Handleinput(q: true);
                    if (modname != null)
                    {
                        switch (modname)
                        {
                            case "Q" or "q":
                                Sonstiges.DebugText("Modnamen Eingabe abgebrochen durch Nutzer.");
                                return;
                            case "Vanilla":
                                IO.ShowMessage("mods.menu.new.mod.name.invalid");
                                break;
                            case "":
                                IO.ShowMessage("mods.menu.new.mod.name.empty");
                                break;
                            case string s when s.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0:
                                IO.ShowMessage("mods.menu.new.mod.name.invalid");
                                break;
                            default:
                                if (Directory.Exists($@"{modspath}\{modname}"))
                                {
                                    IO.ShowMessage("mods.menu.new.mod.name.exists");
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
                        IO.ShowMessage("mods.menu.new.mod.name.invalid");
                    }
                }
                Sonstiges.DebugText("Modname akzeptiert: " + modname);
            }
            else // Update eines bestehenden Mods, übergebe den alten Modnamen und die neuen Pfade, damit der Mod mit den neuen Pfaden neu erstellt wird.
            {
                if (string.IsNullOrEmpty(updatename) || updatepaths == null)
                    WriteLogAndExit(12, "Modname oder Modpfade für Update sind null oder leer."); //Fehler, wenn der Modname oder die Modpfade für ein Update nicht übergeben wurden
                else
                {
                    modname = updatename;
                    modslist = [.. updatepaths!];
                }
                Sonstiges.DebugText("Modupdate für Mod: " + modname);
            }
            if (update == false)
            {
                bool done = false;
                while (done == false)
                {
                    IO.ShowMessage("mods.menu.new.mod.path.prompt");

                    // Use Windows file/folder picker instead of console input
                    string[]? picked = PickFilesOrFolderDialog();
                    if (picked == null || picked.Length == 0)
                    {
                        Sonstiges.DebugText("Pfadauswahl abgebrochen durch Nutzer.");
                        IO.WaitForKeypress();
                        return;
                    }

                    foreach (var inputpath in picked)
                    {
                        if (Directory.Exists(inputpath) || File.Exists(inputpath))
                        {
                            IO.ShowMessage("mods.menu.new.mod.path.accepted", [inputpath]);
                            modslist.Add(inputpath);
                        }
                        else
                        {
                            IO.ShowMessage("mods.menu.new.mod.path.invalid");
                        }
                    }

                    if (IO.YesOrNoPrompt(Localization.T("mods.menu.new.mod.add.more.mods.question")) == false)
                    {
                        Sonstiges.DebugText("Mods hinzufügen fertig");
                        done = true;
                    }
                    else { Sonstiges.DebugText("noch eine mod hinzufügen, fortsetzen"); }
                }
            }

            Sonstiges.DebugText("Erstelle Directory.");
            string modnamepath = $@"{modspath}\{modname}";
            string modfiles = $@"{modnamepath}\files";
            try
            { Directory.CreateDirectory(modfiles); }
            catch (Exception ex)
            { WriteLogAndExit(8, ex.Message); } // Fehler beim Erstellen des Modverzeichnisses, wahrscheinlich ungültige Zeichen im Modnamen, obwohl vorher geprüft.
            foreach (string item in modslist)
            {
                if (File.Exists(item))
                {
                    IO.ShowMessage("mods.menu.new.mod.extract.zip");
                    try
                    { ZipFile.ExtractToDirectory(item, modfiles, true); }
                    catch (Exception ex)
                    { WriteLogAndExit(9, ex.Message); } //Fehler beim extrahieren der zip
                    IO.ShowMessage("mods.menu.new.mod.extract.zip.done");
                }
                else if (Directory.Exists(item))
                {
                    IO.ShowMessage("mods.menu.new.mod.extract.directory");
                    Sonstiges.Filehelper.Copy(item, modfiles, true, Directory.GetFiles(item, "*.*", SearchOption.AllDirectories), true);
                    IO.ShowMessage("mods.menu.new.mod.extract.directory.done");
                }
            }
            //document all mods and files into text files inside the mod folder
            File.WriteAllLines(Path.Combine(modnamepath, modlistsfile), [.. modslist]);

            // New: create source signatures for all entries in modslist
            try
            {
                List<string> sourcesigs = [];
                foreach (string src in modslist)
                {
                    string sig = string.Empty;
                    try
                    {
                        if (File.Exists(src))
                            sig = ComputeFileHash(src);
                        else if (Directory.Exists(src))
                            sig = ComputeDirectoryHash(src);
                        else
                            sig = "MISSING";
                    }
                    catch { sig = "ERROR"; }
                    sourcesigs.Add($"{src}|{sig}");
                }
                File.WriteAllLines(Path.Combine(modnamepath, sourcesigfile), [.. sourcesigs]);
            }
            catch (Exception ex)
            {
                // don't fail the whole operation for signature errors, but log and continue
                Sonstiges.DebugText("Warning: Could not write source signatures: " + ex.Message);
            }

            string[] files = Directory.GetFiles(modfiles, ".", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                files[i] = (Path.GetRelativePath(modfiles, files[i]));
            }

            File.WriteAllLines(Path.Combine(modnamepath, contentsfile), files);
            if (update == false)
            {
                IO.ShowMessage("mods.menu.new.mod.finished", [modname!, Localization.TArray("mods.menu.options")[0]]);
                IO.WaitForKeypress();
            }
        }
        internal static void SwitchMod() // menu option
        {
            Sonstiges.DebugText("Starte Wechsel des aktiven Mods...");
            string? activemod = states.Activemod; //gibt der aktuell aktiven mod zurück, oder "Vanilla" wenn kein mod aktiv ist
            string option = IO.Picker("mods.menu.change.active.mod.prompt", modspath, activemod, true); //gibt der ausgewählten mod zurück
            Sonstiges.DebugText($"Activemod: {activemod} ... Option: {option}");
            if (string.IsNullOrEmpty(option) || option == "exit")
                return;
            else if (option == "no entries")
            {
                IO.ShowMessage("mods.menu.no.mods");
                IO.WaitForKeypress();
                return;
            }
            else
            {
                if (config.UseDefaultMods)
                    UnloadDefaultMods();
                if (config.AutoCheckForUpdates)
                {
                    Sonstiges.DebugText("Auto-Update Check aktiviert. Prüfe auf Änderungen in den Modquellen...");
                    try
                    {
                        CheckAndOfferUpdate(option);
                    }
                    catch (Exception ex)
                    {
                        Sonstiges.DebugText("Update-Check fehlgeschlagen: " + ex.Message);
                    }
                }

                if (option == "Vanilla" && activemod != "Vanilla") //vanilla restore
                {
                    ModToVanilla(activemod!);
                    try
                    {
                        states.Activemod="Vanilla";
                        SaveStates(states);
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
                        ModToVanilla(activemod!);
                        VanillaToMod(option);
                    }
                    states.Activemod = option;
                    SaveStates(states);
                }
                if (config.UseDefaultMods)
                    UnloadDefaultMods();
            }
            IO.WaitForKeypress();
            return;
        }
        internal static void UpdateMod(string? autoupdatename = null) // menu option
        {
            string? activemod = states.Activemod;
            string? option;
            if (autoupdatename == null)
                option = IO.Picker("mods.menu.update.prompt", modspath, activemod, false, false, true);
            else
                option = autoupdatename;
            if (string.IsNullOrEmpty(option) || option == "exit")
                return;
            else if (option == "no entries")
                IO.ShowMessage("mods.menu.no.mods");
            else
            {
                string[] modpaths = [];
                try
                {
                    modpaths = File.ReadAllLines(Path.Combine(modspath, option, modlistsfile));
                }
                catch (Exception ex)
                {
                    WriteLogAndExit(10, ex.Message); //read file error
                }
                if (modpaths.Length == 0) //Abfrage, ob überhaupt Pfade zum Mod hinterlegt wurden
                {
                    IO.ShowMessage("mods.menu.update.check.no.paths"); //wenn keine Pfade hinterlegt wurden, kann nicht aktualisiert werden, ende hier
                    IO.WaitForKeypress();
                    return;
                }
                else
                {

                    IO.ShowMessage("mods.menu.update.progress.start", [option]);

                    List<string> updatepaths = [];
                    if (autoupdatename == null)
                    {
                        for (int i = 0; i < modpaths.Length; i++) //prüft alle pfade, die hinterlegt wurden
                        {
                            bool isvalid = false;
                            if (IO.YesOrNoPrompt(Localization.T("mods.menu.update.check.paths.prompt") + "\n--" + modpaths[i] + "\n")) //user sagt ja, pfad stimmt
                            {
                                if (!File.Exists(modpaths[i]) && !Directory.Exists(modpaths[i])) //pfad existiert aber nicht mehr
                                {
                                    IO.ShowMessage("mods.menu.update.check.pathnolongerexists");
                                }
                                else //pfad existiert noch, füge zur update liste hinzu
                                {
                                    isvalid = true;
                                    updatepaths.Add(modpaths[i]);
                                }
                            }
                            else //user sagt nein, pfad stimmt nicht
                            {
                                if (!IO.YesOrNoPrompt(Localization.T("mods.menu.update.check.asktofixpath"))) //fragen, ob der Pfad korrigiert werden soll
                                    isvalid = true; //nein, entferne ihn
                            }
                            if (isvalid == false) //pfad muss aktualisiert werden
                            {
                                bool validpath = false;
                                while (validpath == false)
                                {
                                    IO.ShowMessage("mods.menu.new.mod.path.prompt");
                                    string? inputpath = IO.Handleinput(q: true);
                                    if (inputpath != null)
                                    {
                                        Console.WriteLine();
                                        switch (inputpath)
                                        {
                                            case "Q" or "q":
                                                Sonstiges.DebugText("Modnamen Eingabe abgebrochen durch Nutzer."); //eingabe abgebrochen, kein pfad wird in update liste geschrieben
                                                return;
                                            case "":
                                                IO.ShowMessage("mods.menu.new.mod.path.empty");
                                                break;
                                            case string s when s.IndexOfAny(Path.GetInvalidPathChars()) >= 0:
                                                IO.ShowMessage("mods.menu.new.mod.path.invalid");
                                                break;
                                            default:
                                                if (Directory.Exists(inputpath) || File.Exists(inputpath))
                                                {
                                                    validpath = true; // eingabe ok, füge zur liste hinzu
                                                    updatepaths.Add(inputpath);
                                                    break;
                                                }
                                                else
                                                {
                                                    IO.ShowMessage("mods.menu.new.mod.path.invalid"); //eingabe ungültig, neu versuchen
                                                    break;
                                                }
                                        }
                                    }
                                    else
                                        IO.ShowMessage("mods.menu.new.mod.path.invalid");
                                }
                            }
                        }
                        IO.ShowMessage("mods.menu.update.check.paths.done");
                        foreach (string item in updatepaths)
                        {
                            Console.WriteLine("-- " + item);
                        }
                    }
                    else
                        updatepaths = [.. modpaths]; //bei autoupdate werden die alten Pfade automatisch übernommen, da ja nur geprüft werden soll, ob sie noch gültig sind oder aktualisiert werden müssen.

                    if (autoupdatename != null || IO.YesOrNoPrompt(Localization.T("mods.menu.update.confirm")))// alle pfade geprüft, bestätigung bevor fortgefahren wird
                    {
                        Sonstiges.DebugText("Alle Pfade geprüft. Starte Update...");

                        IO.ShowMessage("mods.menu.update.progress.running");
                        if (option == activemod) //wenn der zu aktualisierende Mod an ist, wird er zuerst deinstalliert und der Vanilla zustand wiederhergestellt
                        {
                            IO.ShowMessage("mods.menu.update.recover.vanilla");
                            ModToVanilla(option);
                            IO.ShowMessage("mods.menu.update.recover.vanilla.done");
                        }
                        Sonstiges.Filehelper.DeleteDirectory(Path.Combine(modspath, option), true); // altes Modverzeichnis löschen
                        AddMod(true, option, [.. updatepaths]); //neuen Mod mit den aktualisierten Pfaden hinzufügen, dabei wird der alte Modname und die neuen Pfade übergeben. Clean install
                        if (option == activemod) //wenn der zu aktualisierende Mod an ist, wird er nach dem Update wieder aktiviert
                        {
                            IO.ShowMessage("mods.menu.update.reactivate.mod");
                            VanillaToMod(option);
                            IO.ShowMessage("mods.menu.update.reactivate.mod.done");
                        }

                        IO.ShowMessage("mods.menu.update.progress.finished", [option]);

                    }
                }
            }
            IO.WaitForKeypress();
            return;
        }
        internal static void RenameMod() // menu option
        {
            string? activemod = states.Activemod;
            string option = IO.Picker("mods.menu.rename.mod.prompt", modspath, activemod, false, false, true);
            string? newname = string.Empty;
            if (string.IsNullOrEmpty(option) || option == "exit") //Abfrage, ob gültige Option ausgewählt wurde, oder ob Nutzer zurückgehen wollte
                return;
            else if (option == "no entries") // Abfrage, ob überhaupt Mods zum umbenennen vorhanden sind
            {
                IO.ShowMessage("mods.menu.no.mods");
                IO.WaitForKeypress();
                return;
            }
            else
            {
                IO.ShowMessage("mods.menu.rename.mod.name.prompt");
                bool validname = false;
                while (validname == false) // Eingabeschleife für neuen Modnamen, prüft auf ungültige Zeichen, leeren Namen, "Vanilla" und bestehende Modnamen. Abbruch mit Q möglich.
                {
                    newname = IO.Handleinput(q: true);
                    if (newname != null)
                    {
                        switch (newname)
                        {
                            case "Q" or "q":
                                Sonstiges.DebugText("Modnamen Eingabe abgebrochen durch Nutzer.");
                                return;
                            case "Vanilla":
                                IO.ShowMessage("mods.menu.new.mod.name.invalid");
                                break;
                            case "":
                                IO.ShowMessage("mods.menu.new.mod.name.empty");
                                break;
                            case string s when s.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0:
                                IO.ShowMessage("mods.menu.new.mod.name.invalid");
                                break;
                            default:
                                if (Directory.Exists($@"{modspath}\{newname}"))
                                {
                                    IO.ShowMessage("mods.menu.new.mod.name.exists");
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
                        IO.ShowMessage("mods.menu.new.mod.name.invalid");
                    }
                }
                Sonstiges.DebugText("Neuer Modname akzeptiert: " + newname);
                try
                {
                    if (!String.IsNullOrEmpty(newname))
                    {
                        Directory.Move(Path.Combine(modspath, option), Path.Combine(modspath, newname));
                        IO.ShowMessage("mods.menu.rename.mod.done", [option, newname]);
                    }
                    else
                        WriteLogAndExit(11, "Neuer Modname ist null trotz prüfung.");
                }
                catch (Exception ex)
                { WriteLogAndExit(11, ex.Message); } //Fehler beim umbenennen des Modordners
                if (activemod != "Vanilla")
                {
                    states.Activemod = newname!;
                    SaveStates(states);
                }
            }
        }
        internal static void RemoveMod() // menu option
        {
            Sonstiges.DebugText("Starte Entfernen eines Mods...");
            string? activemod = states.Activemod;
            string option = IO.Picker("mods.menu.remove.mod.prompt", modspath, activemod, false, true, true);
            Sonstiges.DebugText($"Activemod: {activemod} ... Option: {option}");
            if (string.IsNullOrEmpty(option) || option == "exit")
                return;
            else if (option == "no entries") // Abfrage, ob überhaupt Mods zum umbenennen vorhanden sind
            {
                IO.ShowMessage("mods.menu.no.mods");
                IO.WaitForKeypress();
                return;
            }
            else
            {
                if (option == "all")
                {
                    if (IO.YesOrNoPrompt(Localization.T("mods.menu.remove.mod.all.warn")))
                    {
                        if (activemod != "Vanilla")
                        {
                            ModToVanilla(activemod!); 
                            states.Activemod = "Vanilla";
                            SaveStates(states);
                        }
                        string[] mods = Directory.GetDirectories(modspath);
                        foreach (string mod in mods)
                        {
                            IO.ShowMessage("mods.menu.remove.mod.progress", [Path.GetRelativePath(modspath, mod)]);
                            Sonstiges.Filehelper.DeleteDirectory(mod, true);
                            IO.ShowMessage("mods.menu.remove.mod.done", [Path.GetRelativePath(modspath, mod)]);
                        }
                        IO.ShowMessage("mods.menu.remove.mod.all.finished");
                        IO.WaitForKeypress();
                        return;
                    }
                    else
                    {
                        IO.WaitForKeypress();
                        return;
                    }
                }
                else
                {
                    if (option == activemod)

                    {
                        if (IO.YesOrNoPrompt(Localization.T("mods.menu.remove.mod.active.warn")))
                        {
                            ModToVanilla(activemod);
                            states.Activemod = "Vanilla";
                            SaveStates(states);
                        }
                        else
                        {
                            IO.WaitForKeypress();
                            return;
                        }
                    }
                    string modpath = Path.Combine(modspath, option);
                    IO.ShowMessage("mods.menu.remove.mod.progress", [option]);
                    Sonstiges.Filehelper.DeleteDirectory(modpath, true);
                    IO.ShowMessage("mods.menu.remove.mod.done", [option]);
                    IO.WaitForKeypress();
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
            IO.ShowMessage("mods.menu.vanillatomod.start");
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
            Sonstiges.Filehelper.Move(
                source: gamepath,
                target: Path.Combine(vanillapath, "files"),
                files: [.. vanillafiles],
                combine: true,
                useProgressbar: true
                );
            IO.ShowMessage("mods.menu.vanillatomod.vanillasaved");
            //mod install
            Sonstiges.Filehelper.Move(
                source: Path.Combine(modpath, "files"),
                target: gamepath,
                files: modfiles,
                combine: true,
                useProgressbar: true
                );
            IO.ShowMessage("mods.menu.vanillatomod.done");
        }
        static void ModToVanilla(string currentmod)
        {
            Sonstiges.DebugText("Mod To Vanilla");
            //mod uninstall
            string? modpath = Path.Combine(modspath, currentmod);
            if (modpath != null && Path.Exists(modpath))
            {
                IO.ShowMessage("mods.menu.modtovanilla.start");
                Sonstiges.DebugText($"Lese Datei: {Path.Combine(modpath, contentsfile)}");
                string[] modfiles = File.ReadAllLines(Path.Combine(modpath, contentsfile)); //relative paths
                Sonstiges.DebugText($"Lese Datei: {Path.Combine(vanillapath, contentsfile)}");
                string[] vanillafiles = File.ReadAllLines(Path.Combine(vanillapath, contentsfile)); //relative paths
                Sonstiges.Filehelper.Move(gamepath, Path.Combine(modpath, "files"), true, modfiles, true, true);
                IO.ShowMessage("mods.menu.modtovanilla.moduninstall.done");
                Sonstiges.Filehelper.Move(Path.Combine(vanillapath, "files"), gamepath, true, vanillafiles, true, true);
                IO.ShowMessage("mods.menu.modtovanilla.vanillarecovery.done");
            }
        }
        static string ComputeFileHash(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            var hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
        static string ComputeDirectoryHash(string dir)
        {
            var files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            using var sha = SHA256.Create();
            foreach (var f in files)
            {
                // incorporate relative path to detect structure changes
                var rel = Path.GetRelativePath(dir, f).Replace(Path.DirectorySeparatorChar, '/');
                var relBytes = System.Text.Encoding.UTF8.GetBytes(rel);
                sha.TransformBlock(relBytes, 0, relBytes.Length, relBytes, 0);

                using var stream = File.OpenRead(f);
                var buffer = new byte[8192];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    sha.TransformBlock(buffer, 0, read, buffer, 0);
                }
            }
            // finalize
            sha.TransformFinalBlock([], 0, 0);
            return BitConverter.ToString(sha.Hash!).Replace("-", "").ToLowerInvariant();
        }
        static void CheckAndOfferUpdate(string modname)
        {
            string modpath = Path.Combine(modspath, modname);
            string sigpath = Path.Combine(modpath, sourcesigfile);
            if (!File.Exists(sigpath))
            {
                Sonstiges.DebugText("Auto Update: No source signature found for mod: " + modname);
                return;
            }

            var lines = File.ReadAllLines(sigpath);
            List<string> changed = [];
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                var parts = line.Split('|', 2);
                if (parts.Length != 2)
                    continue;
                var src = parts[0];
                var saved = parts[1];
                string current;
                try
                {
                    if (File.Exists(src))
                        current = ComputeFileHash(src);
                    else if (Directory.Exists(src))
                        current = ComputeDirectoryHash(src);
                    else
                        current = "MISSING";
                }
                catch
                {
                    current = "ERROR";
                }
                if (!string.Equals(saved, current, StringComparison.OrdinalIgnoreCase))
                    changed.Add(src);
            }

            if (changed.Count > 0)
            {
                IO.ShowMessage("mods.menu.update.available", [modname]);
                foreach (var c in changed)
                    Console.WriteLine("- " + c);

                if (IO.YesOrNoPrompt(Localization.T("mods.menu.autoupdate.prompt")))
                {
                    Sonstiges.DebugText($"User accepted autoupdate for mod {modname}");
                    UpdateMod(modname);
                }
                else
                {
                    Sonstiges.DebugText($"User declined autoupdate for mod {modname}");
                }
            }
            else
            {
                Sonstiges.DebugText("No changes detected for mod: " + modname);
            }
        }
        static string[]? PickFilesOrFolderDialog()
        {
            Sonstiges.DebugText("Öffne Datei/Ordner Auswahl Dialog...");
            string[]? result = null;
            var t = new Thread(() =>
            {
                try
                {
                    using (var fbd = new FolderBrowserDialog())
                    {
                        fbd.Description = "Select folder";
                        var dr2 = fbd.ShowDialog();
                        if (dr2 == DialogResult.OK)
                        {
                            result = [fbd.SelectedPath];
                            return;
                        }
                    }
                    if (result == null || result.Length == 0)
                    {
                        using var ofd = new OpenFileDialog();
                        ofd.Multiselect = true;
                        ofd.Filter = "Zip files (*.zip)|*.zip|All files|*.*";
                        var dr = ofd.ShowDialog();
                        if (dr == DialogResult.OK)
                        {
                            result = ofd.FileNames;
                            return;
                        }
                    }
                    // user cancelled both
                    result = [];
                }
                catch (Exception ex)
                {
                    Sonstiges.DebugText("Picker exception: " + ex.Message);
                    result = [];
                }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            if (result == null || result.Length == 0)
                return null;
            return result;
        }
        public static void OpenDefaultModsFolder()
        {
            Sonstiges.DebugText("Öffne Standard Mods Ordner...");
            if (!Directory.Exists(defaultmodspath))
            {
                Directory.CreateDirectory(Path.Combine(defaultmodspath, "mods"));
                Directory.CreateDirectory(Path.Combine(defaultmodspath, "recovery"));
            }
            Process.Start("explorer.exe", defaultmodspath);
        }
        public static void LoadDefaultMods()
        {
            if (states.Installeddefaultmods)
            {
                Sonstiges.DebugText("Standard Mods wurden bereits installiert, überspringe Installation");
                return;
            }
            else
            {
                string modspath = Path.Combine(defaultmodspath, "mods");
                string recoverypath = Path.Combine(defaultmodspath, "recovery");
                Sonstiges.DebugText("\n------ Lade Mods aus Standard Mods Ordner --------\n");
                if (!Directory.Exists(defaultmodspath))
                {
                    IO.ShowMessage("default.mods.cant.install");
                    Sonstiges.DebugText("Standard Mods Ordner existiert nicht, erstelle ihn und lade keine Mods");
                    Directory.CreateDirectory(modspath);
                    Directory.CreateDirectory(recoverypath);
                    return;
                }
                try
                {
                    //lese alle dateien in default_mods
                    string[] files = Directory.GetFiles(modspath, ".", SearchOption.AllDirectories);
                    if (files.Length == 0)
                    {

                        IO.ShowMessage("default.mods.cant.install");
                        Sonstiges.DebugText("Standard Mods Ordner enthält keine mods, lade keine Mods");
                        config.UseDefaultMods = false;
                        SaveConfig(config);
                        return;
                    }
                    for (int i = 0; i < files.Length; i++)
                    {
                        if (Path.GetFileName(files[i]) == "Readme.txt")
                        {
                            files[i] = string.Empty;
                            continue;
                        }
                        files[i] = Path.GetRelativePath(modspath, files[i]);
                    }

                    //schreibe diese dateien als relativpfad in die contents.txt im mods ordner
                    File.WriteAllLines(Path.Combine(defaultmodspath, "mod_contents.txt"), files);
                    //prüfe existenz dieser dateien im spielverzeichnis, wenn sie existieren, sichere sie im recovery ordner
                    List<string> vanillafiles = [];
                    foreach (string file in files)
                    {
                        if (!string.IsNullOrEmpty(file))
                        {
                            if (File.Exists(Path.Combine(gamepath, file)))
                                vanillafiles.Add(file);
                        }
                    }
                    //schreibe diese dateien als relativpfad in die contents.txt im recovery ordner
                    File.WriteAllLines(Path.Combine(defaultmodspath, "recovery_contents.txt"), [.. vanillafiles]);
                    //kopiere die dateien von spielpfad zu recovery
                    IO.ShowMessage("default.mods.installing");
                    Sonstiges.Filehelper.Move(
                        source: gamepath,
                        target: recoverypath,
                        files: [.. vanillafiles],
                        combine: true,
                        useProgressbar: true
                        );
                    //kopiere die dateien von mods zu spielpfad
                    Sonstiges.Filehelper.Copy(
                        source: modspath,
                        target: gamepath,
                        files: files,
                        combine: true,
                        useProgressbar: true
                        );
                    states.Installeddefaultmods = true;
                    SaveStates(states);
                    IO.ShowMessage("default.mods.installed");
                    return;
                }
                catch (Exception ex)
                {
                    Sonstiges.DebugText("Fehler beim Laden der Standard Mods: " + ex.Message);
                }
            }
        }
        public static void UnloadDefaultMods()
        {
            if (!states.Installeddefaultmods)
            {
                Sonstiges.DebugText("Standard Mods wurden nicht installiert, überspringe Wiederherstellung");
                return;
            }
            else
            {
                string recoverypath = Path.Combine(defaultmodspath, "recovery");
                Sonstiges.DebugText("\n------ Stelle Vanilla Zustand aus Standard Mods Ordner wieder her --------\n");
                if (!Directory.Exists(recoverypath))
                {
                    Sonstiges.DebugText("Recovery Ordner existiert nicht, kann Vanilla Zustand nicht wiederherstellen");
                    return;
                }
                try
                {
                    //lese alle dateien in recovery_contents.txt
                    string[] modfiles = File.ReadAllLines(Path.Combine(defaultmodspath, "mod_contents.txt")); //relative paths
                    foreach (string modfile in modfiles) //prüfe, ob diese dateien im spielverzeichnis existieren, wenn ja, lösche sie
                    {
                        File.Delete(modfile);
                    }
                    //lese alle dateien in recovery
                    string[] files = File.ReadAllLines(Path.Combine(defaultmodspath, "recovery_contents.txt"));
                    //kopiere die dateien von recovery zu spielpfad
                    IO.ShowMessage("default.mods.restoring");
                    Sonstiges.Filehelper.Move(
                        source: recoverypath,
                        target: gamepath,
                        files: files,
                        combine: true,
                        useProgressbar: true
                        );

                    states.Installeddefaultmods = false;
                    SaveStates(states);
                    IO.ShowMessage("default.mods.restored");
                    return;
                }
                catch (Exception ex)
                {
                    Sonstiges.DebugText("Fehler beim Wiederherstellen des Vanilla Zustands: " + ex.Message);
                }
            }
        }
    }
}
