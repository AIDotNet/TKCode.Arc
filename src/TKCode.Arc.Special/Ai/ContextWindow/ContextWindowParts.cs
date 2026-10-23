using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>The icon and icon color of a context window status.</summary>
internal enum ContextWindowTone
{
    /// <summary>An info circle in muted text.</summary>
    Info,

    /// <summary>A warning triangle in the warning color.</summary>
    Warning,

    /// <summary>The shrink glyph in muted text while messages fold (it does not spin).</summary>
    Working,

    /// <summary>A check circle in the success color.</summary>
    Success
}

/// <summary>Lucide glyphs the context window needs that Core <see cref="ArcIcons"/> does not ship.</summary>
internal static class ContextWindowIcons
{
    // Lucide file-plus-corner (formerly file-plus-2), ISC.
    private const string FilePlusCornerData =
        "M11.35 22H6a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.706.706l3.588 3.588A2.4 2.4 0 0 1 20 8v5.35 M14 2v5a1 1 0 0 0 1 1h5 M14 19h6 M17 16v6";

    /// <summary>A page with a plus at its lower corner: attach a file.</summary>
    public static Geometry FilePlusCorner => ArcIcons.Parse(FilePlusCornerData);

    public static Geometry IconOf(ContextWindowTone tone) => tone switch
    {
        ContextWindowTone.Warning => ArcIcons.TriangleAlert,
        ContextWindowTone.Working => ArcIcons.Shrink,
        ContextWindowTone.Success => ArcIcons.CircleCheck,
        _ => ArcIcons.Info
    };

    public static ArcDesignTokenKind ColorOf(ContextWindowTone tone) => tone switch
    {
        ContextWindowTone.Warning => ArcDesignTokenKind.Warning,
        ContextWindowTone.Success => ArcDesignTokenKind.Success,
        _ => ArcDesignTokenKind.TextMuted
    };
}

/// <summary>
/// The status line of a <see cref="ContextWindow"/>: a 15px tone icon and a sentence in secondary text that wraps, the
/// icon centered on the text. A new status crossfades in place (the old line fades out in 100ms and leaves the layout at
/// once, the new one fades in over 160ms while rising 3px, both on the standard curve); without Full motion it is
/// replaced at once.
/// </summary>
internal sealed class ContextWindowStatusLine : Control
{
    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<ContextWindowStatusLine, double>(nameof(IconSize), 15);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<ContextWindowStatusLine, double>(nameof(Gap), 8);

    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<ContextWindowStatusLine, double>(nameof(LineHeight), 19.6);

    public static readonly StyledProperty<double> LetterSpacingProperty =
        AvaloniaProperty.Register<ContextWindowStatusLine, double>(nameof(LetterSpacing), -0.14);

    private readonly ArcContentSwap _swap;

    public ContextWindowStatusLine()
    {
        _swap = new ArcContentSwap
        {
            Mode = ContentSwapMode.Crossfade,
            EnterOffset = 3,
            EnterDuration = TimeSpan.FromMilliseconds(160),
            ExitDuration = TimeSpan.FromMilliseconds(100),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Top
        };
        VisualChildren.Add(_swap);
        LogicalChildren.Add(_swap);
    }

    public double IconSize { get => GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    public double LetterSpacing { get => GetValue(LetterSpacingProperty); set => SetValue(LetterSpacingProperty, value); }

    /// <summary>The sentence on screen.</summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>The tone on screen.</summary>
    public ContextWindowTone Tone { get; private set; }

    /// <summary>Shows a status; <paramref name="animate"/> crossfades it in, otherwise it replaces the old one at once.</summary>
    public void Show(string text, ContextWindowTone tone, bool animate)
    {
        Text = text;
        Tone = tone;
        AutomationProperties.SetName(this, text);
        _swap.Mode = animate ? ContentSwapMode.Crossfade : ContentSwapMode.None;
        _swap.Content = Build(text, tone);
    }

    private Control Build(string text, ContextWindowTone tone)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = Gap };
        var glyph = new ArcGlyph
        {
            Data = ContextWindowIcons.IconOf(tone),
            Size = IconSize,
            StrokeThickness = 1.75,
            VerticalAlignment = VerticalAlignment.Center
        };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, ContextWindowIcons.ColorOf(tone));
        var label = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = LineHeight,
            LetterSpacing = LetterSpacing,
            VerticalAlignment = VerticalAlignment.Center
        };
        ArcTokens.Bind(label, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        Grid.SetColumn(label, 1);
        row.Children.Add(glyph);
        row.Children.Add(label);
        return row;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _swap.Measure(availableSize);
        return _swap.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _swap.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>
/// A legend row of a <see cref="ContextWindow"/>: a 10px swatch, the part's name (one line, cut with an ellipsis), its
/// tokens and its share of the window. An empty row shows its placeholder in muted text and no percentage. While another
/// row is hovered the row fades to 0.45 (160ms in the theme).
/// </summary>
[TemplatePart(TokensPart, typeof(ContextWindowNumber))]
[TemplatePart(PercentPart, typeof(ContextWindowNumber))]
[PseudoClasses(DimmedPseudoClass, EmptyPseudoClass, StrongDividerPseudoClass, LastPseudoClass)]
internal sealed class ContextWindowLegendRow : TemplatedControl
{
    public const string TokensPart = "PART_Tokens";
    public const string PercentPart = "PART_Percent";
    public const string DimmedPseudoClass = ":dimmed";
    public const string EmptyPseudoClass = ":empty";
    public const string StrongDividerPseudoClass = ":strong-divider";
    public const string LastPseudoClass = ":last";

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ContextWindowLegendRow, string?>(nameof(Label));

    public static readonly StyledProperty<IBrush?> SwatchFillProperty =
        AvaloniaProperty.Register<ContextWindowLegendRow, IBrush?>(nameof(SwatchFill));

    public static readonly StyledProperty<IBrush?> SwatchOutlineProperty =
        AvaloniaProperty.Register<ContextWindowLegendRow, IBrush?>(nameof(SwatchOutline));

    private ContextWindowNumber? _tokens;
    private ContextWindowNumber? _percent;
    private double _value;
    private double _window;
    private string? _placeholder;

    public ContextWindowLegendRow(ContextWindowPart part)
    {
        Part = part;
    }

    public ContextWindowPart Part { get; }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public IBrush? SwatchFill { get => GetValue(SwatchFillProperty); set => SetValue(SwatchFillProperty, value); }

    public IBrush? SwatchOutline { get => GetValue(SwatchOutlineProperty); set => SetValue(SwatchOutlineProperty, value); }

    /// <summary>The tokens shown (rolling).</summary>
    public double Value => _value;

    /// <summary>The tokens and percentage as shown.</summary>
    public string TokensText => _tokens?.Text ?? string.Empty;

    public string PercentText => _percent?.Text ?? string.Empty;

    /// <summary>True when the row shows its placeholder.</summary>
    public bool IsEmpty => _placeholder is not null;

    public bool IsDimmed
    {
        get => PseudoClasses.Contains(DimmedPseudoClass);
        set => PseudoClasses.Set(DimmedPseudoClass, value);
    }

    public void SetDivider(bool strong, bool last)
    {
        PseudoClasses.Set(StrongDividerPseudoClass, strong);
        PseudoClasses.Set(LastPseudoClass, last);
    }

    /// <summary>Shows a placeholder instead of the number (and no percentage), or null for the number.</summary>
    public void SetPlaceholder(string? placeholder)
    {
        _placeholder = placeholder;
        PseudoClasses.Set(EmptyPseudoClass, placeholder is not null);
        if (_tokens is not null)
        {
            _tokens.Placeholder = placeholder;
        }
        if (_percent is not null)
        {
            _percent.Placeholder = placeholder is null ? null : string.Empty;
        }
    }

    /// <summary>Shows <paramref name="tokens"/> of a <paramref name="window"/> (every frame of a roll).</summary>
    public void Show(double tokens, double window)
    {
        _value = tokens;
        _window = window;
        _tokens?.Show(tokens, window);
        _percent?.Show(tokens, window);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _tokens = e.NameScope.Find<ContextWindowNumber>(TokensPart);
        _percent = e.NameScope.Find<ContextWindowNumber>(PercentPart);
        SetPlaceholder(_placeholder);
        Show(_value, _window);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    private sealed class RowPeer(ContextWindowLegendRow owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override string? GetNameCore() => AutomationProperties.GetName(Owner);

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>
/// The model pill of a <see cref="ContextWindow"/>: the model's mark (16px), its name (medium), its window in muted
/// tabular figures and a chevron that turns over on the CSS spring while the menu is open. 36 high, 12 left and 10 right,
/// pill radius; hover and open take SurfaceMuted with the strong border. It is a button: the card opens the model menu
/// from its click (focus goes to the selected model when a key opened it).
/// </summary>
[PseudoClasses(OpenPseudoClass)]
internal sealed class ContextWindowModelPill : AvaloniaButton
{
    public const string OpenPseudoClass = ":open";

    public static readonly StyledProperty<Geometry?> MarkProperty =
        AvaloniaProperty.Register<ContextWindowModelPill, Geometry?>(nameof(Mark));

    public static readonly StyledProperty<IBrush?> MarkBrushProperty =
        AvaloniaProperty.Register<ContextWindowModelPill, IBrush?>(nameof(MarkBrush));

    public static readonly StyledProperty<string?> ModelNameProperty =
        AvaloniaProperty.Register<ContextWindowModelPill, string?>(nameof(ModelName));

    public static readonly StyledProperty<string?> WindowTextProperty =
        AvaloniaProperty.Register<ContextWindowModelPill, string?>(nameof(WindowText));

    protected override Type StyleKeyOverride => typeof(ContextWindowModelPill);

    public Geometry? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    /// <summary>The mark's color, or null for the text color.</summary>
    public IBrush? MarkBrush { get => GetValue(MarkBrushProperty); set => SetValue(MarkBrushProperty, value); }

    public string? ModelName { get => GetValue(ModelNameProperty); set => SetValue(ModelNameProperty, value); }

    public string? WindowText { get => GetValue(WindowTextProperty); set => SetValue(WindowTextProperty, value); }

    private ArcGlyph? _mark;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _mark = e.NameScope.Find<ArcGlyph>("PART_Mark");
        ApplyMarkBrush();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MarkBrushProperty)
        {
            ApplyMarkBrush();
        }
    }

    private void ApplyMarkBrush()
    {
        if (_mark is null)
        {
            return;
        }
        if (MarkBrush is { } brush)
        {
            _mark.Foreground = brush;
        }
        else
        {
            // No color of its own: the mark inherits the pill's text color.
            _mark.ClearValue(ArcGlyph.ForegroundProperty);
        }
    }

    /// <summary>True while a key (Enter or Space) is activating the pill, so the menu opens on the selected model.</summary>
    public bool IsKeyboardActivation { get; set; }

    public bool IsOpen
    {
        get => PseudoClasses.Contains(OpenPseudoClass);
        set => PseudoClasses.Set(OpenPseudoClass, value);
    }
}

/// <summary>
/// Lays children out in lines with gaps, wrapping when a line is full (CSS flex-wrap). With <see cref="PushLastToEnd"/>
/// the last child moves to the end of its line when it shares the line with others (Compact now on the right of the
/// actions, the threshold caption on the right of the cell caption) and starts its own line on the left when it wraps.
/// Children are centered on their line.
/// </summary>
internal sealed class ContextWindowFlowPanel : Panel
{
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<ContextWindowFlowPanel, double>(nameof(ColumnGap), 8);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<ContextWindowFlowPanel, double>(nameof(RowGap), 8);

    public static readonly StyledProperty<bool> PushLastToEndProperty =
        AvaloniaProperty.Register<ContextWindowFlowPanel, bool>(nameof(PushLastToEnd));

    static ContextWindowFlowPanel()
    {
        AffectsMeasure<ContextWindowFlowPanel>(ColumnGapProperty, RowGapProperty, PushLastToEndProperty);
    }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    public bool PushLastToEnd { get => GetValue(PushLastToEndProperty); set => SetValue(PushLastToEndProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        foreach (var child in Children)
        {
            child.Measure(new Size(width, double.PositiveInfinity));
        }
        var (lines, used) = Lines(width);
        var height = 0.0;
        for (var i = 0; i < lines.Count; i++)
        {
            height += lines[i].Height + (i > 0 ? RowGap : 0);
        }
        return new Size(double.IsFinite(width) ? Math.Min(width, used) : used, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (lines, _) = Lines(finalSize.Width);
        var y = 0.0;
        Control? last = null;
        foreach (var child in Children)
        {
            if (child.IsVisible)
            {
                last = child;
            }
        }
        foreach (var line in lines)
        {
            var x = 0.0;
            for (var i = line.First; i <= line.Last; i++)
            {
                var child = Children[i];
                if (!child.IsVisible)
                {
                    continue;
                }
                var size = child.DesiredSize;
                var left = x;
                if (PushLastToEnd && ReferenceEquals(child, last) && line.Count > 1)
                {
                    left = Math.Max(x, finalSize.Width - size.Width);
                }
                child.Arrange(new Rect(left, y + (line.Height - size.Height) / 2, size.Width, size.Height));
                x += size.Width + ColumnGap;
            }
            y += line.Height + RowGap;
        }
        return finalSize;
    }

    private (List<Line> Lines, double Width) Lines(double width)
    {
        var lines = new List<Line>();
        var current = new Line { First = -1 };
        var x = 0.0;
        var maxWidth = 0.0;
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (!child.IsVisible)
            {
                continue;
            }
            var size = child.DesiredSize;
            if (current.Count > 0 && double.IsFinite(width) && x + ColumnGap + size.Width > width + 0.5)
            {
                lines.Add(current);
                current = new Line { First = -1 };
                x = 0;
            }
            if (current.Count > 0)
            {
                x += ColumnGap;
            }
            if (current.First < 0)
            {
                current.First = i;
            }
            current.Last = i;
            current.Count++;
            current.Height = Math.Max(current.Height, size.Height);
            x += size.Width;
            maxWidth = Math.Max(maxWidth, x);
        }
        if (current.Count > 0)
        {
            lines.Add(current);
        }
        return (lines, maxWidth);
    }

    private struct Line
    {
        public int First;
        public int Last;
        public int Count;
        public double Height;
    }
}

/// <summary>
/// The body of a <see cref="ContextWindow"/>: the grid column beside a fixed-width legend (side by side, top aligned), or
/// the legend under the grid when <see cref="IsStacked"/>. The first child is the grid block, the second the legend.
/// </summary>
internal sealed class ContextWindowBodyPanel : Panel
{
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<ContextWindowBodyPanel, bool>(nameof(IsStacked));

    public static readonly StyledProperty<double> SideWidthProperty =
        AvaloniaProperty.Register<ContextWindowBodyPanel, double>(nameof(SideWidth), 260);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<ContextWindowBodyPanel, double>(nameof(Gap), 32);

    public static readonly StyledProperty<double> StackedGapProperty =
        AvaloniaProperty.Register<ContextWindowBodyPanel, double>(nameof(StackedGap), 20);

    static ContextWindowBodyPanel()
    {
        AffectsMeasure<ContextWindowBodyPanel>(IsStackedProperty, SideWidthProperty, GapProperty, StackedGapProperty);
    }

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    public double SideWidth { get => GetValue(SideWidthProperty); set => SetValue(SideWidthProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public double StackedGap { get => GetValue(StackedGapProperty); set => SetValue(StackedGapProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return default;
        }
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 696;
        var main = Children[0];
        var side = Children[1];
        if (IsStacked)
        {
            main.Measure(new Size(width, double.PositiveInfinity));
            side.Measure(new Size(width, double.PositiveInfinity));
            return new Size(width, main.DesiredSize.Height + StackedGap + side.DesiredSize.Height);
        }
        var mainWidth = Math.Max(0, width - Gap - SideWidth);
        main.Measure(new Size(mainWidth, double.PositiveInfinity));
        side.Measure(new Size(SideWidth, double.PositiveInfinity));
        return new Size(width, Math.Max(main.DesiredSize.Height, side.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return finalSize;
        }
        var main = Children[0];
        var side = Children[1];
        if (IsStacked)
        {
            main.Arrange(new Rect(0, 0, finalSize.Width, main.DesiredSize.Height));
            side.Arrange(new Rect(0, main.DesiredSize.Height + StackedGap, finalSize.Width, side.DesiredSize.Height));
            return finalSize;
        }
        var mainWidth = Math.Max(0, finalSize.Width - Gap - SideWidth);
        main.Arrange(new Rect(0, 0, mainWidth, main.DesiredSize.Height));
        side.Arrange(new Rect(mainWidth + Gap, 0, SideWidth, side.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>
/// Keeps keyboard focus inside a <see cref="ContextWindow"/> while every action is off (summarizing): focusable but not
/// a tab stop, no size, named after the current status. The card hands focus back to an action when the compaction lands.
/// </summary>
internal sealed class ContextWindowFocusHolder : Control
{
    public ContextWindowFocusHolder()
    {
        Focusable = true;
        IsTabStop = false;
        FocusAdorner = null;
        Width = 0;
        Height = 0;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new HolderPeer(this);

    private sealed class HolderPeer(ContextWindowFocusHolder owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string? GetNameCore() => AutomationProperties.GetName(Owner);
    }
}

/// <summary>One-shot timers of a <see cref="ContextWindow"/> (the wait before an automatic compaction and the summarizing phase).</summary>
internal interface IContextWindowTimers
{
    /// <summary>Runs <paramref name="callback"/> once after <paramref name="delay"/>; disposing the result cancels it.</summary>
    IDisposable Start(TimeSpan delay, Action callback);
}

/// <summary>Wall-clock timers on the UI thread (a zero delay runs on the next dispatcher pass).</summary>
internal sealed class ContextWindowDispatcherTimers : IContextWindowTimers
{
    public static readonly ContextWindowDispatcherTimers Instance = new();

    public IDisposable Start(TimeSpan delay, Action callback) => new Shot(delay, callback);

    private sealed class Shot : IDisposable
    {
        private DispatcherTimer? _timer;
        private Action? _callback;

        public Shot(TimeSpan delay, Action callback)
        {
            _callback = callback;
            if (delay <= TimeSpan.Zero)
            {
                Dispatcher.UIThread.Post(Fire, DispatcherPriority.Background);
                return;
            }
            _timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = delay };
            _timer.Tick += (_, _) => Fire();
            _timer.Start();
        }

        private void Fire()
        {
            _timer?.Stop();
            _timer = null;
            var callback = _callback;
            _callback = null;
            callback?.Invoke();
        }

        public void Dispose()
        {
            _timer?.Stop();
            _timer = null;
            _callback = null;
        }
    }
}
