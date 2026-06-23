using System;
using System.Globalization;
using MidiToEverything.App.Localization;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.App.ViewModels.Editing;

/// <summary>
/// Builds a plain-language, localized description of how the current trigger settings make the
/// action fire — shown live in the editor's trigger panel so the numeric fields are easier to
/// understand. Each field is described by what it *does* (not just its value), and updates live
/// as the user edits it. Pure function of an <see cref="EditableBinding"/>; mirrors the engine
/// semantics in <c>TriggerEvaluator</c> / <c>EdgeGate</c>.
/// </summary>
internal static class TriggerHint
{
    public static string Describe(EditableBinding b) => b.Mode switch
    {
        TriggerMode.Trigger => DescribeTrigger(b),
        TriggerMode.Hold => Fmt("hint.hold", b.Threshold),
        TriggerMode.Absolute => DescribeAbsolute(b),
        TriggerMode.Relative => DescribeRelative(b),
        _ => "",
    };

    /// <summary>Label next to the trigger→action arrow: what the trigger sends (fire vs amount).</summary>
    public static string FlowLabel(EditableBinding b) => b.Mode switch
    {
        TriggerMode.Absolute => Loc.T("flow.amount"),
        TriggerMode.Relative => b.RelativeOutput == RelativeOutput.Amount ? Loc.T("flow.delta") : Loc.T("flow.fire"),
        _ => Loc.T("flow.fire"), // Trigger / Hold
    };

    private static string DescribeTrigger(EditableBinding b)
    {
        // Trigger fires discretely on threshold only; range/deadzone/scale don't apply here.
        var s = Fmt("hint.trigger", b.Threshold);
        s += b.Edge ? Loc.T("hint.edge") : Loc.T("hint.repeat");
        return s;
    }

    private static string DescribeAbsolute(EditableBinding b)
    {
        var min = Math.Min(b.RangeMin, b.RangeMax);
        var max = Math.Max(b.RangeMin, b.RangeMax);
        var gate = b.OutOfRange == OutOfRangeBehavior.Gate;

        // Step-by-step conversion to a 0..100 action amount (live values, incl. the dead-zone edges).
        var s = Fmt(gate ? "hint.abs.gate" : "hint.abs.map", min, max);
        if (b.Deadzone > 0)
        {
            s += Fmt("hint.abs.dz", min + b.Deadzone, max - b.Deadzone);
        }

        if (b.Invert)
        {
            s += Loc.T("hint.abs.invert");
        }

        if (!Near1(b.Scale))
        {
            s += Fmt("hint.abs.scale", Scale(b));
        }

        if (gate && b.Edge)
        {
            s += Loc.T("hint.edge");
        }

        return s;
    }

    private static string DescribeRelative(EditableBinding b)
    {
        // 1) How the delta is read: an encoder encoding, or the change of an absolute value.
        var s = b.RelativeFormat == RelativeFormat.AbsoluteDelta
            ? Loc.T("hint.rel.source.absdelta")
            : Loc.T("hint.rel.source.encoder");
        if (b.RelativeFormat == RelativeFormat.AbsoluteDelta && b.Wrap)
        {
            s += Loc.T("hint.rel.wrap");
        }

        // 2) What the delta does: send as amount, or fire on a direction.
        s += b.RelativeOutput switch
        {
            RelativeOutput.FireOnIncrease => Loc.T("hint.rel.out.increase"),
            RelativeOutput.FireOnDecrease => Loc.T("hint.rel.out.decrease"),
            _ => Loc.T("hint.rel.out.amount"),
        };

        if (b.Invert)
        {
            s += Loc.T("hint.invert");
        }

        // Dead zone (Relative): turns up to this size are ignored (jitter filter).
        s += b.Deadzone > 0 ? Fmt("hint.dz.rel", b.Deadzone) : Loc.T("hint.dz.rel.none");

        // Scale only matters when the delta becomes an amount.
        if (b.RelativeOutput == RelativeOutput.Amount)
        {
            s += ScaleRelative(b);
        }

        return s;
    }

    // Relative (amount output) scales each tick's movement (Absolute builds its own scale
    // clause inline, since it multiplies the 0..100 amount and clamps at 100).
    private static string ScaleRelative(EditableBinding b)
        => Near1(b.Scale) ? Loc.T("hint.scale.rel.one") : Fmt("hint.scale.rel", Scale(b));

    private static bool Near1(double scale) => Math.Abs(scale - 1.0) < 0.0001;

    /// <summary>Scale rendered compactly (1, 1.5, 2 …) so it reads naturally in the sentence.</summary>
    private static string Scale(EditableBinding b) => b.Scale.ToString("0.##", CultureInfo.CurrentCulture);

    private static string Fmt(string key, params object[] args)
        => string.Format(CultureInfo.CurrentCulture, Loc.T(key), args);
}
