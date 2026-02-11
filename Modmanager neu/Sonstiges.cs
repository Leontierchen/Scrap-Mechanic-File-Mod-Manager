using Microsoft.Win32;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using static Modmanager_neu.Program;

namespace Modmanager_neu
{
    [SupportedOSPlatform("windows")]
    internal class Sonstiges
    {
        public static void Contacts() 
        { 
            DebugText("Showing contact info");
            Console.Clear();
            IO.ShowMessage("contact.header");
            IO.ShowMessage("contact.info");
            IO.WaitForKeypress();
        }
        public static void DebugText(string text)
        {
            if (IsDebug)
                Console.WriteLine("[Debug] " + text);
        }
        public static string[] SortArray(string[] array)
        {
            var sorted = array.Select(s =>
            {
                try
                {
                    int start = s.IndexOf('(') + 1;
                    int length = s.IndexOf(')') - start;
                    string dateString = s.Substring(start, length);

                    var dt = DateTime.ParseExact(
                        dateString,
                        "dd.MM.yyyy HH.mm.ss",
                        CultureInfo.InvariantCulture
                    );

                    return (Name: s, Date: dt);
                }
                catch
                {
                    return (Name: s, Date: DateTime.MinValue);
                }
            })
        .OrderByDescending(x => x.Date)
        .Select(x => x.Name)
        .ToArray();
            return sorted;
        }
        public static class NativeMethods
        {
            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool SetConsoleOutputCP(uint wCodePageID);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool SetConsoleCP(uint wCodePageID);
        }
        public class TeeTextWriter(TextWriter first, TextWriter second) : TextWriter
        {
            private readonly TextWriter _first = first;
            private readonly TextWriter _second = second;

            public override Encoding Encoding => Encoding.UTF8;

            public override void Write(char value)
            {
                // write via native console if possible to ensure Unicode glyphs
                NativeConsole.Write(value.ToString());
                _second.Write(value);
            }

            public override void Write(string? value)
            {
                if (value == null) return;
                NativeConsole.Write(value);
                _second.Write(value);
            }

            public override void WriteLine(string? value)
            {
                value ??= string.Empty;
                NativeConsole.Write(value + "\n");
                _second.WriteLine(value);
            }

            public override void Flush()
            {
                _second.Flush();
            }
        }
        public static class NativeConsole

        {
            private const int STD_OUTPUT_HANDLE = -11;

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            private static extern bool WriteConsoleW(IntPtr hConsoleOutput, string lpBuffer, uint nNumberOfCharsToWrite, out uint lpNumberOfCharsWritten, IntPtr lpReserved);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern IntPtr GetStdHandle(int nStdHandle);

            [DllImport("kernel32.dll")]
            private static extern uint GetFileType(IntPtr hFile);

            private const uint FILE_TYPE_CHAR = 0x0002;

            private static bool IsConsoleAttached()
            {
                try
                {
                    var handle = GetStdHandle(STD_OUTPUT_HANDLE);
                    if (handle == IntPtr.Zero) return false;
                    var ft = GetFileType(handle);
                    return (ft & FILE_TYPE_CHAR) == FILE_TYPE_CHAR;
                }
                catch
                {
                    return false;
                }
            }

            public static void Write(string s)
            {
                if (s == null) return;

                if (IsConsoleAttached())
                {
                    try
                    {
                        var handle = GetStdHandle(STD_OUTPUT_HANDLE);
                        WriteConsoleW(handle, s, (uint)s.Length, out _, IntPtr.Zero);
                        return;
                    }
                    catch
                    {
                        // fallback
                    }
                }

                // fallback to managed Console
                try { Console.Out.Write(s); } catch { }
            }
        }
        public static class ProgressBar
        {
            public static void Draw(int current, int total, int width = 70)
            {
                if (total <= 0) total = 1;
                if (current < 0) current = 0;
                if (current > total) current = total;

                double progress = (double)current / total;
                int filled = (int)(progress * width);

                string bar =
                    new string('█', filled) +
                    new string('░', width - filled);

                Console.Write($"\r{current} [{bar}] {total}");
                if (current == total)
                    Console.WriteLine();
            }
        }
        public static class Filehelper
        {
            /// <summary>
            /// Moves files with progress bar and error handling. If files array is null, it will scan the source directory for all files. It preserves directory structure. If overwrite is false, existing files in target will be skipped. If combine is true, source and files are getting their path combined.
            /// </summary>
            /// <param name="source"></param> The source directory to move files from. If combine is true, this will be combined with the file paths in the files array.
            /// <param name="target"></param> The target directory to move files to. The directory structure from source will be preserved under this target directory.
            /// <param name="overwrite"></param> If true, existing files in the target location will be overwritten. If false, existing files will be skipped and a message will be printed.
            /// <param name="files"></param> An optional array of file paths to move. If null, the method will scan the source directory and all subdirectories for files to move. The paths in this array can be either absolute or relative to the source directory. If combine is true, they will be combined with the source path.
            /// <param name="useProgressbar"></param> If true, a progress bar will be displayed in the console to show the progress of the move operation. The progress bar will update after each file is processed.
            /// <param name="combine"></param> If true, the method will combine the source path with the file paths in the files array to determine the full path of each file to move. This allows for more flexible file lists that can be relative to the source directory. If false, the paths in the files array are treated as absolute paths.
            public static void Move(string source, string target, bool overwrite = false, string[]? files = null, bool useProgressbar = true, bool combine = false)
            {
                DebugText("Move started");
                if (files == null)
                {
                    DebugText("No file list provided, scanning directory...");
                    files = Directory.GetFiles(source, "*.*", SearchOption.AllDirectories);
                }
                else
                    DebugText($"Using provided file list with {files.Length} files.");

                for (int i = 0; i < files.Length; i++)
                {
                    if (useProgressbar)
                        ProgressBar.Draw(i + 1, files.Length);
                    if (combine)
                        files[i] = Path.Combine(source, files[i]);

                    string filename = Path.GetFileName(files[i]);
                    string? path = Path.GetDirectoryName(files[i]);
                    string? newpath = Path.Combine(target, Path.GetRelativePath(source, path!));
                    string newfile = Path.Combine(newpath, filename);

                   if (!Directory.Exists(newpath))
                    {
                        try
                        {
                            Directory.CreateDirectory(newpath);
                        }
                        catch (Exception ex)
                        {
                            WriteLogAndExit(5, ex.Message);
                        }
                        // copy file error 
                    }
                    try
                    {
                        File.Move(files[i],newfile, overwrite);
                    }
                    catch (Exception ex)
                    {
                        WriteLogAndExit(5, $"\nMessage: {ex.Message}\nOldfile: {files[i]}\nNewfile: {newfile}\n");
                    }
                }
                DebugText("Move done");
            }
            /// <summary>
            /// Copies files with progress bar and error handling. If files array is null, it will scan the source directory for all files. It preserves directory structure. If overwrite is false, existing files in target will be skipped. If combine is true, source and files are getting their path combined.
            /// </summary>
            /// <param name="source"></param> The source directory to copy files from. If combine is true, this will be combined with the file paths in the files array.
            /// <param name="target"></param> The target directory to copy files to. The directory structure from source will be preserved under this target directory.
            /// <param name="overwrite"></param> If true, existing files in the target location will be overwritten. If false, existing files will be skipped and a message will be printed.
            /// <param name="files"></param> An optional array of file paths to copy. If null, the method will scan the source directory and all subdirectories for files to copy. The paths in this array can be either absolute or relative to the source directory. If combine is true, they will be combined with the source path.
            /// <param name="useProgressbar"></param> If true, a progress bar will be displayed in the console to show the progress of the copy operation. The progress bar will update after each file is processed.
            /// <param name="combine"></param>
            public static void Copy(string source, string target, bool overwrite = false, string[]? files = null, bool useProgressbar = true, bool combine = false)
            {
                DebugText("Copy started");

                if (files == null)
                {
                    DebugText("No file list provided, scanning directory...");
                    files = Directory.GetFiles(source, "*.*", SearchOption.AllDirectories);
                }
                else
                    DebugText($"Using provided file list with {files.Length} files.");
                for (int i = 0; i < files.Length; i++)
                {
                    if (useProgressbar)
                        ProgressBar.Draw(i + 1, files.Length); 
                    if (combine)
                      files[i] = Path.Combine(source, files[i]);
                    
                    string filename = Path.GetFileName(files[i]);
                    string? path = Path.GetDirectoryName(files[i]);
                    string? newpath = Path.Combine(target, Path.GetRelativePath(source, path!));
                    string newfile = Path.Combine(newpath, filename);
                    if (!Directory.Exists(newpath))
                    {
                        try
                        {
                            Directory.CreateDirectory(newpath);
                        }
                        catch (Exception ex)
                        {
                            WriteLogAndExit(6, ex.Message);
                        }
                        // copy file error 
                    }
                    try
                    {
                        File.Copy(files[i], newfile, overwrite);
                    }
                    catch (Exception ex)
                    {
                        WriteLogAndExit(5, $"\nMessage: {ex.Message}\nOldfile: {files[i]}\nNewfile: {newfile}\n");
                    }
                }
                DebugText("Copy done");
            }
            public static void DeleteDirectory(string path, bool useProgressBar = true)
            {
                if (!Directory.Exists(path))
                {
                    Console.WriteLine(string.Format(Localization.T("exit.code.7"), "Directory non existent", path));
                    return;
                }

                // Alle Dateien rekursiv sammeln
                string[] files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);

                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        if (useProgressBar)
                            ProgressBar.Draw(i + 1, files.Length);

                        // Schreibgeschützt, versteckt, System entfernen
                        File.SetAttributes(files[i], FileAttributes.Normal);
                        File.Delete(files[i]);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(string.Format(Localization.T("exit.code.7"), files[i], ex.Message));
                    }
                }

                // Lösche Unterordner von unten nach oben
                string[] dirs = [.. Directory.GetDirectories(path, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length)];

                foreach (string dir in dirs)
                {
                    try
                    {
                        DirectoryInfo di = new(dir)
                        {
                            Attributes = FileAttributes.Normal // Schreibgeschützt/System entfernen
                        };
                        Directory.Delete(dir);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Fehler beim Löschen des Ordners {dir}: {ex.Message}");
                    }
                }

                // Zuletzt den Hauptordner löschen
                try
                {
                    DirectoryInfo mainDir = new(path)
                    {
                        Attributes = FileAttributes.Normal
                    };
                    Directory.Delete(path);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Fehler beim Löschen des Hauptordners {path}: {ex.Message}");
                }
            }
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
    [SupportedOSPlatform("windows")]
    public static class IO
    {
        public static void WaitForKeypress()
        {
            Console.WriteLine("\n" + Localization.T("request.keypress"));
            Console.Write(Localization.T("pointer"));
            Console.ReadKey();
        }
        /// <summary>
        /// Stellt dem Nutzer eine Frage, die mit Ja oder nein beantwortet werden muss.
        /// </summary>
        /// <param name="question">Fragetext als String</param>
        /// <returns>Ja -> true, Nein -> false</returns>
        public static bool YesOrNoPrompt(string question)
        {
            Console.WriteLine($"{question} (Y/J or N )");
            while (true)
            {
                var key = Console.ReadKey(true).Key;

                if (key == ConsoleKey.Y || key == ConsoleKey.J)
                    return true;
                else if (key == ConsoleKey.N)
                    return false;
                else
                    ShowMessage("menu.wrong.keypress");
            }
        }
        public static void ShowMessage(string messageKey, string[]? contents = null)
        {
            if (contents != null && contents.Length > 0)
            {
                object[] args = new object[contents.Length];
                for (int i = 0; i < contents.Length; i++)
                {
                    args[i] = contents[i];
                }
                Console.WriteLine(string.Format(Localization.T(messageKey), args));
            }
            else
            {
                Console.WriteLine(Localization.T(messageKey));
            }
        }
        public static string Handlekeypress(bool q = false)
        {
            if (q)
                Console.WriteLine(Localization.T("menu.go.back.q"));
            Console.Write(Localization.T("pointer"));
            var key = Console.ReadKey(true).KeyChar.ToString();
            return key;
        }
        public static string Handleinput(List<string>? list = null,bool q = false, bool a = false)
        {
            for (int i = 0; list != null && i < list.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {list[i]}");
            }
            if (q)
                Console.WriteLine(Localization.T("menu.go.back.q"));
            if (a)
                Console.WriteLine(Localization.T("picker.all.a"));
            Console.Write(Localization.T("pointer"));
            string input;
            input = Console.ReadLine()?.Trim() ?? "";
            Console.WriteLine(input);
            return input;
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

        internal static string Picker(string question, string? directory = null, string? activeoption = null, bool allowvanilla = false, bool allowall = false, bool allowsame = false)
        {
            Sonstiges.DebugText("Picker active");
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
                Console.WriteLine("0: " + Localization.T("mods.menu.disable.mods")); //vanilla option, only if a mod is active and vanilla is allowed
            for (int i = 0; i < dirs.Length; i++)
            {
                dirs[i] = Path.GetRelativePath(directory, dirs[i]); //only the name of the mod
                Console.WriteLine($"{i + 1}: " + dirs[i]);
            }
            
            string option;
            while (true)
            {
                string input = Handleinput(q: true, a: true);

                if (input.Equals("Q", StringComparison.OrdinalIgnoreCase)) // user wants to go back/cancel
                {
                    ShowMessage("picker.canceled");
                    WaitForKeypress();
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
                        ShowMessage("menu.wrong.keypress");
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
                        ShowMessage("menu.wrong.keypress");
                        continue; // ask for input again
                    }
                    else // mod option selected
                    {
                        option = dirs[index - 1];
                    }

                    if (option == activeoption && allowsame == false) // selected option is already active, and selecting the same option is not allowed
                    {
                        ShowMessage("picker.option.same");
                        continue; // ask for input again
                    }

                    break; // valid option selected, exit loop
                }
                else // invalid input
                {
                    ShowMessage("menu.wrong.keypress");
                }
            }
            Sonstiges.DebugText($"Modpicker return: {option}");
            return option; //returns the selected option
        }

    }
}

