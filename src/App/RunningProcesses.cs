using System.Diagnostics;

namespace MidiToEverything.App;

/// <summary>A currently-running process that owns a visible window — a candidate target app.</summary>
/// <param name="Exe">Executable file name, e.g. "notepad.exe" (matches MatchRule.ProcessNames).</param>
/// <param name="Title">Current main-window title, for disambiguation in the picker.</param>
public sealed record RunningProcess(string Exe, string Title)
{
    public string Display => string.IsNullOrWhiteSpace(Title) ? Exe : $"{Exe}  —  {Title}";
}

/// <summary>
/// Enumerates running processes that own a top-level window, so the profile editor can offer
/// them as target-process candidates instead of requiring the name to be typed.
/// </summary>
public static class RunningProcessScanner
{
    public static IReadOnlyList<RunningProcess> Scan()
    {
        var found = new List<RunningProcess>();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.MainWindowHandle == IntPtr.Zero)
                {
                    continue; // background/service process — not a target app
                }

                var title = process.MainWindowTitle;
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                found.Add(new RunningProcess($"{process.ProcessName}.exe", title));
            }
            catch
            {
                // Access-denied / exited between enumeration and inspection — skip.
            }
            finally
            {
                process.Dispose();
            }
        }

        return found
            .GroupBy(p => p.Exe, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(p => p.Exe, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
