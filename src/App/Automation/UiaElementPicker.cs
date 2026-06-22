using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MidiToEverything.Core.Application.Ports;
using WinPoint = System.Windows.Point;
using FormsCursor = System.Windows.Forms.Cursor;
using UiaAutomation = System.Windows.Automation.Automation;

namespace MidiToEverything.App.Automation;

/// <summary>
/// Element picker: after a short delay (so the user hovers the target), captures the element under
/// the cursor and produces a window regex + element name for a <see cref="Core.Domain.UiaAction"/>.
/// </summary>
public sealed class UiaElementPicker : IUiaElementPicker
{
    private static readonly TimeSpan HoverDelay = TimeSpan.FromSeconds(3);

    private readonly ILogger<UiaElementPicker> _logger;

    public UiaElementPicker(ILogger<UiaElementPicker>? logger = null)
        => _logger = logger ?? NullLogger<UiaElementPicker>.Instance;

    public async Task<UiaPick?> PickAsync()
    {
        await Task.Delay(HoverDelay).ConfigureAwait(false);

        try
        {
            var pos = FormsCursor.Position;
            var element = AutomationElement.FromPoint(new WinPoint(pos.X, pos.Y));
            if (element is null)
            {
                return null;
            }

            var autoId = element.Current.AutomationId;
            var name = element.Current.Name ?? "";
            var elementName = !string.IsNullOrEmpty(autoId) ? autoId : name;
            if (string.IsNullOrWhiteSpace(elementName))
            {
                return null; // nothing addressable
            }

            var window = TopLevelWindow(element);
            var pattern = "";
            if (window is not null)
            {
                try
                {
                    var proc = Process.GetProcessById(window.Current.ProcessId).ProcessName;
                    if (!string.IsNullOrEmpty(proc))
                    {
                        pattern = "^" + Regex.Escape(proc); // match by process at start of "process\ntitle"
                    }
                }
                catch
                {
                    // leave pattern empty
                }
            }

            return new UiaPick(pattern, elementName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UIA pick failed");
            return null;
        }
    }

    private static AutomationElement? TopLevelWindow(AutomationElement element)
    {
        var walker = TreeWalker.ControlViewWalker;
        var node = element;
        while (node is not null)
        {
            var parent = walker.GetParent(node);
            if (parent is null || UiaAutomation.Compare(parent, AutomationElement.RootElement))
            {
                return node;
            }

            node = parent;
        }

        return null;
    }
}
