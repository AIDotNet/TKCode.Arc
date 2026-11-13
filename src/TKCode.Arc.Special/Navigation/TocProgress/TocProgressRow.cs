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
/// One row of a <see cref="TocProgress"/> list, drawn by itself: the section title, indented by its level past the line,
/// wrapping when long. Its text color comes from the theme by state (muted while upcoming, secondary once read, the full
/// text color while current and when an upcoming row is hovered), so the theme's brush transition animates it; the row
/// copies the color into the one brush its text layout was built with.
/// </summary>
[Avalonia.Controls.Metadata.PseudoClasses(":active", ":read")]
internal sealed class TocProgressRow : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<TocProgressRow>();

    public static readonly StyledProperty<IBrush?> FocusFillProperty =
        AvaloniaProperty.Register<TocProgressRow, IBrush?>(nameof(FocusFill));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<TocProgressRow, CornerRadius>(nameof(CornerRadius));

    private readonly TocProgressList _list;
    private readonly SolidColorBrush _textBrush = new(Colors.Gray);
    private TextLayout? _layout;
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
        AffectsRender<TocProgressRow>(FocusFillProperty, CornerRadiusProperty);
    }

    public TocProgressRow(TocProgressList list, int index)
    {
        _list = list;
        Index = index;
    }

    public int Index { get; set; }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public IBrush? FocusFill { get => GetValue(FocusFillProperty); set => SetValue(FocusFillProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

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
            ClearLayout();
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
            ClearLayout();
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
        }
    }

    /// <summary>The text color drawn now (tests read it).</summary>
    public Color TextColor => _textBrush.Color;

    /// <summary>The text left edge: the gutter plus the indent of the level.</summary>
    public double TextLeft => _list.RailGutter + (Math.Clamp(_level, 1, 3) - 1) * _list.LevelIndent;

    public void ClearLayout()
    {
        _layout?.Dispose();
        _layout = null;
    }

    private TextLayout Layout()
    {
        if (_layout is { } cached)
        {
            return cached;
        }
        var typeface = new Typeface(TextElement.GetFontFamily(this), FontStyle.Normal, FontWeight.Normal);
        _layout = new TextLayout(_title ?? string.Empty, typeface, _list.TextSize, _textBrush, TextAlignment.Left, TextWrapping.Wrap,
            TextTrimming.None, null, FlowDirection.LeftToRight, Math.Max(1, _layoutWidth), double.PositiveInfinity,
            _list.TextLineHeight, _list.TextLetterSpacing, 0);
        return _layout;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty)
        {
            if (Foreground is ISolidColorBrush solid)
            {
                _textBrush.Color = solid.Color;
                _textBrush.Opacity = solid.Opacity;
            }
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 240 : availableSize.Width;
        var textWidth = Math.Max(1, width - TextLeft - _list.RowPadding.Right);
        if (Math.Abs(textWidth - _layoutWidth) > 0.5)
        {
            _layoutWidth = textWidth;
            ClearLayout();
        }
        var height = Layout().Height;
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
        var bounds = new Rect(Bounds.Size);
        if (Classes.Contains(":focus-visible") && FocusFill is { } fill)
        {
            context.DrawRectangle(fill, null, new RoundedRect(bounds, CornerRadius));
        }
        else
        {
            // The whole row is a target, not only its glyphs.
            context.FillRectangle(Brushes.Transparent, bounds);
        }
        Layout().Draw(context, new Point(TextLeft, _list.RowPadding.Top));
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
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep14));
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
        // The ring may have just been built: it needs a layout pass of its own.
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
        InvalidateVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ClearLayout();
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
