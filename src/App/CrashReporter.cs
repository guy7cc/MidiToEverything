using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using MidiToEverything.App.Localization;
using MidiToEverything.Core;
using Serilog;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace MidiToEverything.App;

/// <summary>
/// Last-resort crash handling. The app used to vanish silently on an unhandled exception; now any
/// unhandled error (UI thread, background thread, or faulted task) is logged with a crash report,
/// the app is relaunched, and the next launch tells the user what happened. A loop guard stops a
/// crash-on-startup from restarting forever.
/// </summary>
internal static class CrashReporter
{
    private const int MaxRestartsPerWindow = 3;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);
    private static int _handling;

    private static string LogDir => Path.Combine(AppInfo.DataDirectory, "logs");
    private static string MarkerPath => Path.Combine(AppInfo.DataDirectory, "last-crash.txt");
    private static string HistoryPath => Path.Combine(AppInfo.DataDirectory, "crash-history.txt");

    /// <summary>Best-effort cleanup (e.g. remove the tray icon) run just before the process exits.</summary>
    public static Action? Cleanup { get; set; }

    /// <summary>Register the global exception handlers. Call as early as possible in startup.</summary>
    public static void Install(Application app)
    {
        app.DispatcherUnhandledException += (_, e) =>
        {
            e.Handled = true; // suppress WPF's default crash; we log, restart, and inform instead
            Fatal(e.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Fatal(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            try { Log.Error(e.Exception, "Unobserved task exception (ignored)."); } catch { /* ignore */ }
            e.SetObserved(); // a faulted background task must not take the whole app down
        };
    }

    /// <summary>On startup, if the previous run crashed, tell the user once and clear the marker.</summary>
    public static void ShowPendingCrashNotice()
    {
        try
        {
            if (!File.Exists(MarkerPath))
            {
                return;
            }

            var detail = File.ReadAllText(MarkerPath);
            File.Delete(MarkerPath);
            MessageBox.Show(
                string.Format(Loc.T("crash.restarted"), detail, LogDir),
                Loc.T("crash.title"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch { /* never let the notice itself break startup */ }
    }

    private static void Fatal(Exception? ex)
    {
        if (Interlocked.Exchange(ref _handling, 1) == 1)
        {
            return; // a crash is already being handled
        }

        ex ??= new Exception("Unknown error (no exception object).");
        WriteReport(ex);

        var looping = RecordAndDetectLoop();
        try { Cleanup?.Invoke(); } catch { /* best effort */ }

        if (looping)
        {
            // Repeated crashes — stop the restart cycle and tell the user directly.
            try
            {
                MessageBox.Show(string.Format(Loc.T("crash.loop"), ex.Message, LogDir),
                    Loc.T("crash.title"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { /* a terminating background thread may not be able to show UI */ }
        }
        else
        {
            TryRestart();
        }

        try { Log.CloseAndFlush(); } catch { /* ignore */ }
        Environment.Exit(looping ? 1 : 0);
    }

    private static void WriteReport(Exception ex)
    {
        var stamp = DateTime.Now;
        try { Log.Fatal(ex, "Unhandled exception — restarting."); } catch { /* logger may be unset */ }
        try
        {
            Directory.CreateDirectory(LogDir);
            File.WriteAllText(Path.Combine(LogDir, $"crash-{stamp:yyyyMMdd-HHmmss}.txt"),
                $"{AppInfo.Name} {AppInfo.Version}{Environment.NewLine}{stamp:O}{Environment.NewLine}{Environment.NewLine}{ex}");
            // Concise summary shown by the next launch.
            File.WriteAllText(MarkerPath, $"{stamp:yyyy-MM-dd HH:mm:ss}  {ex.GetType().Name}: {ex.Message}");
        }
        catch { /* nothing more we can do */ }
    }

    private static void TryRestart()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
            {
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            try { Log.Error(ex, "Failed to relaunch after crash."); } catch { /* ignore */ }
        }
    }

    // Record this crash's time and report whether too many happened within the window (a crash loop).
    private static bool RecordAndDetectLoop()
    {
        try
        {
            var now = DateTime.UtcNow;
            var recent = new List<DateTime>();
            if (File.Exists(HistoryPath))
            {
                foreach (var line in File.ReadAllLines(HistoryPath))
                {
                    if (DateTime.TryParse(line, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t)
                        && now - t < Window)
                    {
                        recent.Add(t);
                    }
                }
            }

            recent.Add(now);
            File.WriteAllLines(HistoryPath, recent.Select(t => t.ToString("O", CultureInfo.InvariantCulture)));
            return recent.Count > MaxRestartsPerWindow;
        }
        catch
        {
            return false; // if we can't track it, prefer restarting
        }
    }
}
