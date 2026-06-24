using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;

// midisend — inject MIDI events into a named output port (a loopMIDI virtual port the app listens on).
//
// Usage:
//   midisend <portName> <event> [<event> ...]
// Events (channel is 1-16):
//   noteon:CH:NOTE:VEL     e.g. noteon:1:60:100
//   noteoff:CH:NOTE        e.g. noteoff:1:60
//   note:CH:NOTE:VEL:MS    press+release with MS hold  e.g. note:1:60:100:200
//   cc:CH:NUM:VAL          e.g. cc:1:74:127
//   sleep:MS               wait MS milliseconds
//   list                   (as portName) list available output ports and exit
//
// A 120ms gap is inserted between events so the engine processes them in order.

if (args.Length == 1 && args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("-- output ports --");
    foreach (var d in OutputDevice.GetAll())
    {
        Console.WriteLine(d.Name);
    }
    Console.WriteLine("-- input ports --");
    foreach (var d in InputDevice.GetAll())
    {
        Console.WriteLine(d.Name);
    }
    return 0;
}

// midisend monitor <inputPort> <ms> — listen on an input port and print received events
// (used to verify the app's midiOut action: point it at the loopback the app sends to).
if (args.Length >= 2 && args[0].Equals("monitor", StringComparison.OrdinalIgnoreCase))
{
    var inName = args[1];
    var ms = args.Length > 2 ? int.Parse(args[2]) : 3000;
    InputDevice input;
    try
    {
        input = InputDevice.GetByName(inName);
    }
    catch (Exception)
    {
        Console.Error.WriteLine($"MIDI入力ポート '{inName}' が見つかりません。");
        return 3;
    }

    var count = 0;
    input.EventReceived += (_, e) => { Console.WriteLine($"recv {e.Event}"); Interlocked.Increment(ref count); };
    using (input)
    {
        input.StartEventsListening();
        Thread.Sleep(ms);
        input.StopEventsListening();
    }

    Console.WriteLine($"received {count} event(s)");
    return count > 0 ? 0 : 1;
}

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: midisend <portName> <event> [<event> ...]   (or: midisend list)");
    return 2;
}

var portName = args[0];
OutputDevice device;
try
{
    device = OutputDevice.GetByName(portName);
}
catch (Exception)
{
    Console.Error.WriteLine($"MIDI出力ポート '{portName}' が見つかりません。loopMIDI でこの名前のポートを作成し、アプリがそれを認識しているか確認してください。");
    Console.Error.WriteLine("利用可能なポート:");
    foreach (var d in OutputDevice.GetAll())
    {
        Console.Error.WriteLine("  " + d.Name);
    }
    return 3;
}

static FourBitNumber Ch(string s) => (FourBitNumber)(int.Parse(s) - 1); // MIDI ch 1-16 -> 0-15
static SevenBitNumber Sb(string s) => (SevenBitNumber)int.Parse(s);

using (device)
{
    foreach (var spec in args.Skip(1))
    {
        var p = spec.Split(':');
        switch (p[0].ToLowerInvariant())
        {
            case "noteon":
                device.SendEvent(new NoteOnEvent(Sb(p[2]), Sb(p[3])) { Channel = Ch(p[1]) });
                break;
            case "noteoff":
                device.SendEvent(new NoteOffEvent(Sb(p[2]), (SevenBitNumber)0) { Channel = Ch(p[1]) });
                break;
            case "note":
                device.SendEvent(new NoteOnEvent(Sb(p[2]), Sb(p[3])) { Channel = Ch(p[1]) });
                Thread.Sleep(p.Length > 4 ? int.Parse(p[4]) : 150);
                device.SendEvent(new NoteOffEvent(Sb(p[2]), (SevenBitNumber)0) { Channel = Ch(p[1]) });
                break;
            case "cc":
                device.SendEvent(new ControlChangeEvent(Sb(p[2]), Sb(p[3])) { Channel = Ch(p[1]) });
                break;
            case "sleep":
                Thread.Sleep(int.Parse(p[1]));
                continue;
            default:
                Console.Error.WriteLine($"unknown event: {spec}");
                return 4;
        }

        Console.WriteLine($"sent {spec}");
        Thread.Sleep(120);
    }
}

return 0;
