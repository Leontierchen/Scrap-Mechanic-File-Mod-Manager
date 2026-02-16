using Modmanager_neu;
using System.Runtime.Versioning;
using static Modmanager_neu.Program;

[SupportedOSPlatform("windows")]
internal static class Menu
{
    public static readonly Stack<Action> MenuStack = new();

    public static void Start()
    {
        OpenMainMenu();
    }

    // -------------------------
    // MAIN MENU
    // -------------------------
    public static void OpenMainMenu()
    {
        MenuStack.Clear();
        ShowMenu(
            titleKey: "main.menu.title",
            subTextKey: "main.menu.subtext",
            subTextArg: "currentmod",
            optionsKey: "main.menu.options",
            actions:
            [
                OpenBackupMenu,
                OpenOutfitMenu,
                OpenModsMenu,
                OpenSettingsMenu,
                Sonstiges.Contacts,
                StartGame
            ]
        );
    }

    // -------------------------
    // BACKUP MENU
    // -------------------------
    public static void OpenBackupMenu()
    {

        PushMenu(() =>
            ShowMenu(
                "backup.menu.title",
                "backup.menu.subtext",
                "lastbackup",
                "backup.menu.options",
                [
                    BackupManager.CreateBackup,
                    BackupManager.LoadBackup,
                    BackupManager.DeleteBackup
                ]
            )
        );
    }
   
    // -------------------------
    // OUTFIT MENU
    // -------------------------
    public static void OpenOutfitMenu()
    {
        PushMenu(() =>
            ShowMenu(
                "outfit.menu.title",
                "outfit.menu.subtext",
                "currentoutfit",
                "outfit.menu.options",
                [
                    OutfitManager.SaveOutfit,
                    OutfitManager.LoadOutfit,
                    OutfitManager.DeleteOutfit
                ]
            )
        );
    }

    // -------------------------
    // MODS MENU
    // -------------------------
    public static void OpenModsMenu()
    {
        PushMenu(() =>
            ShowMenu(
                "mods.menu.title",
                "mods.menu.subtext",
                "currentmod",
                "mods.menu.options",
                [
                    Modtool.SwitchMod,
                    () => Modtool.AddMod(), // Lambda-Ausdruck verwendet
                    () => Modtool.UpdateMod(),
                    Modtool.RenameMod,
                    Modtool.RemoveMod
                ]
            )
        );
    }

    // -------------------------
    // SETTINGS MENU
    // -------------------------
    public static void OpenSettingsMenu()
    {
        PushMenu(() =>
           ShowMenu(
               "settings.menu.title",
               "setting.menu.subtext",
               "",
               "settings.menu.options",
               [
                   Settings.LanguageSelection,
                   Settings.Toggledebug
               ]
           )
       );
    }

    // -------------------------
    // MENU CORE
    // -------------------------
    public static void ShowMenu(string titleKey,string subTextKey,object? subTextArg,string optionsKey,Action[] actions)
    {
        int loopCount = 0; // logging loop counter
        while (true)
        {
            loopCount++; // Increment loop counter for logging purposes
            Console.WriteLine($"Menu Stack: {MenuStack.Count} | Loop Count: {loopCount}"); // Log info
            Sonstiges.DebugText("---------- Menü wird geladen----------");
            Console.Clear();
            if (langfallback)
                Console.WriteLine($"Language file for '{config.Language}' not found. Falling back to '{Masterlanguage}' as its the Master Language.");
            Console.WriteLine($"{Program.AppName} v{Program.AppVersion}\n");
            Console.WriteLine("-------------- " + Localization.T(titleKey)+ " --------------\n\n");

            string? sub = Convert.ToString(subTextArg);

            if (!string.IsNullOrEmpty(subTextKey))
            {
                if (!string.IsNullOrEmpty(sub))
                {
                    object? subTextArg2;
                    if (sub == "lastbackup")
                        subTextArg2 = BackupManager.GetLastBackupName();
                    else if (sub == "currentmod")
                        subTextArg2 = Modtool.GetCurrentMod();
                    else if (sub == "currentoutfit")
                        subTextArg2 = OutfitManager.GetCurrentOutfit();
                    else
                        subTextArg2 = subTextArg;

                    Console.WriteLine(string.Format(Localization.T(subTextKey), subTextArg2));
                }
                else
                    Console.WriteLine(Localization.T(subTextKey));
            }
            Console.WriteLine();

            var options = Localization.TArray(optionsKey);
            int emptyline = 0;
            for (int i = 0; i < options.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(options[i]))
                {
                    Console.WriteLine();
                    emptyline++;
                }
                else
                {
                    int a = i - emptyline;
                    Console.WriteLine($"{a + 1}) {options[i]}");
                }
            }
            if (MenuStack.Count > 0)
                IO.ShowMessage("menu.go.back.esc");
            else
                IO.ShowMessage("menu.exit.esc");


            bool abort = HandleMenuInput(actions,MenuStack.Count);
            if (abort)
                break;
        }
    }
    public static bool HandleMenuInput(Action[] actions, int menuCount)
    {
        while (true)
        {

            Console.Write(Localization.T("pointer"));
            var key = Console.ReadKey(true);
            
            // Zahlen 1–9
            if (char.IsDigit(key.KeyChar))
            {
                int index = key.KeyChar - '0';
                Console.Write(index+"\n");
                if (index >= 1 && index <= actions.Length)
                {
                    actions[index - 1].Invoke();
                    return false;
                }
            }

            // ESC = zurück
            if (key.Key == ConsoleKey.Escape)
           {
                Console.Write("ESC\n");
                if (menuCount > 0)
                    GoBack();
                else
                    ExitProgram();

                return true;
            }
            // Ungültige Taste 
            IO.ShowMessage("menu.wrong.keypress");
            //System.Threading.Thread.Sleep(1500);
        }
    }

    // -------------------------
    // NAVIGATION
    // -------------------------
    public static void PushMenu(Action menu)
    {
        MenuStack.Push(menu);
        menu();
    }

    public static void GoBack()
    {
        if (MenuStack.Count > 0)
        {
            MenuStack.Pop();
        }
    }

    // -------------------------
    // UTIL
    // -------------------------
    public static void ShowPlaceholder(string name)
    {
        Console.Clear();
        Console.WriteLine(name);
        IO.ShowMessage("placeholder");
        Console.WriteLine();
        IO.ShowMessage("request.keypress");
        Console.ReadKey();
    }

    public static void StartGame()
    {
        if (!IsDebug)
            System.Diagnostics.Process.Start("ScrapMechanicLaunch.bat");
        else
            Sonstiges.DebugText("Spiel starten (Debug Mode: Kein tatsächlicher Start)");
        WriteLogAndExit(0); // Beendet das Programm mit einem Rückgabewert von 0 (erfolgreich)
    }

    
    
    public static void ExitProgram()
    {
        IO.ShowMessage("program.exit");
    }
    
}
