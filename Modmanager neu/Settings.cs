using System.Runtime.Versioning;
using System.Text.Json;
using static Modmanager_neu.Program;

namespace Modmanager_neu
{
    [SupportedOSPlatform("windows")]
    internal class LanguageEntry
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
    }

    [SupportedOSPlatform("windows")]
    internal class Settings
    {
        public static void LanguageSelection()
        {
            string langDir = Path.Combine(AppContext.BaseDirectory, "language");

            if (!Directory.Exists(langDir))
            {
                Console.WriteLine("Language-Ordner nicht gefunden.");
                IO.WaitForKeypress();
                return;
            }

            var languages = new List<LanguageEntry>();

            foreach (var file in Directory.GetFiles(langDir, "*.json"))
            {
                try
                {
                    string code = Path.GetFileNameWithoutExtension(file);

                    var json = File.ReadAllText(file);
                    var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("language.name", out var nameProp))
                    {
                        languages.Add(new LanguageEntry
                        {
                            Code = code,
                            Name = nameProp.GetString() ?? code
                        });
                    }
                }
                catch
                {
                    // ungültige Sprachdatei → ignorieren
                }
            }

            if (languages.Count == 0)
            {
                IO.ShowMessage("settings.language.no.files");
                IO.WaitForKeypress();
                return;
            }

            Console.Clear();
            IO.ShowMessage("settings.language.select");
            Console.WriteLine();

            for (int i = 0; i < languages.Count; i++)
            {
                string current = languages[i].Code == config.Language ? " *" : "";
                Console.WriteLine($"{i + 1}) {languages[i].Name} ({languages[i].Code}){current}");
            }

            while (true)
            {

                string input = IO.Handleinput(q: true);

                if (input.Equals("Q", StringComparison.OrdinalIgnoreCase))
                    return;

                if (int.TryParse(input, out int index) &&
                    index >= 1 &&
                    index <= languages.Count)
                {
                    var selected = languages[index - 1];

                    config.Language = selected.Code;
                    Localization.Init(selected.Code);

                    Program.SaveConfig(config);

                    IO.ShowMessage("settings.language.changed", [selected.Name]);

                    IO.WaitForKeypress();
                    return;
                }

                IO.ShowMessage("menu.wrong.keypress");
            }
        }
        public static void Toggledebug()
        {
            if (config.Debug)
                config.Debug = false;
            else
                config.Debug = true;

            SaveConfig(config);
            IsDebug = config.Debug;
        }
        public static void ToggleAutoCheckUpdates()
        {
            if (config.AutoCheckForUpdates)
                config.AutoCheckForUpdates = false;
            else
                config.AutoCheckForUpdates = true;
            SaveConfig(config);
        }
        public static void ToggleDefaultMods()
        {
            if (config.UseDefaultMods)
            {
                config.UseDefaultMods = false;
                if (states.Installeddefaultmods)
                    Modtool.UnloadDefaultMods();
            }
            else
            {
                config.UseDefaultMods = true;
                if (!states.Installeddefaultmods)
                    Modtool.LoadDefaultMods();
            }
            SaveConfig(config);
            IO.WaitForKeypress();
        }
    }
}
