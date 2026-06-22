using System.Text;
using System.Text.RegularExpressions;
using MidiToEverything.Core.Application;
using MidiToEverything.Core.Domain;
using MidiToEverything.Infrastructure.Midi;
using MidiToEverything.Infrastructure.Window;
using MidiToEverything.Tools.WindowWatch;

// Active-window / profile-switch verification for M6 (docs/04_Roadmap.md). Watches the
// foreground window and shows which profile auto-activates; MIDI pads switch manually.
// Run via run-window-watch.bat.

Console.OutputEncoding = Encoding.UTF8;
var sync = new object();

PrintHeader();

var config = BuildConfig();

using var watcher = new WinEventWindowWatcher();
using var manager = new ProfileManager(config, watcher);
using var source = new DryWetMidiSource();
var executor = new ActionExecutor(new NullInputSink());
await using var pipeline = new MidiEventPipeline(source, manager, executor);

// MIDI-driven switch actions (FR-5.4) flow from the pipeline into the manager.
pipeline.ProfileSwitchRequested += (_, action) => manager.HandleSwitch(action);

source.DeviceConnected += (_, d) => WriteLine(ConsoleColor.Green, $"  ● 接続: {d.Name}");
source.DeviceDisconnected += (_, d) => WriteLine(ConsoleColor.Red, $"  ○ 切断: {d.Name}");
manager.Changed += (_, state) => PrintState(state);

pipeline.Start();
source.Start();
manager.Start(); // seeds and prints the initial foreground/profile

Console.WriteLine();
WriteLine(ConsoleColor.Gray, "  プロファイル: Notepad / Browser / Explorer / VS Code（一致するアプリを前面にすると自動切替）");
WriteLine(ConsoleColor.Gray, "  手動切替(MIDI): Note 36=次, 37=前, 38=固定/解除トグル");
Console.WriteLine();
WriteLine(ConsoleColor.Yellow, "  ▼ 使い方: メモ帳・ブラウザ・エクスプローラーを Alt+Tab で切り替えてください。");
WriteLine(ConsoleColor.Yellow, "           前面ウィンドウに応じてプロファイルが自動で切り替わります。");
Console.WriteLine("  終了するには Ctrl+C を押します。");
Console.WriteLine(new string('-', 72));

using var quit = new ManualResetEventSlim(false);
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    quit.Set();
};
quit.Wait();

await pipeline.DisposeAsync();
manager.Stop();
source.Stop();
Console.WriteLine();
Console.WriteLine("  終了しました。");
return;

AppConfig BuildConfig()
{
    Binding Switch(int note, ProfileSwitchTarget target) => new()
    {
        Signal = new Signal { Type = SignalKind.NoteOn, Number = note },
        Actions = new InputAction[] { new SwitchProfileAction(target) },
    };

    Profile App(string id, string name, params string[] processes) => new()
    {
        Id = id,
        Name = name,
        // Unified match regex: any of the process names on the first line of "process\ntitle".
        Match = new MatchRule
        {
            Pattern = string.Join("|", processes.Select(p => "^" + Regex.Escape(p) + "$")),
        },
    };

    var @base = new Profile
    {
        Id = "base",
        Name = "Base",
        Bindings = new[]
        {
            Switch(36, ProfileSwitchTarget.Next),
            Switch(37, ProfileSwitchTarget.Previous),
            Switch(38, ProfileSwitchTarget.Toggle),
        },
    };

    return new AppConfig
    {
        BaseProfile = @base,
        Profiles = new[]
        {
            App("notepad", "Notepad", "notepad.exe"),
            App("browser", "Browser", "chrome.exe", "msedge.exe", "firefox.exe"),
            App("explorer", "Explorer", "explorer.exe"),
            App("vscode", "VS Code", "Code.exe"),
        },
    };
}

void PrintState(ActiveProfileState state)
{
    var pin = state.IsPinned ? "  [固定]" : "";
    var context = state.Context?.Name ?? "(一致なし)";
    var title = Truncate(state.Window.WindowTitle, 32);

    WriteLine(ConsoleColor.DarkGray, $"  [前面] {state.Window.ProcessName,-16} \"{title}\"");
    WriteLine(ConsoleColor.Cyan, $"     → プロファイル: {state.Effective.Name}{pin}   (context: {context})");
}

static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";

void PrintHeader()
{
    Console.WriteLine();
    WriteLine(ConsoleColor.Cyan, "  === MidiToEverything : ウィンドウ連動プロファイル切替 (M6 動作確認) ===");
    Console.WriteLine();
}

void WriteLine(ConsoleColor color, string text)
{
    lock (sync)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = previous;
    }
}
