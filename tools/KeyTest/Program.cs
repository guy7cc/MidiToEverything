using System.Text;
using Microsoft.Extensions.Logging;
using MidiToEverything.Core.Application;
using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Mapping;
using MidiToEverything.Infrastructure.Input;
using MidiToEverything.Infrastructure.Midi;
using MidiToEverything.Tools.KeyTest;

// Real key-injection verification for M5 (docs/04_Roadmap.md): wires real MIDI input through
// the mapping pipeline into Win32 SendInput. Each Note On types a letter into the focused
// window, so playing the device types text into e.g. Notepad. Run via run-key-test.bat.

Console.OutputEncoding = Encoding.UTF8;
var sync = new object();

PrintHeader();

var srcLogger = new ConsoleRelayLogger<DryWetMidiSource>((lvl, msg) =>
    WriteLine(ConsoleColor.Yellow, $"   ! {msg}"));
var sinkLogger = new ConsoleRelayLogger<Win32InputSink>((lvl, msg) =>
    WriteLine(ConsoleColor.Yellow, $"   ! {msg}"));

using var source = new DryWetMidiSource(srcLogger);
var sink = new LoggingInputSink(
    new Win32InputSink(sinkLogger),
    text => WriteLine(ConsoleColor.Cyan, "  -> " + text));

var config = BuildKeyTestConfig();
var context = new MutableMappingContext(new ProfileLayers(config.Base));
var executor = new ActionExecutor(sink);
await using var pipeline = new MidiEventPipeline(source, context, executor);

source.DeviceConnected += (_, d) => WriteLine(ConsoleColor.Green, $"  ● 接続: {d.Name}");
source.DeviceDisconnected += (_, d) => WriteLine(ConsoleColor.Red, $"  ○ 切断: {d.Name}");

pipeline.Start();
source.Start();

WriteLine(ConsoleColor.Gray, "  マッピング: 各ノート(Note On) → アルファベットを1文字入力 (a〜z を循環)");
Console.WriteLine();
WriteLine(ConsoleColor.Yellow, "  ▼ 使い方");
Console.WriteLine("    1) メモ帳など文字入力できるウィンドウを開く");
Console.WriteLine("    2) そのウィンドウを最前面にする（ここをクリックしない）");
Console.WriteLine("    3) MIDI の鍵盤/パッドを弾く → 文字が入力されます");
Console.WriteLine();
WriteLine(ConsoleColor.DarkYellow, "  ※ キー入力は『最前面のウィンドウ』に送られます。送りたい先を前面にしてください。");
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
source.Stop();
Console.WriteLine();
Console.WriteLine("  終了しました。");
return;

// Build a base profile that maps every Note On to a single letter (a..z by note number),
// so any device produces visible typing.
KeyTestConfig BuildKeyTestConfig()
{
    var bindings = new List<Binding>(128);
    for (var note = 0; note < 128; note++)
    {
        var letter = (char)('a' + (note % 26));
        bindings.Add(new Binding
        {
            Signal = new Signal { Type = SignalKind.NoteOn, Number = note },
            Actions = new InputAction[] { new KeyAction(new[] { letter.ToString() }) },
            Label = $"note {note} → {letter}",
        });
    }

    return new KeyTestConfig(new Profile { Id = "keytest", Name = "Key Test", Bindings = bindings });
}

void PrintHeader()
{
    Console.WriteLine();
    WriteLine(ConsoleColor.Cyan, "  === MidiToEverything : キー送信テスト (M5 動作確認) ===");
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

internal sealed record KeyTestConfig(Profile Base);
