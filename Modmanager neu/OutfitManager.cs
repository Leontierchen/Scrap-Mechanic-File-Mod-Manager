using System.Runtime.Versioning;
using static Modmanager_neu.Program;

namespace Modmanager_neu
{
    [SupportedOSPlatform("windows")]
    internal class OutfitManager
    {
        public static readonly string outfitpath = Path.Combine(Modtool.gamepath, Modtool.modtool, "outfits");
        public static string outfitfile = Path.Combine(outfitpath, "active.txt");
        public static string GetCurrentOutfit()
        {
            Sonstiges.DebugText("Versuche aktuelles Outfit zu laden...");
            string[]? outfits;
            if (!File.Exists(outfitfile))
                return Localization.T("outfit.manager.no.outfits");
            else
            {
                outfits = File.ReadAllLines(outfitfile);
                if (outfits.Length > 0)
                {
                    return outfits[1];
                }
                else
                {
                    return Localization.T("outfit.manager.no.outfits");
                }
            }
        }
        public static void SaveOutfit()
        {
            Sonstiges.DebugText("Starte Outfit Speichern...");
            IO.ShowMessage("outfit.manager.name.promt");
            bool validname = false;
            string? outfitname = null;
            while (validname == false)
            {
                outfitname = IO.Handleinput(q: true);
                if (outfitname != null)
                {
                    switch (outfitname)
                    {
                        case "Q" or "q":
                            Sonstiges.DebugText("Modnamen Eingabe abgebrochen durch Nutzer.");
                            return;
                        case "":
                            IO.ShowMessage("outfit.manager.name.invalid");
                            break;
                        case string s when s.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0:
                            IO.ShowMessage("outfit.manager.name.invalid");
                            break;
                        default:
                            if (Directory.Exists(Path.Combine(outfitpath, outfitname)))
                            {
                                IO.ShowMessage("outfit.manager.name.exists");
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
                    IO.ShowMessage("outfit.manager.name.invalid");
                }
            }
            if (validname && !string.IsNullOrEmpty(outfitname))
            {
                string newOutfitDir = Path.Combine(outfitpath, outfitname);
                try
                {
                    Directory.CreateDirectory(newOutfitDir);
                }
                catch (Exception ex) 
                {
                    WriteLogAndExit(8, ex.Message); // Fehler beim Erstellen des Verzeichnisses
                    return;
                }
                try 
                {
                    File.Copy(Path.Combine(BackupManager.steamuser, "character"), Path.Combine(newOutfitDir, "character"), true);
                }
                catch (Exception ex)
                {
                    WriteLogAndExit(6, ex.Message); // Fehler beim Kopieren einer Datei
                    return;
                }
                File.WriteAllText(outfitfile, $"Aktives Outfit:\n{outfitname}");
                IO.ShowMessage("outfit.manager.saved", [outfitname]);
                IO.WaitForKeypress();
                return;
            }
        }
        public static void LoadOutfit()
        {
            Sonstiges.DebugText("Starte Outfit Laden...");
            string currentOutfit = GetCurrentOutfit();
            string outfitToLoad = IO.Picker("outfit.manager.load.prompt", outfitpath, currentOutfit);
            if (string.IsNullOrEmpty(outfitToLoad) || outfitToLoad == "exit")
                return;
            else if (outfitToLoad == "no enries")
                IO.ShowMessage("outfit.manager.no.outfits");
            else
            {                 
                try
                {
                    File.Copy(Path.Combine(outfitpath, outfitToLoad, "character"), Path.Combine(BackupManager.steamuser, "character"), true);
                }
                catch (Exception ex)
                {
                    WriteLogAndExit(6, ex.Message); // Fehler beim Kopieren einer Datei
                    return;
                }
                File.WriteAllText(outfitfile, $"Aktives Outfit:\n{outfitToLoad}");
                IO.ShowMessage("outfit.manager.loaded", [outfitToLoad]);
                IO.WaitForKeypress();
                return;
            }
        }
        public static void DeleteOutfit()
        {
            string currentOutfit = GetCurrentOutfit();
            if (currentOutfit == Localization.T("outfit.manager.no.outfits"))
            {
                IO.ShowMessage("outfit.manager.no.outfits");
                IO.WaitForKeypress();
                return;
            }
            string outfitTodelete = IO.Picker("outfit.manager.delete.prompt", outfitpath,allowsame: true);
            if (string.IsNullOrEmpty(outfitTodelete) || outfitTodelete == "exit")
                return;
            else if (outfitTodelete == "no enries")
                IO.ShowMessage("outfit.manager.no.outfits");
            else
            {
                Sonstiges.Filehelper.DeleteDirectory(Path.Combine(outfitpath, outfitTodelete));
                File.WriteAllText(outfitfile, $"Aktives Outfit:\n{Localization.T("outfit.manager.no.selection")}");
                IO.ShowMessage("outfit.manager.deleted", [outfitTodelete]);
                IO.WaitForKeypress();
                return;
            }
        }
    }
}
