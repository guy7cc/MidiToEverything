using MidiToEverything.Core.Application;
using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Mapping;
using MidiToEverything.Core.Persistence;

namespace MidiToEverything.Core.Tests.Persistence;

public class ConfigSerializerTests
{
    private static MidiMessage NoteOn(int number, string device = "akai", int channel = 1)
        => new(device, channel, MidiMessageType.NoteOn, number, 100);

    private static AppConfig SingleBinding(Signal signal, params InputAction[] actions) => new()
    {
        BaseProfile = new Profile
        {
            Id = "base",
            Name = "base",
            Bindings = new[] { new Binding { Signal = signal, Actions = actions } },
        },
    };

    [Fact]
    public void DefaultConfig_RoundTrips_PreservingResolutionBehavior()
    {
        var loaded = ConfigSerializer.Deserialize(ConfigSerializer.Serialize(DefaultConfig.Create()));

        var resolver = new MappingResolver();
        var csp = loaded.Profiles.Single(p => p.Id == "clip-studio");
        var obs = loaded.Profiles.Single(p => p.Id == "obs");

        // base undo with no context
        Assert.Equal("base",
            resolver.Resolve(NoteOn(36), new ProfileLayers(loaded.BaseProfile)).SourceProfileId);
        // CSP blocks Note37 (NoneAction survived the round-trip)
        Assert.Equal(ResolutionOutcome.Blocked,
            resolver.Resolve(NoteOn(37), new ProfileLayers(loaded.BaseProfile, Context: csp)).Outcome);
        // OBS overrides Note36
        var obsHit = resolver.Resolve(NoteOn(36), new ProfileLayers(loaded.BaseProfile, Context: obs));
        Assert.Equal("obs", obsHit.SourceProfileId);
        Assert.Equal(new[] { "ctrl", "shift", "1" }, Assert.IsType<KeyAction>(obsHit.Binding!.Actions[0]).Keys);
    }

    [Fact]
    public void Serialization_IsIdempotent()
    {
        var first = ConfigSerializer.Serialize(DefaultConfig.Create());
        var second = ConfigSerializer.Serialize(ConfigSerializer.Deserialize(first));

        Assert.Equal(first, second);
    }

    [Fact]
    public void NumericChannel_SerializesAsNumber_AndRoundTrips()
    {
        var config = SingleBinding(
            new Signal { Channel = "2", Type = SignalKind.NoteOn, Number = 36 },
            new KeyAction(new[] { "a" }));

        var json = ConfigSerializer.Serialize(config);
        Assert.Contains("\"channel\": 2", json);

        var loaded = ConfigSerializer.Deserialize(json);
        Assert.Equal("2", loaded.BaseProfile.Bindings[0].Signal.Channel);
    }

    [Fact]
    public void AnyChannel_SerializesAsString()
    {
        var config = SingleBinding(
            new Signal { Channel = Signal.AnyChannel, Type = SignalKind.NoteOn, Number = 36 },
            new KeyAction(new[] { "a" }));

        Assert.Contains("\"channel\": \"any\"", ConfigSerializer.Serialize(config));
    }

    [Theory]
    [InlineData(ProfileSwitchTarget.Next, null, "next")]
    [InlineData(ProfileSwitchTarget.Previous, null, "prev")]
    [InlineData(ProfileSwitchTarget.Toggle, null, "toggle")]
    [InlineData(ProfileSwitchTarget.Specific, "obs", "obs")]
    public void SwitchProfileTarget_RoundTrips(ProfileSwitchTarget target, string? id, string expectedJsonTarget)
    {
        var config = SingleBinding(
            new Signal { Type = SignalKind.NoteOn, Number = 51 },
            new SwitchProfileAction(target, id));

        var json = ConfigSerializer.Serialize(config);
        Assert.Contains($"\"target\": \"{expectedJsonTarget}\"", json);

        var loaded = ConfigSerializer.Deserialize(json);
        var action = Assert.IsType<SwitchProfileAction>(loaded.BaseProfile.Bindings[0].Actions[0]);
        Assert.Equal(target, action.Target);
        if (target == ProfileSwitchTarget.Specific)
        {
            Assert.Equal(id, action.ProfileId);
        }
    }

    [Theory]
    [InlineData(WindowOp.Minimize)]
    [InlineData(WindowOp.Maximize)]
    [InlineData(WindowOp.Close)]
    [InlineData(WindowOp.ToggleTopMost)]
    public void WindowControlAction_RoundTrips(WindowOp op)
    {
        var config = SingleBinding(
            new Signal { Type = SignalKind.NoteOn, Number = 60 },
            new WindowControlAction(op));

        var json = ConfigSerializer.Serialize(config);
        Assert.Contains("\"type\": \"windowControl\"", json);

        var loaded = ConfigSerializer.Deserialize(json);
        var action = Assert.IsType<WindowControlAction>(loaded.BaseProfile.Bindings[0].Actions[0]);
        Assert.Equal(op, action.Op);
    }

    [Fact]
    public void NewActions_RoundTrip()
    {
        var sig = new Signal { Type = SignalKind.NoteOn, Number = 60 };

        Assert.IsType<MediaKeyAction>(RoundTripAction(sig, new MediaKeyAction(MediaKey.Mute)));
        Assert.Equal("hello world", Assert.IsType<TypeTextAction>(
            RoundTripAction(sig, new TypeTextAction("hello world"))).Text);

        var launch = Assert.IsType<LaunchAction>(
            RoundTripAction(sig, new LaunchAction("notepad.exe", "a.txt", @"C:\tmp")));
        Assert.Equal("notepad.exe", launch.Target);
        Assert.Equal("a.txt", launch.Arguments);
        Assert.Equal(@"C:\tmp", launch.WorkingDir);

        Assert.Equal(VolumeTarget.Microphone, Assert.IsType<SetVolumeAction>(
            RoundTripAction(sig, new SetVolumeAction(VolumeTarget.Microphone))).Target);

        var uia = Assert.IsType<UiaAction>(
            RoundTripAction(sig, new UiaAction("^notepad", "OK", UiaVerb.SetValue, "hi")));
        Assert.Equal("^notepad", uia.WindowPattern);
        Assert.Equal("OK", uia.ElementName);
        Assert.Equal(UiaVerb.SetValue, uia.Verb);
        Assert.Equal("hi", uia.Value);

        Assert.Equal(DesktopOp.Previous, Assert.IsType<VirtualDesktopAction>(
            RoundTripAction(sig, new VirtualDesktopAction(DesktopOp.Previous))).Op);
        Assert.Equal(WindowsSetting.DarkMode, Assert.IsType<WindowsToggleAction>(
            RoundTripAction(sig, new WindowsToggleAction(WindowsSetting.DarkMode))).Setting);
        Assert.IsType<BrightnessAction>(RoundTripAction(sig, new BrightnessAction()));

        var http = Assert.IsType<HttpAction>(
            RoundTripAction(sig, new HttpAction("http://h/a", "POST", "{}")));
        Assert.Equal("http://h/a", http.Url);
        Assert.Equal("POST", http.Method);
        Assert.Equal("{}", http.Body);

        var osc = Assert.IsType<OscAction>(
            RoundTripAction(sig, new OscAction("127.0.0.1:9000", "/fader", "0.5")));
        Assert.Equal("127.0.0.1:9000", osc.Target);
        Assert.Equal("/fader", osc.Address);
        Assert.Equal("0.5", osc.Args);

        var obs = Assert.IsType<ObsAction>(
            RoundTripAction(sig, new ObsAction(ObsOp.SceneSwitch, "Intro")));
        Assert.Equal(ObsOp.SceneSwitch, obs.Op);
        Assert.Equal("Intro", obs.Arg);

        var midi = Assert.IsType<MidiOutAction>(
            RoundTripAction(sig, new MidiOutAction("^loopMIDI", MidiOutKind.NoteOn, 2, 60, 100, UseInputValue: true)));
        Assert.Equal("^loopMIDI", midi.Device);
        Assert.Equal(MidiOutKind.NoteOn, midi.Kind);
        Assert.Equal(2, midi.Channel);
        Assert.Equal(60, midi.Data1);
        Assert.Equal(100, midi.Data2);
        Assert.True(midi.UseInputValue);

        var macro = Assert.IsType<MacroAction>(RoundTripAction(sig,
            new MacroAction(new IReadOnlyList<string>[] { new[] { "ctrl", "c" }, new[] { "enter" } }, 50)));
        Assert.Equal(2, macro.Steps.Count);
        Assert.Equal(new[] { "ctrl", "c" }, macro.Steps[0]);
        Assert.Equal(50, macro.StepDelayMs);

        var toggle = Assert.IsType<ToggleAction>(RoundTripAction(sig,
            new ToggleAction(new[] { "a" }, new[] { "b" }, "loopMIDI", 2, 36)));
        Assert.Equal(new[] { "a" }, toggle.KeysA);
        Assert.Equal(new[] { "b" }, toggle.KeysB);
        Assert.Equal("loopMIDI", toggle.LedDevice);
        Assert.Equal(2, toggle.LedChannel);
        Assert.Equal(36, toggle.LedNote);

        var plugin = Assert.IsType<PluginAction>(
            RoundTripAction(sig, new PluginAction("my-plugin", "do", "arg1")));
        Assert.Equal("my-plugin", plugin.PluginId);
        Assert.Equal("do", plugin.Command);
        Assert.Equal("arg1", plugin.Arg);
    }

    [Fact]
    public void RelativeOutput_RoundTrips()
    {
        var config = SingleBinding(new Signal { Type = SignalKind.Cc, Number = 74 }, new KeyAction(new[] { "a" }));
        config = config with
        {
            BaseProfile = config.BaseProfile with
            {
                Bindings = new[]
                {
                    config.BaseProfile.Bindings[0] with
                    {
                        Trigger = new Trigger { Mode = TriggerMode.Relative, RelativeOutput = RelativeOutput.FireOnIncrease },
                    },
                },
            },
        };

        var json = ConfigSerializer.Serialize(config);
        Assert.Contains("\"relativeOutput\": \"fireOnIncrease\"", json);

        var trigger = ConfigSerializer.Deserialize(json).BaseProfile.Bindings[0].Trigger;
        Assert.Equal(RelativeOutput.FireOnIncrease, trigger.RelativeOutput);
    }

    [Fact]
    public void LegacyRelativeFromAbsolute_MigratesToRelativeAbsoluteDelta()
    {
        // The old standalone mode is migrated to Relative + AbsoluteDelta on read.
        var json = """
        {
          "version": 2,
          "baseProfile": {
            "id": "base", "name": "b",
            "bindings": [ {
              "signal": { "type": "cc", "number": 74 },
              "trigger": { "mode": "relativeFromAbsolute", "wrap": true },
              "actions": [ { "type": "scroll" } ]
            } ]
          }
        }
        """;

        var trigger = ConfigSerializer.Deserialize(json).BaseProfile.Bindings[0].Trigger;
        Assert.Equal(TriggerMode.Relative, trigger.Mode);
        Assert.Equal(RelativeFormat.AbsoluteDelta, trigger.RelativeFormat);
        Assert.True(trigger.Wrap);
    }

    [Fact]
    public void ObsConnectionSettings_RoundTrip()
    {
        var config = DefaultConfig.Create();
        config = config with { Settings = config.Settings with { ObsHost = "10.0.0.2", ObsPort = 4456, ObsPassword = "pw" } };

        var loaded = ConfigSerializer.Deserialize(ConfigSerializer.Serialize(config));

        Assert.Equal("10.0.0.2", loaded.Settings.ObsHost);
        Assert.Equal(4456, loaded.Settings.ObsPort);
        Assert.Equal("pw", loaded.Settings.ObsPassword);
    }

    [Fact]
    public void AllowExternalLaunch_RoundTrips()
    {
        var config = DefaultConfig.Create();
        config = config with { Settings = config.Settings with { AllowExternalLaunch = true } };

        var loaded = ConfigSerializer.Deserialize(ConfigSerializer.Serialize(config));

        Assert.True(loaded.Settings.AllowExternalLaunch);
    }

    private static InputAction RoundTripAction(Signal signal, InputAction action)
    {
        var loaded = ConfigSerializer.Deserialize(ConfigSerializer.Serialize(SingleBinding(signal, action)));
        return loaded.BaseProfile.Bindings[0].Actions[0];
    }

    [Fact]
    public void ContinuousTrigger_RoundTrips_RangeAndMode()
    {
        var config = SingleBinding(
            new Signal { Type = SignalKind.Cc, Number = 74 },
            new ScrollAction());
        config = config with
        {
            BaseProfile = config.BaseProfile with
            {
                Bindings = new[]
                {
                    config.BaseProfile.Bindings[0] with
                    {
                        Trigger = new Trigger { Mode = TriggerMode.Absolute, RangeMin = 10, RangeMax = 120, Scale = 2.5 },
                    },
                },
            },
        };

        var trigger = ConfigSerializer.Deserialize(ConfigSerializer.Serialize(config))
            .BaseProfile.Bindings[0].Trigger;

        Assert.Equal(TriggerMode.Absolute, trigger.Mode);
        Assert.Equal(10, trigger.RangeMin);
        Assert.Equal(120, trigger.RangeMax);
        Assert.Equal(2.5, trigger.Scale, 3);
        Assert.Equal(OutOfRangeBehavior.Clamp, trigger.OutOfRange); // default preserved
    }

    [Fact]
    public void AbsoluteGate_RoundTrips_OutOfRangeBehavior()
    {
        var config = SingleBinding(new Signal { Type = SignalKind.Cc, Number = 74 }, new KeyAction(new[] { "a" }));
        config = config with
        {
            BaseProfile = config.BaseProfile with
            {
                Bindings = new[]
                {
                    config.BaseProfile.Bindings[0] with
                    {
                        Trigger = new Trigger
                        {
                            Mode = TriggerMode.Absolute,
                            RangeMin = 60,
                            RangeMax = 70,
                            OutOfRange = OutOfRangeBehavior.Gate,
                        },
                    },
                },
            },
        };

        var json = ConfigSerializer.Serialize(config);
        Assert.Contains("\"outOfRange\": \"gate\"", json);

        var trigger = ConfigSerializer.Deserialize(json).BaseProfile.Bindings[0].Trigger;
        Assert.Equal(OutOfRangeBehavior.Gate, trigger.OutOfRange);
        Assert.Equal(60, trigger.RangeMin);
        Assert.Equal(70, trigger.RangeMax);
    }

    [Fact]
    public void V1Config_MigratesProcessNamesAndTitleToUnifiedPattern()
    {
        // A version-1 config using the old processNames/titlePattern fields.
        var json = """
        {
          "version": 1,
          "baseProfile": { "id": "base", "name": "b" },
          "profiles": [
            { "id": "csp", "name": "CSP",
              "match": { "processNames": ["CLIPStudioPaint.exe"], "priority": 7 } }
          ]
        }
        """;

        var config = ConfigSerializer.Deserialize(json);

        Assert.Equal(2, config.Version);
        var match = config.Profiles.Single().Match!;
        Assert.Equal(7, match.Priority);
        Assert.True(match.Matches("CLIPStudioPaint.exe", "anything"));
        Assert.False(match.Matches("chrome.exe", "anything"));
    }

    [Fact]
    public void NewerSchemaVersion_Throws()
    {
        var json = "{ \"version\": 999, \"baseProfile\": { \"id\": \"base\", \"name\": \"b\" } }";

        Assert.Throws<NotSupportedException>(() => ConfigSerializer.Deserialize(json));
    }

    [Fact]
    public void EnumValues_SerializeAsCamelCaseStrings()
    {
        var json = ConfigSerializer.Serialize(DefaultConfig.Create());

        Assert.Contains("\"type\": \"noteOn\"", json);
        Assert.Contains("\"type\": \"cc\"", json);
        Assert.Contains("\"type\": \"key\"", json);     // action discriminator
        Assert.Contains("\"type\": \"none\"", json);    // block action
    }
}
