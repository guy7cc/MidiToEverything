using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.App.Automation;

/// <summary>
/// UI Automation client adapter for <see cref="IUiaDriver"/> (docs/05 §5, Phase 2). Finds the
/// target top-level window by a "process\ntitle" regex, locates the element by Name/AutomationId,
/// and actuates it (Invoke / Toggle / SetValue).
/// </summary>
public sealed class UiaDriver : IUiaDriver
{
    private readonly ILogger<UiaDriver> _logger;

    public UiaDriver(ILogger<UiaDriver>? logger = null)
        => _logger = logger ?? NullLogger<UiaDriver>.Instance;

    public void Actuate(string windowPattern, string elementName, UiaVerb verb, string? value)
    {
        try
        {
            var window = FindWindow(windowPattern);
            if (window is null)
            {
                _logger.LogWarning("UIA: no window matched '{Pattern}'", windowPattern);
                return;
            }

            var byName = new PropertyCondition(AutomationElement.NameProperty, elementName);
            var byId = new PropertyCondition(AutomationElement.AutomationIdProperty, elementName);
            var element = window.FindFirst(TreeScope.Descendants, new OrCondition(byName, byId));
            if (element is null)
            {
                _logger.LogWarning("UIA: element '{Name}' not found in target window", elementName);
                return;
            }

            Apply(element, verb, value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UIA actuate failed");
        }
    }

    private static AutomationElement? FindWindow(string pattern)
    {
        var windows = AutomationElement.RootElement.FindAll(TreeScope.Children, Condition.TrueCondition);
        foreach (AutomationElement w in windows)
        {
            string proc;
            try
            {
                proc = Process.GetProcessById(w.Current.ProcessId).ProcessName;
            }
            catch
            {
                proc = "";
            }

            var target = $"{proc}\n{w.Current.Name ?? ""}";
            try
            {
                if (string.IsNullOrWhiteSpace(pattern) || Regex.IsMatch(target, pattern, RegexOptions.IgnoreCase))
                {
                    return w;
                }
            }
            catch (ArgumentException)
            {
                return null; // invalid regex matches nothing
            }
        }

        return null;
    }

    private void Apply(AutomationElement element, UiaVerb verb, string? value)
    {
        switch (verb)
        {
            case UiaVerb.Invoke when element.TryGetCurrentPattern(InvokePattern.Pattern, out var inv):
                ((InvokePattern)inv).Invoke();
                break;
            case UiaVerb.Toggle when element.TryGetCurrentPattern(TogglePattern.Pattern, out var tog):
                ((TogglePattern)tog).Toggle();
                break;
            case UiaVerb.SetValue when element.TryGetCurrentPattern(ValuePattern.Pattern, out var val):
                ((ValuePattern)val).SetValue(value ?? "");
                break;
            default:
                _logger.LogWarning("UIA: element does not support {Verb}", verb);
                break;
        }
    }
}
