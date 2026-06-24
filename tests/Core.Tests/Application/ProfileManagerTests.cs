using System.Text.RegularExpressions;
using MidiToEverything.Core.Application;
using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Tests.Fakes;

namespace MidiToEverything.Core.Tests.Application;

public class ProfileManagerTests
{
    private static Profile Base() => new() { Id = "base", Name = "Base" };

    private static Profile App(string id, string process, int priority = 0) => new()
    {
        Id = id,
        Name = id,
        Match = new MatchRule { Pattern = "^" + Regex.Escape(process) + "$", Priority = priority },
    };

    private static AppConfig Config(params Profile[] profiles) => new()
    {
        BaseProfile = Base(),
        Profiles = profiles,
    };

    private static (ProfileManager manager, FakeWindowWatcher watcher) Build(params Profile[] profiles)
    {
        var watcher = new FakeWindowWatcher();
        var manager = new ProfileManager(Config(profiles), watcher);
        manager.Start();
        return (manager, watcher);
    }

    [Fact]
    public void Foreground_AutoSelectsMatchingProfile()
    {
        var (manager, watcher) = Build(App("notepad", "notepad.exe"));

        watcher.SetForeground("notepad.exe", "Untitled");

        Assert.Equal("notepad", manager.State.Effective.Id);
        Assert.Contains(manager.State.Active, p => p.Id == "notepad"); // the matched rule is active
        Assert.Contains(manager.Current.Rules, p => p.Id == "notepad");
    }

    [Fact]
    public void Foreground_NoMatch_FallsBackToBaseOnly()
    {
        var (manager, watcher) = Build(App("notepad", "notepad.exe"));

        watcher.SetForeground("explorer.exe", "Files");

        Assert.Single(manager.State.Active); // only the base rule is active
        Assert.Equal("base", manager.State.Effective.Id);
    }

    [Fact]
    public void Foreground_HigherPriorityWins()
    {
        var (manager, watcher) = Build(
            App("low", "app.exe", priority: 1),
            App("high", "app.exe", priority: 10));

        watcher.SetForeground("app.exe", "");

        Assert.Equal("high", manager.State.Effective.Id);           // highest-priority match for display
        Assert.Contains(manager.State.Active, p => p.Id == "low");  // but both rules are active (union)
        Assert.Contains(manager.State.Active, p => p.Id == "high");
    }

    [Fact]
    public void ManualPin_OverridesAutoSwitch()
    {
        var (manager, watcher) = Build(App("notepad", "notepad.exe"), App("obs", "obs64.exe"));
        watcher.SetForeground("notepad.exe", "");

        manager.Pin("obs");

        Assert.True(manager.State.IsPinned);
        Assert.Equal("obs", manager.State.Effective.Id);
        // the window match stays active underneath the force-enable
        Assert.Contains(manager.State.Active, p => p.Id == "notepad");
    }

    [Fact]
    public void Unpin_ResumesAutoSwitch()
    {
        var (manager, watcher) = Build(App("notepad", "notepad.exe"), App("obs", "obs64.exe"));
        watcher.SetForeground("notepad.exe", "");
        manager.Pin("obs");

        manager.Unpin();

        Assert.False(manager.State.IsPinned);
        Assert.Equal("notepad", manager.State.Effective.Id);
    }

    [Fact]
    public void SwitchNext_CyclesAndPins()
    {
        var (manager, _) = Build(App("a", "a.exe"), App("b", "b.exe"), App("c", "c.exe"));

        manager.HandleSwitch(new SwitchProfileAction(ProfileSwitchTarget.Next));
        Assert.Equal("a", manager.State.Effective.Id);

        manager.HandleSwitch(new SwitchProfileAction(ProfileSwitchTarget.Next));
        Assert.Equal("b", manager.State.Effective.Id);
    }

    [Fact]
    public void SwitchPrevious_WrapsAround()
    {
        var (manager, _) = Build(App("a", "a.exe"), App("b", "b.exe"));

        manager.HandleSwitch(new SwitchProfileAction(ProfileSwitchTarget.Previous));

        Assert.Equal("b", manager.State.Effective.Id); // -1 from "none" wraps to last
    }

    [Fact]
    public void SwitchSpecific_PinsThatProfile()
    {
        var (manager, _) = Build(App("a", "a.exe"), App("b", "b.exe"));

        manager.HandleSwitch(new SwitchProfileAction(ProfileSwitchTarget.Specific, "b"));

        Assert.Equal("b", manager.State.Effective.Id);
        Assert.True(manager.State.IsPinned);
    }

    [Fact]
    public void Toggle_PinsContextThenReleases()
    {
        var (manager, watcher) = Build(App("notepad", "notepad.exe"));
        watcher.SetForeground("notepad.exe", "");

        manager.HandleSwitch(new SwitchProfileAction(ProfileSwitchTarget.Toggle));
        Assert.True(manager.State.IsPinned);
        Assert.Equal("notepad", manager.State.Effective.Id);

        manager.HandleSwitch(new SwitchProfileAction(ProfileSwitchTarget.Toggle));
        Assert.False(manager.State.IsPinned);
    }

    [Fact]
    public void Reload_AppliesNewProfilesAndReevaluatesContext()
    {
        var (manager, watcher) = Build(App("notepad", "notepad.exe"));
        watcher.SetForeground("obs64.exe", "");
        Assert.Equal("base", manager.State.Effective.Id); // no rule matches obs yet (base only)

        manager.Reload(new AppConfig
        {
            BaseProfile = Base(),
            Profiles = new[] { App("obs", "obs64.exe") },
        });

        Assert.Equal("obs", manager.State.Effective.Id); // re-evaluated against the current window
        Assert.Single(manager.CurrentConfig.Profiles);
    }

    [Fact]
    public void Reload_ClearsPinPointingAtRemovedProfile()
    {
        var (manager, _) = Build(App("a", "a.exe"), App("b", "b.exe"));
        manager.Pin("b");
        Assert.True(manager.State.IsPinned);

        manager.Reload(new AppConfig { BaseProfile = Base(), Profiles = new[] { App("a", "a.exe") } });

        Assert.False(manager.State.IsPinned); // "b" no longer exists
    }

    [Fact]
    public void Changed_RaisedOnForegroundAndManualSwitch()
    {
        var (manager, watcher) = Build(App("notepad", "notepad.exe"));
        var events = new List<ActiveProfileState>();
        manager.Changed += (_, s) => events.Add(s);

        watcher.SetForeground("notepad.exe", "");
        manager.Pin("notepad");

        Assert.Equal(2, events.Count);
        Assert.Equal("notepad", events[^1].Effective.Id);
    }
}
