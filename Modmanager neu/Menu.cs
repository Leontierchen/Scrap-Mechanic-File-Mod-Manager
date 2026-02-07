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
                OpenContactMenu,
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
                OutfitManager.GetCurrentOutfit(),
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
                    Modtool.AddMod,
                    Modtool.UpdateMod,
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

    public static void OpenContactMenu()
    {
        ShowPlaceholder("Kontakt");
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
                Console.WriteLine(Localization.T("menu.go.back.esc"));
            else
                Console.WriteLine(Localization.T("menu.exit.esc"));


            bool abort = HandleInput(actions,MenuStack.Count);
            if (abort)
                break;
        }
    }
    public static bool HandleInput(Action[] actions, int menuCount)
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
            Console.WriteLine(Localization.T("menu.wrong.keypress"));
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
        Console.WriteLine(Localization.T("placeholder"));
        Console.WriteLine();
        Console.WriteLine(Localization.T("request.keypress"));
        Console.ReadKey();
    }

    public static void StartGame()
    {
        if (!IsDebug)
            System.Diagnostics.Process.Start("ScrapMechanicLaunch.bat");
    }

    /// <summary>
    /// Stellt dem Nutzer eine Frage, die mit Ja oder nein beantwortet werden muss.
    /// </summary>
    /// <param name="question">Fragetext als String</param>
    /// <returns>Ja -> true, Nein -> false</returns>
    public static bool YesOrNoPrompt(string question)
    {
        Console.WriteLine($"{question} (Y/J or N )");
        Localization.T("pointer");
        while (true)
        {
            var key = Console.ReadKey(true).Key;

            if (key == ConsoleKey.Y || key == ConsoleKey.J)
                return true;
            else if (key == ConsoleKey.N)
                return false;
            else
                Console.WriteLine(Localization.T("menu.wrong.keypress"));
        }
    }

    public static void ExitProgram()
    {
        Console.WriteLine(Localization.T("program.exit"));
    }
    public static void WaitForKeypress()
    {
        Console.WriteLine("\n" + Localization.T("request.keypress"));
        Console.Write(Localization.T("pointer"));
        Console.ReadKey();
    }
}
