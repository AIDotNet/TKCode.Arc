using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// Text that the library builds for a tooltip bubble (plain text tips, the label and shortcut of a
/// <see cref="FloatingButtonGroupItem"/> tip). It takes its foreground from the <see cref="Tooltip"/> it sits in as a
/// local value, so neither a host's descendant style (the popup is a logical child of its trigger, so
/// <c>Button.my-btn TextBlock</c> reaches it) nor an app theme's global <c>TextBlock</c> style can turn it dark on the
/// dark bubble (#190). It is styled as a <see cref="TextBlock"/> otherwise.
/// </summary>
internal sealed class TooltipText : TextBlock
{
    private IDisposable? _foreground;

    protected override Type StyleKeyOverride => typeof(TextBlock);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _foreground?.Dispose();
        _foreground = null;
        if (this.FindAncestorOfType<Tooltip>() is { } bubble)
        {
            _foreground = Bind(ForegroundProperty, bubble.GetObservable(Tooltip.ForegroundProperty), BindingPriority.LocalValue);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _foreground?.Dispose();
        _foreground = null;
    }
}
