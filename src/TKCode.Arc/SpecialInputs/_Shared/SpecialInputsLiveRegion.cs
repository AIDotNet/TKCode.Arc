using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc;

/// <summary>
/// A visually hidden live region for the special inputs (Arc's <c>aria-live</c> / <c>role="status"</c> span): the
/// announced text is separate from what is on screen, and repeating the same message toggles a zero-width space so it
/// is announced again. Polite by default; <see cref="IsAssertive"/> switches to assertive.
/// </summary>
internal sealed class SpecialInputsLiveRegion : TextBlock
{
    private int _count;

    public SpecialInputsLiveRegion()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        IsHitTestVisible = false;
        Focusable = false;
        Opacity = 0;
        Width = 1;
        Height = 1;
        ClipToBounds = true;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
    }

    /// <summary>The last message announced (without the repeat marker).</summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>Announces assertively instead of politely.</summary>
    public bool IsAssertive
    {
        get => AutomationProperties.GetLiveSetting(this) == AutomationLiveSetting.Assertive;
        set => AutomationProperties.SetLiveSetting(this, value ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
    }

    /// <summary>Announces <paramref name="message"/>; null or empty clears the region silently.</summary>
    public void Announce(string? message)
    {
        Message = message ?? string.Empty;
        if (string.IsNullOrEmpty(Message))
        {
            Text = string.Empty;
            return;
        }
        _count++;
        Text = _count % 2 == 0 ? Message + "​" : Message;
    }
}
