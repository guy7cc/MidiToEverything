using System.Text;
using Microsoft.Extensions.Logging;
using MidiToEverything.Core.Domain;
using MidiToEverything.Infrastructure.Midi;
using MidiToEverything.Tools.MidiMonitor;

// Real-device verification harness for M4 (docs/04_Roadmap.md): lists MIDI inputs, reports
// hot-plug connect/disconnect, and prints incoming events live. Run via run-midi-monitor.bat.

Console.OutputEncoding = Encoding.UTF8;
var sync = new object();

PrintHeader();

var logger = new ConsoleRelayLogger<DryWetMidiSource>((level, message) =>
    WriteLine(level >= LogLevel.Warning ? ConsoleColor.Yellow : ConsoleColor.DarkGray, $"   ! {message}"));

using var source = new DryWetMidiSource(logger);

source.DeviceConnected += (_, d) => WriteLine(ConsoleColor.Green, $"  ● 接続: {d.Name}");
source.DeviceDisconnected += (_, d) => WriteLine(ConsoleColor.Red, $"  ○ 切断: {d.Name}");
source.MessageReceived += (_, m) => WriteLine(ColorFor(m.Type), "  " + Format(m));

source.Start();

var initial = source.Devices;
if (initial.Count == 0)
{
    WriteLine(ConsoleColor.Yellow, "  (MIDI入力デバイスが見つかりません。接続するとここに表示されます)");
}
else
{
    WriteLine(ConsoleColor.Gray, $"  検出済みデバイス: {string.Join(", ", initial.Select(d => d.Name))}");
}

Console.WriteLine();
Console.WriteLine("  鍵盤/パッド/ノブ/フェーダーを操作してください。デバイスの抜き差しも試せます。");
Console.WriteLine("  終了するには Ctrl+C を押します。");
Console.WriteLine(new string('-', 72));

// Hot-plug is handled by polling inside DryWetMidiSource, so the tool just needs to
// stay alive until Ctrl+C.
using var quit = new ManualResetEventSlim(false);
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    quit.Set();
};
quit.Wait();

source.Stop();
Console.WriteLine();
Console.WriteLine("  終了しました。");
return;

void PrintHeader()
{
    Console.WriteLine();
    WriteLine(ConsoleColor.Cyan, "  === MidiToEverything : MIDI入力モニタ (M4 動作確認) ===");
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

static ConsoleColor ColorFor(MidiMessageType type) => type switch
{
    MidiMessageType.NoteOn => ConsoleColor.Green,
    MidiMessageType.NoteOff => ConsoleColor.DarkGray,
    MidiMessageType.ControlChange => ConsoleColor.Cyan,
    MidiMessageType.PitchBend => ConsoleColor.Magenta,
    MidiMessageType.ProgramChange => ConsoleColor.Yellow,
    _ => ConsoleColor.Gray,
};

static string Format(MidiMessage m)
{
    var head = $"{m.Device,-16} ch{m.Channel:00}  {m.Type,-13}";
    return m.Type switch
    {
        MidiMessageType.NoteOn or MidiMessageType.NoteOff =>
            $"{head} note={m.Number,3} {NoteName(m.Number!.Value),-4} vel={m.Value,3}",
        MidiMessageType.ControlChange =>
            $"{head} cc  ={m.Number,3}      val={m.Value,3} {Gauge(m.Value, 127)}",
        MidiMessageType.PitchBend =>
            $"{head}              val={m.Value,5} {Gauge(m.Value, 16383)}",
        MidiMessageType.ProgramChange =>
            $"{head} program={m.Number,3}",
        _ => head,
    };
}

static string NoteName(int note)
{
    string[] names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    return $"{names[note % 12]}{(note / 12) - 1}";
}

static string Gauge(int value, int max)
{
    const int width = 20;
    var filled = Math.Clamp((int)Math.Round((double)value / max * width), 0, width);
    return "[" + new string('#', filled) + new string('-', width - filled) + "]";
}
