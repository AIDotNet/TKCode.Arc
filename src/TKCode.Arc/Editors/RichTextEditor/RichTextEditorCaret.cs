using System.Collections.Immutable;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.Utilities;

namespace TKCode.Arc;

/// <summary>The self-drawn caret: a thin accent bar that blinks without re-rendering the text.</summary>
internal sealed class RichTextEditorCaret : Control
{
    private readonly DispatcherTimer _timer;

    public RichTextEditorCaret()
    {
        IsHitTestVisible = false;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(530) };
        _timer.Tick += (_, _) => Opacity = Opacity > 0.5 ? 0 : 1;
    }

    private IBrush? _brush;

    public IBrush? Brush
    {
        get => _brush;
        set
        {
            _brush = value;
            InvalidateVisual();
        }
    }

    /// <summary>Shows the caret solid and restarts the blink (after every move or edit).</summary>
    public void Restart()
    {
        Opacity = 1;
        _timer.Stop();
        if (IsVisible)
        {
            _timer.Start();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty)
        {
            Restart();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }

    public override void Render(DrawingContext context)
    {
        if (_brush is not null)
        {
            context.FillRectangle(_brush, new Rect(Bounds.Size));
        }
    }
}
