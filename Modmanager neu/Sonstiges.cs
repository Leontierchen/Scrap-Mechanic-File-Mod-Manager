using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using static Modmanager_neu.Program;

namespace Modmanager_neu
{
    [SupportedOSPlatform("windows")]
    internal class Sonstiges
    {
        public static void DebugText(string text)
        {
            if (IsDebug)
                Console.WriteLine("[Debug] " + text);
        }
        public static string[] SortArray(string [] array)
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
            private static int? _startY;

            public static void Draw(int current, int max, int barWidth = 30)
            {
                if (max <= 0) max = 1;
                if (current < 0) current = 0;
                if (current > max) current = max;

                _startY ??= Console.CursorTop;

                int startY = _startY.Value;

                if (startY + 1 >= Console.BufferHeight)
                {
                    startY = Console.BufferHeight - 3;
                    _startY = startY;
                }

                double percent = (double)current / max;
                int filled = (int)Math.Round(percent * barWidth);
                int percentage = (int)Math.Round(percent * 100);

                string text = $"{percentage,3}% ({current}/{max})";

                int barX = Math.Max(0, (Console.WindowWidth - (barWidth + 2)) / 2);
                int textX = Math.Max(0, (Console.WindowWidth - text.Length) / 2);

                Console.SetCursorPosition(textX, startY);
                Console.Write(text.PadRight(Console.WindowWidth));

                Console.SetCursorPosition(barX, startY + 1);
                Console.Write("[");
                Console.Write(new string('█', filled));
                Console.Write(new string('░', barWidth - filled));
                Console.Write("]");
                if (current == max)
                    Finish();
            }

            public static void Finish()
            {
                if (_startY == null)
                    return;

                int endLine = _startY.Value + 2;

                if (endLine >= Console.BufferHeight)
                    endLine = Console.BufferHeight - 1;

                Console.SetCursorPosition(0, endLine);
                _startY = null;
            }
        }
        public static class Filehelper
        {
            public static void Move(string source, string target, bool overwrite = false, string[]? files = null, bool useProgressbar = true)
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

                    string filename = Path.GetFileName(files[i]);
                    string? path = Path.GetDirectoryName(files[i]);
                    string? newpath = Path.Combine(target, Path.GetRelativePath(source, path));
                    string newfile = Path.Combine(newpath, filename);

                    if (File.Exists(Path.Combine(newpath, filename)))
                    {
                        Console.WriteLine(String.Format(Localization.T("file.exists.skip"), files[i]));
                    }
                    else
                    {
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
                }
                DebugText("Move done");
            }
            public static void Copy(string source, string target, bool overwrite = false, string[]? files = null, bool useProgressbar = true)
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
                      

                    string filename = Path.GetFileName(files[i]);
                    string? path = Path.GetDirectoryName(files[i]);
                    string? newpath = Path.Combine(target, Path.GetRelativePath(source, path));
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
                string[] dirs = Directory.GetDirectories(path, "*", SearchOption.AllDirectories)
                                         .OrderByDescending(d => d.Length) // tiefste zuerst
                                         .ToArray();

                foreach (string dir in dirs)
                {
                    try
                    {
                        DirectoryInfo di = new DirectoryInfo(dir);
                        di.Attributes = FileAttributes.Normal; // Schreibgeschützt/System entfernen
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
                    DirectoryInfo mainDir = new DirectoryInfo(path);
                    mainDir.Attributes = FileAttributes.Normal;
                    Directory.Delete(path);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Fehler beim Löschen des Hauptordners {path}: {ex.Message}");
                }
            }
        }
    }
}

