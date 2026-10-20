using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc;

/// <summary>
/// A visually hidden live region for the editors' result announcements (Arc's <c>aria-live</c> spans): polite for
/// results, assertive for refusals. Repeating a message toggles a zero width space so it is read again.
/// </summary>
internal sealed class EditorsLiveRegion : TextBlock
{
    public static readonly StyledProperty<bool> IsAssertiveProperty =
        AvaloniaProperty.Register<EditorsLiveRegion, bool>(nameof(IsAssertive));

    private int _count;

    public EditorsLiveRegion()
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

    /// <summary>Assertive regions interrupt (refusals); polite ones wait (results).</summary>
    public bool IsAssertive { get => GetValue(IsAssertiveProperty); set => SetValue(IsAssertiveProperty, value); }

    /// <summary>The last message announced (without the repeat marker).</summary>
    public string Message { get; private set; } = string.Empty;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsAssertiveProperty)
        {
            AutomationProperties.SetLiveSetting(this, IsAssertive ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
        }
    }

    public void Announce(string? message)
    {
        Message = message ?? string.Empty;
        if (Message.Length == 0)
        {
            Text = string.Empty;
            return;
        }
        _count++;
        Text = _count % 2 == 0 ? Message + "​" : Message;
    }
}
