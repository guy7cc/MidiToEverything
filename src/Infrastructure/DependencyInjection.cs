using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Infrastructure.Input;
using MidiToEverything.Infrastructure.Midi;
using MidiToEverything.Infrastructure.Window;

namespace MidiToEverything.Infrastructure;

/// <summary>Registers the Windows adapters behind the Core ports (docs/02_Architecture.md §2).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // MIDI input (DryWetMIDI).
        services.AddSingleton<DryWetMidiSource>(sp =>
            new DryWetMidiSource(sp.GetService<ILogger<DryWetMidiSource>>()));
        services.AddSingleton<IMidiSource>(sp => sp.GetRequiredService<DryWetMidiSource>());

        // Foreground-window monitoring (SetWinEventHook).
        services.AddSingleton<WinEventWindowWatcher>(sp =>
            new WinEventWindowWatcher(sp.GetService<ILogger<WinEventWindowWatcher>>()));
        services.AddSingleton<IWindowWatcher>(sp => sp.GetRequiredService<WinEventWindowWatcher>());

        // Foreground-window control (ShowWindow/SetWindowPos).
        services.AddSingleton<IWindowController>(_ => new Win32WindowController());

        // Shell launch + system audio (Phase 1 actions).
        services.AddSingleton<IShellLauncher>(sp =>
            new Shell.ShellLauncher(sp.GetService<ILogger<Shell.ShellLauncher>>()));
        services.AddSingleton<ISystemAudio>(sp =>
            new Audio.Win32SystemAudio(sp.GetService<ILogger<Audio.Win32SystemAudio>>()));

        // Windows setting toggles + display brightness (Phase 2 actions).
        services.AddSingleton<ISystemToggle>(sp =>
            new Settings.Win32SystemToggle(sp.GetService<ILogger<Settings.Win32SystemToggle>>()));
        services.AddSingleton<IDisplayBrightness>(sp =>
            new Display.WmiDisplayBrightness(sp.GetService<ILogger<Display.WmiDisplayBrightness>>()));

        // Network senders (Phase 3 actions): HTTP/webhook + OSC over UDP.
        services.AddSingleton<IHttpSender>(sp =>
            new Net.HttpSender(sp.GetService<ILogger<Net.HttpSender>>()));
        services.AddSingleton<IOscSender>(sp =>
            new Net.OscSender(sp.GetService<ILogger<Net.OscSender>>()));

        // MIDI output (DryWetMIDI) for MIDI-out actions / virtual ports.
        services.AddSingleton<IMidiOutput>(sp =>
            new Midi.DryWetMidiOutput(sp.GetService<ILogger<Midi.DryWetMidiOutput>>()));

        // Input emission (SendInput) behind an emergency-stop gate.
        services.AddSingleton<Win32InputSink>(sp =>
            new Win32InputSink(sp.GetService<ILogger<Win32InputSink>>()));
        services.AddSingleton<GatedInputSink>(sp =>
            new GatedInputSink(sp.GetRequiredService<Win32InputSink>()));
        services.AddSingleton<IInputSink>(sp => sp.GetRequiredService<GatedInputSink>());

        return services;
    }
}
