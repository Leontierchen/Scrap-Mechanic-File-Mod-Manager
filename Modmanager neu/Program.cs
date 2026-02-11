using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;

namespace Modmanager_neu
{
    [SupportedOSPlatform("windows")]
    class Program ()
    {

        public static readonly string AppName = "Scrap Mechanic file mod Manager";
        public static readonly string AppVersion = "0.1";
        public static readonly string Masterlanguage = "de";
        public static bool IsDebug { get; set; } = true;
        public static bool langfallback = false;

        public static Config config { get; set; } = null!;
        
        
        public static readonly string logspath = Path.Combine(AppContext.BaseDirectory, "logs");
        public static readonly string languagepath = Path.Combine(AppContext.BaseDirectory, "language");
        

        // Logging helpers
        private static StringWriter? _logBuffer;
        private static TextWriter? _originalOut;
        private static TextWriter? _originalErr;

        //public static Config config = LoadConfig() ?? new Config();

        static void Main()
        {

            config = LoadConfig() ?? new Config();
            IsDebug = config.Debug;
            // Ensure console uses UTF-8 so Umlauts (Ä Ö Ü etc.) are displayed correctly
            // Also switch the Windows console code page to UTF-8 (65001) to avoid � characters
            try
            {
                // Set Windows console code page to UTF-8
                Sonstiges.NativeMethods.SetConsoleOutputCP(65001);
                Sonstiges.NativeMethods.SetConsoleCP(65001);
            }
            catch { }

            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            // Setup logging (capture all Console output)
            _originalOut = Console.Out;
            _originalErr = Console.Error;
            _logBuffer = new StringWriter();
            var tee = new Sonstiges.TeeTextWriter(_originalOut, _logBuffer);
            Console.SetOut(tee);
            Console.SetError(tee);

            Sonstiges.DebugText("Logging initialized.");
            Sonstiges.DebugText("----------- start config -----------");
            if (config != null)
            {
                Console.WriteLine("[Debug] Geladene Konfiguration:");
                foreach (var prop in typeof(Config).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    var value = prop.GetValue(config);
                    Console.WriteLine($"[Debug]  {prop.Name}: {value}");
                }
            }
            else
            {
                Console.WriteLine("[Debug] Keine gültige Konfiguration gefunden.");
            }
            // config start
            if (config == null)
            {
                Console.WriteLine("Keine Konfigurationsdatei gefunden — erstelle neu / Found no Config file - creating new");
                config = new Config();
            }

            Localization.Init(config.Language ?? Masterlanguage);
            SaveConfig(config);
            // ----------- ende config -----------
            // ----------- start main -----------
            if (IsDebug)
                IO.WaitForKeypress();
            Menu.Start();
            if (IsDebug)
                WriteLogAndExit(2, "0"); // exit code 2 = Debug mode
            else
                WriteLogAndExit(0, "0"); // exit code 0 = log and exit immediately
            // ----------- ende main -----------
        }
        public static void WriteLogAndExit(int exitCode, string ex = "")
        {
            // Ausgabe des Exit-Grunds
            string cause;
            if (exitCode < 3)
            {
                cause = exitCode switch
                {
                    0 => String.Empty, // normal exit with no pause, error or nothing to report
                    1 => Localization.T("exit.code.1"), // pause
                    2 => Localization.T("exit.code.2"), // Debug mode
                    _ => Localization.T("undefined.error"), // undefined error
                };
            }//normal exits without Errors
            else // exits with Errors
            {
                cause = string.Format(Localization.T("error.details"), exitCode switch
                {
                    3 => string.Format(Localization.T("exit.code.3"), ex), // SteamID invalid
                    4 => string.Format(Localization.T("exit.code.4"), ex), // GamePath invalid
                    5 => string.Format(Localization.T("exit.code.5"), ex), // Move file error
                    6 => string.Format(Localization.T("exit.code.6"), ex), // Copy file error
                    7 => string.Format(Localization.T("exit.code.7"), ex), // Delete file error
                    8 => string.Format(Localization.T("exit.code.8"), ex), // Create directory error
                    9 => string.Format(Localization.T("exit.code.9"), ex), // Extract zip error
                    10 => string.Format(Localization.T("exit.code.10"), ex), // Write file error
                    11 => string.Format(Localization.T("exit.code.11"), ex), // Rename mod error
                    12 => string.Format(Localization.T("exit.code.12"), ex), // Update Mod error

                    30 => Localization.T("placeholder"), // placeholder for future errors
                    40 => string.Format(Localization.T("placeholder"), ex), // placeholder for future errors with details

                    _ => Localization.T("undefined.error"), // undefined error
                }) + "\n-->exit-code " + exitCode;
            }
            //  Ausgabe des Grundes vor dem Log
            if (exitCode > 3)
                IO.ShowMessage("program.exit.error.occurred");

            if (!string.IsNullOrEmpty(cause))
                Console.WriteLine("-->" + cause);

            try
            {
                // Flush Console writers
                Console.Out.Flush();
                if (Directory.Exists(logspath))
                {
                    // Lösche alte Logs, behalte aber die letzten 10
                    var oldLogs = Directory.GetFiles(logspath, "run_*.log")
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(f => f.CreationTime)
                        .Skip(10);
                    foreach (var log in oldLogs)
                    {
                        try
                        {
                            log.Delete();
                        }
                        catch { }
                    }
                }
                else
                    Directory.CreateDirectory(logspath);
                string filename = $"run_{DateTime.Now:yyyyMMdd_HHmmss}.log";
                string path = Path.Combine(logspath, filename);

                // Write buffer content to file
                var content = _logBuffer?.ToString() ?? string.Empty;
                // Append a footer with exit code and timestamp
                content = $"Timestamp: {DateTime.Now:O}\nExitCode: {exitCode}\n\n" + content;
                File.WriteAllText(path, content, Encoding.UTF8);

                // Restore original console and notify
                if (_originalOut != null)
                    Console.SetOut(_originalOut);
                if (_originalErr != null)
                    Console.SetError(_originalErr);

                IO.ShowMessage("log.written", [path]);
            }
            catch
            {
                // best effort: ignore logging errors
            }
            finally
            {
                // Pause nur bei Fehlern oder im Debug-Modus
                if (exitCode > 0)
                {
                    IO.ShowMessage("exit.code.1"); // press enter to exit
                    IO.Handleinput();
                }
                else
                    Thread.Sleep(1000);
                // Exit
                Environment.Exit(exitCode);
            }
        }
        // ------ Config laden/speichern ------
        public static Config? LoadConfig()
        {
            Console.WriteLine("[Debug] Lade Konfiguration...");

            var possible = GetConfigSearchPaths().Cast<string>().ToArray();
            Console.WriteLine("[Debug] Suche nach config.json in folgenden Pfaden:");
                foreach (var p in possible)
                {
                    Console.WriteLine("[Debug]   " + p);
                }
            string? found = possible.FirstOrDefault(File.Exists);
            Console.WriteLine("[Debug] Config gefunden in: " + found);
            if (found == null) return null;

            try
            {
                var json = File.ReadAllText(found);
                JsonSerializerOptions jsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };
                var options = jsonSerializerOptions;
                var cfg = JsonSerializer.Deserialize<Config>(json, options);

                return cfg;
            }
            catch
            {
                return null;
            }
        }
        // Ermittelt den Pfad zur config.json: existierende Datei falls vorhanden, sonst Standard im BaseDirectory
        public static string GetConfigFilePath()
        {
            var possible = GetConfigSearchPaths().Cast<string>().ToArray();
            var found = possible.FirstOrDefault(File.Exists);
            if (!string.IsNullOrEmpty(found)) return found!;

            // Standardpfad neben der EXE
            return Path.Combine(AppContext.BaseDirectory, "config.json");
        }
        private static string[] GetConfigSearchPaths()
        {
            return [
                Path.Combine(AppContext.BaseDirectory, "logs", "config.json"),
                Path.Combine(AppContext.BaseDirectory, "config.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "config.json"),
                Path.Combine(AppContext.BaseDirectory, "language", "config.json"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "config.json"),
            ];
        }
        // Speichert die Konfiguration als prettified JSON an dem angegebenen Pfad
        public static void SaveConfig(Config cfg)
        {
            JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = true };
            var options = jsonSerializerOptions;
            var json = JsonSerializer.Serialize(cfg, options);
            File.WriteAllText(GetConfigFilePath(), json, Encoding.UTF8);
        }
        // Steam related functions
        internal class Steam
        {
            //get current windows username
            public static string Username()
            {
                string temp = Path.GetTempPath();
                string[] path = temp.Split(@"\");
                string username = path[2];
                return (username);
            }
            public static string GetSteamUser()
            {
                if (config.SteamID != null && Directory.Exists(config.SteamID))
                {
                    return config.SteamID;
                }
                else //try to find steam user folder
                {
                    string steamuser = string.Empty;
                    string username = Username();
                    string steampath = @$"C:\Users\{username}\AppData\Roaming\Axolot Games\Scrap Mechanic\User";
                    if (Directory.Exists(steampath))
                    {
                        string[] temp = Directory.GetDirectories(steampath);
                        string input = Path.Combine(steampath, temp[0]) ?? string.Empty;
                        if (!Directory.Exists(input)) //ask user for steam folder
                        {
                            int tries = 3;
                            IO.ShowMessage("enter.steam.user.promt");
                            while (tries != 0)
                            {
                                var userinput = IO.Handleinput();
                                string userinput2 = Path.Combine(steampath, userinput);
                                if (Directory.Exists(userinput))
                                {
                                    input = userinput;
                                    break;
                                }
                                else if (Directory.Exists(userinput2))
                                {
                                    input = userinput2;
                                    break;
                                }
                                else
                                {
                                    IO.ShowMessage("wrong.folder.input", [tries.ToString()]);
                                    tries--;
                                }
                            }
                            if (tries == 0)
                            {
                                input = string.Empty;
                            }
                        }
                        steamuser = input;
                    }
                    config.SteamID = steamuser;
                    SaveConfig(config);
                    return (steamuser);
                }
            }
        }
        // Translation / Localization class
        internal class Localization
        {
            public static Dictionary<string, JsonElement>? _map;
            public static Dictionary<string, JsonElement>? _masterMap;
            public static string _lang = Masterlanguage;

            /// <summary>
            /// Initialisiert die Lokalisierung. Lädt die Master-Datei (Deutsch) und die gewählte Sprache.
            /// Fehlende Keys können vom Benutzer manuell übersetzt werden.
            /// </summary>
            /// <param name="lang"></param> Die gewählte Sprache oder Masterlanguage als Fallback
            public static void Init(string lang)
            {
                Sonstiges.DebugText($"Initialisiere Lokalisierung für Sprache: '{lang}'");
                _lang = string.IsNullOrWhiteSpace(lang) ? Masterlanguage : lang;
                
                // Master-Datei immer laden
                LoadMasterLangFile();
                
                // Gewählte Sprache laden
                LoadLangFile(_lang);
                
                Sonstiges.DebugText($"Lokalisierung initialisiert. Geladene Einträge: {_map?.Count ?? 0}");
            }

            private static void LoadMasterLangFile()
            {
                try
                {
                    var baseDir = AppContext.BaseDirectory;
                    var langDir = Path.Combine(baseDir, "language");
                    var file = Path.Combine(langDir, Masterlanguage + ".json");

                    if (!File.Exists(file))
                    {
                        Sonstiges.DebugText($"Master-Sprachdatei ({Masterlanguage}) nicht gefunden!");
                        _masterMap = [];
                        return;
                    }

                    var bytes = File.ReadAllBytes(file);
                    string json = TryDecode(bytes);
                    _masterMap = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? [];
                    Sonstiges.DebugText($"Master-Sprachdatei geladen: {_masterMap.Count} Einträge");
                }
                catch (Exception ex)
                {
                    Sonstiges.DebugText($"Fehler beim Laden der Master-Sprachdatei: {ex.Message}");
                    _masterMap = [];
                }
            }

            public static void LoadLangFile(string lang)
            {
                try
                {
                    var baseDir = AppContext.BaseDirectory;
                    var langDir = Path.Combine(baseDir, "language");
                    var file = Path.Combine(langDir, lang + ".json");

                    if (!File.Exists(file))
                    {
                        if (lang != Masterlanguage)
                        {
                            Sonstiges.DebugText($"Sprachdatei '{lang}.json' nicht gefunden. Erstelle leere Map.");
                            _map = [];
                            return;
                        }
                        else
                        {
                            Sonstiges.DebugText($"Master-Sprachdatei nicht vorhanden!");
                            _map = [];
                            return;
                        }
                    }

                    var bytes = File.ReadAllBytes(file);
                    string json = TryDecode(bytes);
                    _map = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? [];
                    Sonstiges.DebugText($"Sprachdatei '{lang}' geladen: {_map.Count} Einträge");
                }
                catch (Exception ex)
                {
                    Sonstiges.DebugText($"Fehler beim Laden der Sprachdatei: {ex.Message}");
                    _map = [];
                }
            }

            public static string TryDecode(byte[] bytes)
            {
                try
                {
                    var s = Encoding.UTF8.GetString(bytes);
                    if (!ContainsReplacementChar(s)) return s;
                }
                catch { }

                try
                {
                    var s = Encoding.Default.GetString(bytes);
                    if (!ContainsReplacementChar(s)) return s;
                }
                catch { }

                try
                {
                    var s = Encoding.Unicode.GetString(bytes);
                    if (!ContainsReplacementChar(s)) return s;
                }
                catch { }

                return Encoding.UTF8.GetString(bytes);
            }

            public static bool ContainsReplacementChar(string s)
            {
                return s.Contains('\uFFFD');
            }

            /// <summary>
            /// Liest den übersetzten Text für den angegebenen Schlüssel.
            /// Falls der Key nicht existiert und wir nicht in der Master-Sprache sind,
            /// wird der Benutzer zur Eingabe einer Übersetzung aufgefordert.
            /// </summary>
            /// <param name="key">Der Schlüssel für die Übersetzung</param>
            /// <returns>Übersetzter Text oder User-Input oder Master-Text</returns>
            public static string T(string key)
            {
                // Versuche Key in aktueller Sprache zu finden
                if (_map != null && _map.TryGetValue(key, out var val))
                {
                    if (val.ValueKind == JsonValueKind.String)
                        return val.GetString()!;
                }

                // Wenn wir nicht in der Master-Sprache sind, frage den User
                if (_lang != Masterlanguage)
                {
                    return PromptForTranslation(key);
                }

                // Master-Sprache: versuche aus Master zu laden
                if (_masterMap != null && _masterMap.TryGetValue(key, out var masterVal))
                {
                    if (masterVal.ValueKind == JsonValueKind.String)
                        return masterVal.GetString()!;
                }

                return key;
            }

            /// <summary>
            /// Liest ein Array von Strings für den angegebenen Schlüssel.
            /// Falls der Key nicht existiert und wir nicht in der Master-Sprache sind,
            /// wird der Benutzer zur Eingabe aufgefordert.
            /// </summary>
            /// <param name="key">Der Schlüssel für das Array</param>
            /// <returns>Übersetzte Stringarray oder leeres Array</returns>
            public static string[] TArray(string key)
            {
                // Versuche Key in aktueller Sprache zu finden
                if (_map != null && _map.TryGetValue(key, out var val) &&
                    val.ValueKind == JsonValueKind.Array)
                {
                    return [.. val.EnumerateArray()
                              .Where(e => e.ValueKind == JsonValueKind.String)
                              .Select(e => e.GetString()!)];
                }

                // Wenn wir nicht in der Master-Sprache sind, frage den User
                if (_lang != Masterlanguage)
                {
                    var masterArray = GetMasterArray(key);
                    if (masterArray.Length > 0)
                    {
                        return PromptForArrayTranslation(key, masterArray);
                    }
                }

                // Master-Sprache: versuche aus Master zu laden
                if (_masterMap != null && _masterMap.TryGetValue(key, out var masterVal) &&
                    masterVal.ValueKind == JsonValueKind.Array)
                {
                    return [.. masterVal.EnumerateArray()
                              .Where(e => e.ValueKind == JsonValueKind.String)
                              .Select(e => e.GetString()!)];
                }

                return [];
            }

            private static string[] GetMasterArray(string key)
            {
                if (_masterMap != null && _masterMap.TryGetValue(key, out var val) &&
                    val.ValueKind == JsonValueKind.Array)
                {
                    return [.. val.EnumerateArray()
                              .Where(e => e.ValueKind == JsonValueKind.String)
                              .Select(e => e.GetString()!)];
                }
                return [];
            }

            private static string PromptForTranslation(string key)
            {
                // Hole den Master-Text als Beispiel
                string masterText = key;
                if (_masterMap != null && _masterMap.TryGetValue(key, out var masterVal))
                {
                    if (masterVal.ValueKind == JsonValueKind.String)
                        masterText = masterVal.GetString() ?? key;
                }

                Console.WriteLine("\n" + new string('=', 60));
                Console.WriteLine($"⚠️  FEHLENDE ÜBERSETZUNG FÜR '{_lang.ToUpper()}'");
                Console.WriteLine(new string('=', 60));
                Console.WriteLine($"\nKey: {key}");
                Console.WriteLine($"Beispiel ({Masterlanguage}): {masterText}");
                Console.WriteLine($"\nBitte geben Sie die Übersetzung für '{_lang}' ein:");
                Console.Write("> ");

                string? userInput = IO.Handleinput();
                
                if (string.IsNullOrWhiteSpace(userInput))
                {
                    Console.WriteLine("⚠️  Keine Eingabe. Verwende Beispieltext.");
                    return masterText;
                }

                // Speichere die Übersetzung
                SaveTranslation(key, userInput);
                _map![key] = JsonSerializer.SerializeToElement(userInput);

                Console.WriteLine("✓ Übersetzung gespeichert!\n");
                return userInput;
            }

            private static string[] PromptForArrayTranslation(string key, string[] masterArray)
            {
                Console.WriteLine("\n" + new string('=', 60));
                Console.WriteLine($"⚠️  FEHLENDE ARRAY-ÜBERSETZUNG FÜR '{_lang.ToUpper()}'");
                Console.WriteLine(new string('=', 60));
                Console.WriteLine($"\nKey: {key}");
                Console.WriteLine($"\nBeispiele ({Masterlanguage}):");
                for (int i = 0; i < masterArray.Length; i++)
                {
                    Console.WriteLine($"  {i}) {masterArray[i]}");
                }

                var translatedArray = new List<string>();
                Console.WriteLine($"\nBitte geben Sie die Übersetzungen für '{_lang}' ein:");
                Console.WriteLine("(Drücken Sie ENTER ohne Text, um den Beispieltext zu verwenden)\n");

                for (int i = 0; i < masterArray.Length; i++)
                {
                    Console.Write($"[{i}] > ");
                    string? userInput = IO.Handleinput();

                    if (string.IsNullOrWhiteSpace(userInput))
                    {
                        translatedArray.Add(masterArray[i]);
                    }
                    else
                    {
                        translatedArray.Add(userInput);
                    }
                }

                // Speichere die Übersetzungen
                SaveTranslation(key, translatedArray);
                _map![key] = JsonSerializer.SerializeToElement(translatedArray);

                Console.WriteLine("\n✓ Übersetzungen gespeichert!\n");
                return [.. translatedArray];
            }

            private static void SaveTranslation(string key, string value)
            {
                try
                {
                    _map ??= [];

                    Directory.CreateDirectory(languagepath);
                    
                    var file = Path.Combine(languagepath, _lang + ".json");

                    // Speichere die aktuelle Map
                    JsonSerializerOptions serializerOptions = new() { WriteIndented = true };
                    var options = serializerOptions;
                    var json = JsonSerializer.Serialize(_map, options);
                    File.WriteAllText(file, json, Encoding.UTF8);

                    Sonstiges.DebugText($"Übersetzung gespeichert: {key} = {value}");
                }
                catch (Exception ex)
                {
                    Sonstiges.DebugText($"Fehler beim Speichern der Übersetzung: {ex.Message}");
                }
            }

            private static void SaveTranslation(string key, List<string> values)
            {
                try
                {
                    _map ??= [];

                    var baseDir = AppContext.BaseDirectory;
                    var langDir = Path.Combine(baseDir, "language");
                    Directory.CreateDirectory(langDir);

                    var file = Path.Combine(langDir, _lang + ".json");

                    // Speichere die aktuelle Map
                    JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = true };
                    var options = jsonSerializerOptions;
                    var json = JsonSerializer.Serialize(_map, options);
                    File.WriteAllText(file, json, Encoding.UTF8);

                    Sonstiges.DebugText($"Array-Übersetzung gespeichert: {key} mit {values.Count} Einträgen");
                }
                catch (Exception ex)
                {
                    Sonstiges.DebugText($"Fehler beim Speichern der Übersetzung: {ex.Message}");
                }
            }
        }
        // kleine poco klasse zum erstellen einer neuen Config. Hier muss alles angegeben sein, was auch in der Config erscheinen soll
        /// <summary>
        /// Kleine POCO-Klasse zur Repräsentation der Konfiguration. Alle Eigenschaften sollten mit einem Standardwert versehen sein, damit bei der Erstellung einer neuen Config alle notwendigen Felder vorhanden sind. Die Klasse ist intern, da sie nur innerhalb des Programms verwendet wird und nicht von außen zugänglich sein muss.
        /// </summary>
        internal class Config
        {
            public string? Language { get; set; } = "de"; // default deutsch
            public string? SteamID { get; set; } = "User_xxxxxxxxxxxxxx";
            public string? GamePath { get; set; } = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Scrap Mechanic";
            public bool Debug { get; set; } = false;
        }
    }
}
