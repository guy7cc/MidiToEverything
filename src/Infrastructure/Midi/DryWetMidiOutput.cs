using System.Text.RegularExpressions;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.Infrastructure.Midi;

/// <summary>
/// DryWetMIDI adapter for <see cref="IMidiOutput"/> (docs/05 §5, Phase 3). Resolves the output
/// device by a name regex and caches the opened device; on send failure the cache entry is
/// dropped so the next send re-resolves (e.g. after a virtual port reappears).
/// </summary>
public sealed class DryWetMidiOutput : IMidiOutput, IDisposable
{
    private readonly Dictionary<string, OutputDevice> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private readonly ILogger<DryWetMidiOutput> _logger;

    public DryWetMidiOutput(ILogger<DryWetMidiOutput>? logger = null)
        => _logger = logger ?? NullLogger<DryWetMidiOutput>.Instance;

    public void Send(string devicePattern, MidiOutKind kind, int channel, int data1, int data2)
    {
        try
        {
            var device = Resolve(devicePattern);
            if (device is null)
            {
                _logger.LogWarning("MIDI out: no device matched '{Pattern}'", devicePattern);
                return;
            }

            device.SendEvent(Build(kind, channel, data1, data2));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MIDI out send to '{Pattern}' failed", devicePattern);
            Invalidate(devicePattern);
        }
    }

    private OutputDevice? Resolve(string pattern)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(pattern, out var cached))
            {
                return cached;
            }

            OutputDevice? chosen = null;
            foreach (var device in OutputDevice.GetAll())
            {
                bool match;
                try
                {
                    match = string.IsNullOrWhiteSpace(pattern) || pattern == "*"
                            || Regex.IsMatch(device.Name, pattern, RegexOptions.IgnoreCase);
                }
                catch (ArgumentException)
                {
                    device.Dispose();
                    chosen?.Dispose();
                    return null; // invalid regex matches nothing
                }

                if (match && chosen is null)
                {
                    chosen = device;
                }
                else
                {
                    device.Dispose();
                }
            }

            if (chosen is not null)
            {
                _cache[pattern] = chosen; // cache only hits, so unplugged devices are retried
            }

            return chosen;
        }
    }

    private void Invalidate(string pattern)
    {
        lock (_lock)
        {
            if (_cache.Remove(pattern, out var device))
            {
                device.Dispose();
            }
        }
    }

    private static MidiEvent Build(MidiOutKind kind, int channel, int data1, int data2)
    {
        var ch = (FourBitNumber)(byte)Math.Clamp(channel - 1, 0, 15);
        var d1 = (SevenBitNumber)(byte)Math.Clamp(data1, 0, 127);
        var d2 = (SevenBitNumber)(byte)Math.Clamp(data2, 0, 127);

        ChannelEvent ev = kind switch
        {
            MidiOutKind.NoteOn => new NoteOnEvent(d1, d2),
            MidiOutKind.NoteOff => new NoteOffEvent(d1, d2),
            MidiOutKind.ProgramChange => new ProgramChangeEvent(d1),
            _ => new ControlChangeEvent(d1, d2),
        };
        ev.Channel = ch;
        return ev;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var device in _cache.Values)
            {
                device.Dispose();
            }

            _cache.Clear();
        }
    }
}
