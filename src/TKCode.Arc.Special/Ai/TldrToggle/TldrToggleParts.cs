using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>The article in Full mode: its paragraphs, one under the other.</summary>
internal sealed class TldrToggleArticleView : StackPanel
{
    protected override Type StyleKeyOverride => typeof(TldrToggleArticleView);

    /// <summary>The paragraphs in order.</summary>
    internal IEnumerable<TldrToggleParagraph> Paragraphs => Children.OfType<TldrToggleParagraph>();
}

/// <summary>The key points: one row per point.</summary>
internal sealed class TldrTogglePointList : StackPanel
{
    protected override Type StyleKeyOverride => typeof(TldrTogglePointList);

    /// <summary>The rows in order.</summary>
    internal IReadOnlyList<TldrTogglePointRow> Rows => Children.OfType<TldrTogglePointRow>().ToList();
}

/// <summary>
/// One paragraph of the article. It takes focus when a key point jumps to it, and its whole text is highlighted with a
/// rounded <see cref="TextRangeDecoration"/> (AccentSubtle) that fades in over 160ms, holds, and fades out over 600ms;
/// the fades are opacity only, so they also run with reduced motion.
/// </summary>
internal sealed class TldrToggleParagraph : DecoratedTextBlock
{
    /// <summary>The highlight's fill.</summary>
    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<TldrToggleParagraph, IBrush?>(nameof(HighlightBrush));

    /// <summary>The highlight's corners.</summary>
    public static readonly StyledProperty<CornerRadius> HighlightCornerRadiusProperty =
        AvaloniaProperty.Register<TldrToggleParagraph, CornerRadius>(nameof(HighlightCornerRadius));

    /// <summary>How far the highlight reaches past the text.</summary>
    public static readonly StyledProperty<Thickness> HighlightOutsetProperty =
        AvaloniaProperty.Register<TldrToggleParagraph, Thickness>(nameof(HighlightOutset));

    /// <summary>The highlight's fade in and out, and how long it stays between them.</summary>
    internal static readonly TimeSpan FadeIn = TimeSpan.FromMilliseconds(160);

    internal static readonly TimeSpan FadeOut = TimeSpan.FromMilliseconds(600);

    internal static readonly TimeSpan Hold = TimeSpan.FromMilliseconds(1400);

    private readonly TextRangeDecoration _highlight = new() { Opacity = 0 };
    private MotionValue<double>? _fade;
    private int _version;

    protected override Type StyleKeyOverride => typeof(TldrToggleParagraph);

    /// <inheritdoc cref="HighlightBrushProperty"/>
    public IBrush? HighlightBrush { get => GetValue(HighlightBrushProperty); set => SetValue(HighlightBrushProperty, value); }

    /// <inheritdoc cref="HighlightCornerRadiusProperty"/>
    public CornerRadius HighlightCornerRadius { get => GetValue(HighlightCornerRadiusProperty); set => SetValue(HighlightCornerRadiusProperty, value); }

    /// <inheritdoc cref="HighlightOutsetProperty"/>
    public Thickness HighlightOutset { get => GetValue(HighlightOutsetProperty); set => SetValue(HighlightOutsetProperty, value); }

    /// <summary>The paragraph's id in the article.</summary>
    public string ParagraphId { get; init; } = string.Empty;

    /// <summary>Its number in the article, from 1.</summary>
    public int Number { get; init; }

    /// <summary>The highlight's opacity now (0 when none shows).</summary>
    internal double HighlightOpacity => _highlight.Opacity;

    /// <summary>Highlights the paragraph: in over 160ms, then out over 600ms after a short hold. A new call restarts it.</summary>
    internal async Task Highlight()
    {
        var version = ++_version;
        _highlight.Start = 0;
        _highlight.Length = Text?.Length ?? 0;
        _highlight.Background = HighlightBrush;
        _highlight.CornerRadius = HighlightCornerRadius.TopLeft;
        _highlight.Outset = HighlightOutset;
        if (!Decorations.Contains(_highlight))
        {
            Decorations.Insert(0, _highlight);
        }
        if (_fade is null)
        {
            _fade = new MotionValue<double>(this, 0, MotionVectors.Double);
            _fade.Changed += value =>
            {
                _highlight.Opacity = value;
                InvalidateDecorations();
            };
        }
        if (!await _fade.AnimateTo(1, MotionSpec.Tween(FadeIn, ArcEasings.Standard)) && version != _version)
        {
            return;
        }
        await Task.Delay(Hold);
        if (version != _version)
        {
            return;
        }
        await _fade.AnimateTo(0, MotionSpec.Tween(FadeOut, ArcEasings.Standard));
        if (version == _version && _highlight.Opacity <= 0)
        {
            Decorations.Remove(_highlight);
        }
    }
}

/// <summary>
/// One key point: a status glyph that ticks from a dashed circle to a check when the point is written, its text
/// streaming in chunks, and a ¶ jump link that appears once the whole list is done.
/// </summary>
[TemplatePart(TextPart, typeof(TldrToggleStreamText))]
[TemplatePart(LinkPart, typeof(AvaloniaButton))]
[PseudoClasses(DonePseudoClass, LinkedPseudoClass)]
internal sealed class TldrTogglePointRow : TemplatedControl
{
    internal const string TextPart = "PART_Text";
    internal const string LinkPart = "PART_Link";
    internal const string DonePseudoClass = ":done";
    internal const string LinkedPseudoClass = ":linked";

    /// <summary>The paragraph number the link shows, as text.</summary>
    public static readonly StyledProperty<string?> NumberTextProperty =
        AvaloniaProperty.Register<TldrTogglePointRow, string?>(nameof(NumberText));

    /// <summary>The link's tooltip.</summary>
    public static readonly StyledProperty<string?> LinkTipProperty =
        AvaloniaProperty.Register<TldrTogglePointRow, string?>(nameof(LinkTip));

    /// <summary>The glyph of a point still being written (Lucide circle-dashed).</summary>
    public static readonly Geometry PendingGlyph = ArcIcons.Parse(
        "M10.1 2.182a10 10 0 0 1 3.8 0M13.9 21.818a10 10 0 0 1-3.8 0M17.609 3.721a10 10 0 0 1 2.69 2.7M2.182 13.9a10 10 0 0 1 0-3.8" +
        "M20.279 17.609a10 10 0 0 1-2.7 2.69M21.818 10.1a10 10 0 0 1 0 3.8M3.721 6.391a10 10 0 0 1 2.7-2.69M6.391 20.279a10 10 0 0 1-2.69-2.7");

    private readonly StringBuilder _text = new();
    private TldrToggleStreamText? _textView;
    private AvaloniaButton? _link;
    private bool _isDone;
    private bool _isLinked;
    private string? _linkName;
    private int _number;

    /// <summary>The source paragraph id, or null.</summary>
    public string? Source { get; init; }

    /// <summary>The source paragraph's number in the article, from 1 (0 when the point has no source).</summary>
    public int Number
    {
        get => _number;
        init
        {
            _number = value;
            NumberText = value > 0 ? value.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
        }
    }

    /// <inheritdoc cref="NumberTextProperty"/>
    public string? NumberText { get => GetValue(NumberTextProperty); set => SetValue(NumberTextProperty, value); }

    /// <inheritdoc cref="LinkTipProperty"/>
    public string? LinkTip { get => GetValue(LinkTipProperty); set => SetValue(LinkTipProperty, value); }

    /// <summary>The point's text so far.</summary>
    public string Text => _text.ToString();

    /// <summary>Whether the point is written (the glyph shows a check).</summary>
    public bool IsDone
    {
        get => _isDone;
        set
        {
            _isDone = value;
            PseudoClasses.Set(DonePseudoClass, value);
        }
    }

    /// <summary>Whether the jump link shows and can be used.</summary>
    public bool IsLinked
    {
        get => _isLinked;
        set
        {
            _isLinked = value;
            PseudoClasses.Set(LinkedPseudoClass, value);
            if (_link is not null)
            {
                _link.IsEnabled = value;
            }
        }
    }

    /// <summary>The jump link's accessible name.</summary>
    public string? LinkName
    {
        get => _linkName;
        set
        {
            _linkName = value;
            if (_link is not null)
            {
                AutomationProperties.SetName(_link, value);
            }
        }
    }

    internal AvaloniaButton? Link => _link;

    /// <summary>Raised when the jump link is pressed.</summary>
    public event EventHandler? JumpRequested;

    /// <summary>Adds text; it fades in when <paramref name="fade"/>.</summary>
    public void Append(string chunk, bool fade)
    {
        _text.Append(chunk);
        _textView?.Append(chunk, fade);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_link is not null)
        {
            _link.Click -= OnLinkClick;
        }
        _textView = e.NameScope.Find<TldrToggleStreamText>(TextPart);
        _link = e.NameScope.Find<AvaloniaButton>(LinkPart);
        _textView?.SetText(_text.ToString());
        if (_link is not null)
        {
            _link.Click += OnLinkClick;
            _link.IsEnabled = _isLinked;
            AutomationProperties.SetName(_link, _linkName);
        }
    }

    private void OnLinkClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        JumpRequested?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>The body while the model thinks: one calm shimmering line.</summary>
internal sealed class TldrToggleThinking : TemplatedControl
{
    /// <summary>The line's text.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<TldrToggleThinking, string?>(nameof(Text));

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
}

/// <summary>A quiet line in the body: an empty article, or a result stopped before any text arrived.</summary>
internal sealed class TldrToggleNote : TemplatedControl
{
    /// <summary>The note.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<TldrToggleNote, string?>(nameof(Text));

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
}

/// <summary>A failed result: an alert icon, the message and Try again. Screen readers hear it as an alert.</summary>
[TemplatePart(RetryPart, typeof(AvaloniaButton))]
internal sealed class TldrToggleAlert : TemplatedControl
{
    internal const string RetryPart = "PART_Retry";

    /// <summary>The message.</summary>
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<TldrToggleAlert, string?>(nameof(Message));

    /// <summary>The button's text.</summary>
    public static readonly StyledProperty<string?> RetryTextProperty =
        AvaloniaProperty.Register<TldrToggleAlert, string?>(nameof(RetryText));

    private AvaloniaButton? _retry;

    public TldrToggleAlert()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Assertive);
    }

    /// <inheritdoc cref="MessageProperty"/>
    public string? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

    /// <inheritdoc cref="RetryTextProperty"/>
    public string? RetryText { get => GetValue(RetryTextProperty); set => SetValue(RetryTextProperty, value); }

    internal AvaloniaButton? Retry => _retry;

    /// <summary>Raised when Try again is pressed.</summary>
    public event EventHandler? RetryRequested;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_retry is not null)
        {
            _retry.Click -= OnRetry;
        }
        _retry = e.NameScope.Find<AvaloniaButton>(RetryPart);
        if (_retry is not null)
        {
            _retry.Click += OnRetry;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MessageProperty)
        {
            AutomationProperties.SetName(this, Message);
        }
    }

    private void OnRetry(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RetryRequested?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>What the footer's state label says.</summary>
internal enum TldrToggleStatusKind
{
    Full,
    Thinking,
    Writing,
    Done,
    Stopped,
    Failed
}

/// <summary>
/// The footer's state: an icon and a word, never color alone (done pairs a check with the count, stopped a square with
/// "Stopped", failed an alert with "Failed"). "Writing" shimmers while text arrives.
/// </summary>
[TemplatePart(GlyphPart, typeof(ArcGlyph))]
[TemplatePart(LabelPart, typeof(ToolCallText))]
[PseudoClasses(DonePseudoClass, FailedPseudoClass)]
internal sealed class TldrToggleStatus : TemplatedControl
{
    internal const string GlyphPart = "PART_Glyph";
    internal const string LabelPart = "PART_Label";
    internal const string DonePseudoClass = ":done";
    internal const string FailedPseudoClass = ":failed";

    private ArcGlyph? _glyph;
    private ToolCallText? _label;

    public TldrToggleStatus(TldrToggleStatusKind kind, string text)
    {
        Kind = kind;
        Text = text;
        PseudoClasses.Set(DonePseudoClass, kind == TldrToggleStatusKind.Done);
        PseudoClasses.Set(FailedPseudoClass, kind == TldrToggleStatusKind.Failed);
        AutomationProperties.SetName(this, text);
    }

    public TldrToggleStatusKind Kind { get; }

    public string Text { get; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _glyph = e.NameScope.Find<ArcGlyph>(GlyphPart);
        _label = e.NameScope.Find<ToolCallText>(LabelPart);
        if (_glyph is not null)
        {
            _glyph.Data = Kind switch
            {
                TldrToggleStatusKind.Full => ArcIcons.FileText,
                TldrToggleStatusKind.Done => ArcIcons.Check,
                TldrToggleStatusKind.Stopped => ArcIcons.Square,
                TldrToggleStatusKind.Failed => ArcIcons.CircleAlert,
                _ => ArcIcons.Sparkles,
            };
        }
        if (_label is not null)
        {
            _label.Text = Text;
            _label.IsShimmering = Kind == TldrToggleStatusKind.Writing;
        }
    }
}

/// <summary>Which actions the footer offers.</summary>
internal enum TldrToggleActionsKind
{
    None,
    Running,
    Finished
}

/// <summary>The footer's actions: Stop while a result streams; Regenerate and Copy once it has finished or stopped.</summary>
[TemplatePart(StopPart, typeof(AvaloniaButton))]
[TemplatePart(RegeneratePart, typeof(AvaloniaButton))]
[TemplatePart(CopyPart, typeof(CopyButton))]
[PseudoClasses(RunningPseudoClass, FinishedPseudoClass)]
internal sealed class TldrToggleActions : TemplatedControl
{
    internal const string StopPart = "PART_Stop";
    internal const string RegeneratePart = "PART_Regenerate";
    internal const string CopyPart = "PART_Copy";
    internal const string RunningPseudoClass = ":running";
    internal const string FinishedPseudoClass = ":finished";

    /// <summary>The Stop button's text.</summary>
    public static readonly StyledProperty<string?> StopTextProperty =
        AvaloniaProperty.Register<TldrToggleActions, string?>(nameof(StopText));

    /// <summary>The regenerate button's name and tooltip.</summary>
    public static readonly StyledProperty<string?> RegenerateTextProperty =
        AvaloniaProperty.Register<TldrToggleActions, string?>(nameof(RegenerateText));

    /// <summary>The copy button's name.</summary>
    public static readonly StyledProperty<string?> CopyLabelProperty =
        AvaloniaProperty.Register<TldrToggleActions, string?>(nameof(CopyLabel));

    /// <summary>What Copy copies.</summary>
    public static readonly StyledProperty<string?> CopyTextProperty =
        AvaloniaProperty.Register<TldrToggleActions, string?>(nameof(CopyText));

    private AvaloniaButton? _stop;
    private AvaloniaButton? _regenerate;

    public TldrToggleActions(TldrToggleActionsKind kind)
    {
        Kind = kind;
        PseudoClasses.Set(RunningPseudoClass, kind == TldrToggleActionsKind.Running);
        PseudoClasses.Set(FinishedPseudoClass, kind == TldrToggleActionsKind.Finished);
    }

    public TldrToggleActionsKind Kind { get; }

    /// <inheritdoc cref="StopTextProperty"/>
    public string? StopText { get => GetValue(StopTextProperty); set => SetValue(StopTextProperty, value); }

    /// <inheritdoc cref="RegenerateTextProperty"/>
    public string? RegenerateText { get => GetValue(RegenerateTextProperty); set => SetValue(RegenerateTextProperty, value); }

    /// <inheritdoc cref="CopyLabelProperty"/>
    public string? CopyLabel { get => GetValue(CopyLabelProperty); set => SetValue(CopyLabelProperty, value); }

    /// <inheritdoc cref="CopyTextProperty"/>
    public string? CopyText { get => GetValue(CopyTextProperty); set => SetValue(CopyTextProperty, value); }

    internal AvaloniaButton? StopButton => _stop;

    internal AvaloniaButton? RegenerateButton => _regenerate;

    /// <summary>Raised when Stop is pressed.</summary>
    public event EventHandler? StopRequested;

    /// <summary>Raised when Regenerate is pressed.</summary>
    public event EventHandler? RegenerateRequested;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_stop is not null)
        {
            _stop.Click -= OnStop;
        }
        if (_regenerate is not null)
        {
            _regenerate.Click -= OnRegenerate;
        }
        _stop = e.NameScope.Find<AvaloniaButton>(StopPart);
        _regenerate = e.NameScope.Find<AvaloniaButton>(RegeneratePart);
        if (_stop is not null)
        {
            _stop.Click += OnStop;
        }
        if (_regenerate is not null)
        {
            _regenerate.Click += OnRegenerate;
        }
    }

    private void OnStop(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        StopRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnRegenerate(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RegenerateRequested?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// The mode switch and the length slider: side by side (the slider at the right, up to <see cref="SliderMaxWidth"/>), or,
/// when <see cref="IsStacked"/> (a card below 520px), the slider under the switch at full width.
/// </summary>
internal sealed class TldrToggleControlsPanel : Panel
{
    /// <summary>Puts the slider under the switch at full width.</summary>
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<TldrToggleControlsPanel, bool>(nameof(IsStacked));

    /// <summary>The space beside or under the switch.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<TldrToggleControlsPanel, double>(nameof(Gap), 12);

    /// <summary>The slider's widest when side by side.</summary>
    public static readonly StyledProperty<double> SliderMaxWidthProperty =
        AvaloniaProperty.Register<TldrToggleControlsPanel, double>(nameof(SliderMaxWidth), 240);

    static TldrToggleControlsPanel()
    {
        AffectsMeasure<TldrToggleControlsPanel>(IsStackedProperty, GapProperty, SliderMaxWidthProperty);
    }

    /// <inheritdoc cref="IsStackedProperty"/>
    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    /// <inheritdoc cref="GapProperty"/>
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <inheritdoc cref="SliderMaxWidthProperty"/>
    public double SliderMaxWidth { get => GetValue(SliderMaxWidthProperty); set => SetValue(SliderMaxWidthProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return base.MeasureOverride(availableSize);
        }
        var (mode, length) = (Children[0], Children[1]);
        var width = availableSize.Width;
        if (IsStacked)
        {
            mode.Measure(availableSize);
            length.Measure(availableSize);
            return new Size(double.IsFinite(width) ? width : Math.Max(mode.DesiredSize.Width, length.DesiredSize.Width),
                mode.DesiredSize.Height + Gap + length.DesiredSize.Height);
        }
        mode.Measure(availableSize.WithWidth(double.PositiveInfinity));
        var rest = double.IsFinite(width) ? Math.Max(0, width - mode.DesiredSize.Width - Gap) : SliderMaxWidth;
        length.Measure(availableSize.WithWidth(Math.Min(rest, SliderMaxWidth)));
        return new Size(double.IsFinite(width) ? width : mode.DesiredSize.Width + Gap + length.DesiredSize.Width,
            Math.Max(mode.DesiredSize.Height, length.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return base.ArrangeOverride(finalSize);
        }
        var (mode, length) = (Children[0], Children[1]);
        if (IsStacked)
        {
            mode.Arrange(new Rect(0, 0, Math.Min(mode.DesiredSize.Width, finalSize.Width), mode.DesiredSize.Height));
            length.Arrange(new Rect(0, mode.DesiredSize.Height + Gap, finalSize.Width, length.DesiredSize.Height));
            return finalSize;
        }
        var modeTop = (finalSize.Height - mode.DesiredSize.Height) / 2;
        mode.Arrange(new Rect(0, modeTop, mode.DesiredSize.Width, mode.DesiredSize.Height));
        var rest = Math.Max(0, finalSize.Width - mode.DesiredSize.Width - Gap);
        var sliderWidth = Math.Min(rest, SliderMaxWidth);
        var sliderTop = (finalSize.Height - length.DesiredSize.Height) / 2;
        length.Arrange(new Rect(finalSize.Width - sliderWidth, sliderTop, sliderWidth, length.DesiredSize.Height));
        return finalSize;
    }
}
