using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Mapping;
using static MidiToEverything.Core.Tests.Mapping.SchemaFixture;

namespace MidiToEverything.Core.Tests.Mapping;

/// <summary>
/// Encodes the conflict-resolution table in docs/03_ProfileSchema.md §6 plus the
/// layering rules in docs/02_Architecture.md §3.2.
/// </summary>
public class MappingResolverTests
{
    private readonly MappingResolver _resolver = new();

    private static KeyAction Key(MappingResolution r) =>
        Assert.IsType<KeyAction>(Assert.Single(r.Binding!.Actions));

    // ── Table row 1: no context match → base applies ──────────────────────────

    [Fact]
    public void Note36_NoContext_UsesBaseUndo()
    {
        var layers = new ProfileLayers(Base());

        var r = _resolver.Resolve(NoteOn(36), layers);

        Assert.Equal(ResolutionOutcome.Resolved, r.Outcome);
        Assert.Equal("base", r.SourceProfileId);
        Assert.Equal(new[] { "ctrl", "z" }, Key(r).Keys);
    }

    [Fact]
    public void Note37_NoContext_UsesBaseCopy()
    {
        var layers = new ProfileLayers(Base());

        var r = _resolver.Resolve(NoteOn(37), layers);

        Assert.Equal(ResolutionOutcome.Resolved, r.Outcome);
        Assert.Equal(new[] { "ctrl", "c" }, Key(r).Keys);
    }

    // ── Table row 2: Clip Studio Paint front ──────────────────────────────────

    [Fact]
    public void Note36_InClipStudio_FallsBackToBaseUndo()
    {
        // CSP defines nothing for Note36 → fall through to base (FR-6.3).
        var layers = new ProfileLayers(Base(), Context: ClipStudio());

        var r = _resolver.Resolve(NoteOn(36), layers);

        Assert.Equal(ResolutionOutcome.Resolved, r.Outcome);
        Assert.Equal("base", r.SourceProfileId);
        Assert.Equal(new[] { "ctrl", "z" }, Key(r).Keys);
    }

    [Fact]
    public void Note37_InClipStudio_IsBlocked()
    {
        // CSP blocks Note37 with NoneAction → no fallback, no emission (FR-6.4).
        var layers = new ProfileLayers(Base(), Context: ClipStudio());

        var r = _resolver.Resolve(NoteOn(37), layers);

        Assert.Equal(ResolutionOutcome.Blocked, r.Outcome);
        Assert.Equal("clip-studio", r.SourceProfileId);
        Assert.False(r.ShouldEmit);
        Assert.Null(r.Binding);
    }

    // ── Table row 3: OBS front ────────────────────────────────────────────────

    [Fact]
    public void Note36_InObs_IsOverridden()
    {
        // OBS overrides base Note36 (FR-6.2).
        var layers = new ProfileLayers(Base(), Context: Obs());

        var r = _resolver.Resolve(NoteOn(36), layers);

        Assert.Equal(ResolutionOutcome.Resolved, r.Outcome);
        Assert.Equal("obs", r.SourceProfileId);
        Assert.Equal(new[] { "ctrl", "shift", "1" }, Key(r).Keys);
    }

    [Fact]
    public void Note37_InObs_FallsBackToBaseCopy()
    {
        var layers = new ProfileLayers(Base(), Context: Obs());

        var r = _resolver.Resolve(NoteOn(37), layers);

        Assert.Equal(ResolutionOutcome.Resolved, r.Outcome);
        Assert.Equal("base", r.SourceProfileId);
        Assert.Equal(new[] { "ctrl", "c" }, Key(r).Keys);
    }

    // ── No match at all ───────────────────────────────────────────────────────

    [Fact]
    public void UnmappedSignal_ReturnsNoMatch()
    {
        var layers = new ProfileLayers(Base(), Context: ClipStudio());

        var r = _resolver.Resolve(NoteOn(99), layers);

        Assert.Equal(ResolutionOutcome.NoMatch, r.Outcome);
        Assert.False(r.ShouldEmit);
    }

    // ── Pinned layer beats context ────────────────────────────────────────────

    [Fact]
    public void PinnedProfile_TakesPriorityOverContext()
    {
        // Pin OBS while CSP is the foreground context: OBS wins for Note36.
        var layers = new ProfileLayers(Base(), Context: ClipStudio(), Pinned: Obs());

        var r = _resolver.Resolve(NoteOn(36), layers);

        Assert.Equal("obs", r.SourceProfileId);
        Assert.Equal(new[] { "ctrl", "shift", "1" }, Key(r).Keys);
    }

    // ── Disabled profile is skipped ───────────────────────────────────────────

    [Fact]
    public void DisabledContextProfile_IsSkipped()
    {
        var disabledObs = Obs() with { Enabled = false };
        var layers = new ProfileLayers(Base(), Context: disabledObs);

        var r = _resolver.Resolve(NoteOn(36), layers);

        Assert.Equal("base", r.SourceProfileId);
        Assert.Equal(new[] { "ctrl", "z" }, Key(r).Keys);
    }

    // ── CC continuous binding resolves in context ─────────────────────────────

    [Fact]
    public void Cc74_InClipStudio_ResolvesScroll()
    {
        var layers = new ProfileLayers(Base(), Context: ClipStudio());

        var r = _resolver.Resolve(Cc(74, 64), layers);

        Assert.Equal(ResolutionOutcome.Resolved, r.Outcome);
        Assert.IsType<ScrollAction>(Assert.Single(r.Binding!.Actions));
    }
}
