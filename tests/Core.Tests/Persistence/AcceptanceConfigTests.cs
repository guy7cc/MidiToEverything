using MidiToEverything.Core.Persistence;

namespace MidiToEverything.Core.Tests.Persistence;

/// <summary>
/// Guards the action-acceptance runbook config (.claude/skills/test-actions/test-config.json) against
/// schema drift: it must deserialize cleanly and exercise every concrete action type, so following the
/// runbook actually tests every action (not silently falling back to defaults on a bad type).
/// </summary>
public class AcceptanceConfigTests
{
    [Fact]
    public void AcceptanceConfig_Deserializes_AndCoversAllActionTypes()
    {
        var config = ConfigSerializer.Deserialize(File.ReadAllText(RepoPaths.AcceptanceConfig));

        Assert.NotNull(config.BaseProfile);
        Assert.True(config.BaseProfile.Bindings.Count >= 22, "expected one binding per action type, plus extras");
        Assert.All(config.BaseProfile.Bindings, b => Assert.NotEmpty(b.Actions));

        var actionTypes = config.BaseProfile.Bindings
            .SelectMany(b => b.Actions)
            .Select(a => a.GetType().Name)
            .ToHashSet();

        // Every concrete action handler type must be represented (discriminators parsed correctly).
        string[] expected =
        {
            "KeyAction", "TypeTextAction", "MouseClickAction", "CursorMoveAction", "ScrollAction",
            "MediaKeyAction", "WindowControlAction", "LaunchAction", "SetVolumeAction", "UiaAction",
            "VirtualDesktopAction", "WindowsToggleAction", "BrightnessAction", "HttpAction", "OscAction",
            "ObsAction", "MidiOutAction", "MacroAction", "ToggleAction", "PluginAction",
            "SwitchProfileAction", "NoneAction",
        };
        foreach (var name in expected)
        {
            Assert.Contains(name, actionTypes);
        }
    }
}
