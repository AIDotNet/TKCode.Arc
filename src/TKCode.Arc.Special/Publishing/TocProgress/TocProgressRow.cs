using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Where a row's section stands for the reader.</summary>
internal enum TocProgressRowState
{
    Upcoming,
    Read,
    Active
}

/// <summary>
/// One row of a <see cref="TocProgress"/> list, drawn by itself: the section title (up to two lines), indented by its
/// level past the rail, in the muted color while upcoming, the secondary color once read and the full color with medium
/// weight while it is the section being read. The text of each state is laid out once and kept.
/// </summary>
internal sealed class TocProgressRow : Control
{
    private readonly TocProgressList _list;
    private readonly TextLayout?[] _layouts = new TextLayout?[4];
    private double _layoutWidth = -1;
    private LazyFocusRing? _focusRing;
    private bool _ringAttached;
    private string? _title;
    private int _level = 1;
    private TocProgressRowState _state;

    static TocProgressRow()
    {
        FocusableProperty.OverrideDefaultValue<TocProgressRow>(true);
        CursorProperty.OverrideDefaultValue<TocProgressRow>(new Cursor(StandardCursorType.Hand));
        FocusAdornerProperty.OverrideDefaultValue<TocProgressRow>(null);
        ClipToBoundsProperty.OverrideDefaultValue<TocProgressRow>(false);
    }

    public TocProgressRow(TocProgressList list, int index)
    {
        _list = list;
        Index = index;
    }

    public int Index { get; set; }

    public string? Title
    {
        get => _title;
        set
        {
            if (_title == value)
            {
                return;
            }
            _title = value;
            ClearLayouts();
            AutomationProperties.SetName(this, value ?? string.Empty);
            InvalidateMeasure();
        }
    }

    public int Level
    {
        get => _level;
        set
        {
            if (_level == value)
            {
                return;
            }
            _level = value;
            ClearLayouts();
            InvalidateMeasure();
        }
    }

    public TocProgressRowState State
    {
        get => _state;
        set
        {
            if (_state == value)
            {
                return;
            }
            _state = value;
            PseudoClasses.Set(":active", value == TocProgressRowState.Active);
            PseudoClasses.Set(":read", value == TocProgressRowState.Read);
            AutomationProperties.SetHelpText(this, value switch
            {
                TocProgressRowState.Active => _list.Owner?.Labels.Current,
                TocProgressRowState.Read => _list.Owner?.Labels.Read,
                _ => null
            });
            InvalidateVisual();
        }
    }

    /// <summary>The text left edge: the rail gutter plus the indent of the level.</summary>
    public double TextLeft => _list.RailGutter + (_level - 1) * _list.LevelIndent;

    public void ClearLayouts()
    {
        for (var i = 0; i < _layouts.Length; i++)
        {
            _layouts[i]?.Dispose();
            _layouts[i] = null;
        }
    }

    private TextLayout Layout(int slot, IBrush? brush, FontWeight weight)
    {
        if (_layouts[slot] is { } cached)
        {
            return cached;
        }
        var typeface = new Typeface(TextElement.GetFontFamily(this), FontStyle.Normal, weight);
        var layout = new TextLayout(_title ?? string.Empty, typeface, _list.TextSize, brush, TextAlignment.Left, TextWrapping.Wrap,
            TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, Math.Max(1, _layoutWidth), double.PositiveInfinity,
            _list.TextLineHeight, 0, 2);
        _layouts[slot] = layout;
        return layout;
    }

    private TextLayout CurrentLayout()
    {
        if (IsPointerOver && _state != TocProgressRowState.Active)
        {
            return Layout(3, _list.ActiveBrush, FontWeight.Normal);
        }
        return _state switch
        {
            TocProgressRowState.Active => Layout(2, _list.ActiveBrush, _list.ActiveWeight),
            TocProgressRowState.Read => Layout(1, _list.ReadBrush, FontWeight.Normal),
            _ => Layout(0, _list.UpcomingBrush, FontWeight.Normal)
        };
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 240 : availableSize.Width;
        var textWidth = Math.Max(1, width - TextLeft - _list.RowPadding.Right);
        if (Math.Abs(textWidth - _layoutWidth) > 0.5)
        {
            _layoutWidth = textWidth;
            ClearLayouts();
        }
        // Measure with the widest state (medium weight) so a row never changes height when it becomes active.
        var height = Layout(2, _list.ActiveBrush, _list.ActiveWeight).Height;
        if (_focusRing?.Ring is { } ring)
        {
            ring.Measure(availableSize);
        }
        return new Size(width, height + _list.RowPadding.Top + _list.RowPadding.Bottom);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _focusRing?.Ring?.Arrange(new Rect(finalSize));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        // The whole row is a target, not only its glyphs.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        CurrentLayout().Draw(context, new Point(TextLeft, _list.RowPadding.Top));
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            _list.Invoke(this, keyboard: false);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Enter:
            case Key.Space:
                _list.Invoke(this, keyboard: true);
                e.Handled = true;
                break;
            case Key.Down:
                e.Handled = _list.FocusRow(Index + 1);
                break;
            case Key.Up:
                e.Handled = _list.FocusRow(Index - 1);
                break;
            case Key.Home:
                e.Handled = _list.FocusRow(0);
                break;
            case Key.End:
                e.Handled = _list.FocusRow(int.MaxValue);
                break;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing ??= new LazyFocusRing(this, TocProgressTokens.Key(TocProgressTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep8));
        if (!_ringAttached)
        {
            _ringAttached = true;
            _focusRing.Attach(c =>
            {
                LogicalChildren.Add(c);
                VisualChildren.Add(c);
            }, c =>
            {
                VisualChildren.Remove(c);
                LogicalChildren.Remove(c);
            });
        }
        _focusRing.Update();
        _list.OnRowFocused(this);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ClearLayouts();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    private sealed class RowPeer(TocProgressRow owner) : ControlAutomationPeer(owner), IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Hyperlink;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public void Invoke()
        {
            var row = (TocProgressRow)Owner;
            row._list.Invoke(row, keyboard: true);
        }
    }
}
