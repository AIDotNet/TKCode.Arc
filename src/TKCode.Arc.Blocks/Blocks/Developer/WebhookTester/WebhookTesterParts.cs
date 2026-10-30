using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Builders for the code-made parts of <see cref="WebhookTester"/>, bound to ArcDesign tokens.</summary>
internal static class WebhookTesterUi
{
    /// <summary>A text block with token-bound size, color and (optionally) the mono font.</summary>
    public static TextBlock Text(string? text, ArcDesignTokenKind size, ArcDesignTokenKind color,
        FontWeight weight = FontWeight.Normal, bool mono = false, double lineHeight = double.NaN)
    {
        var block = new TextBlock { Text = text, FontWeight = weight, LineHeight = lineHeight };
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, size);
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        ArcTokens.Bind(block, TextBlock.FontFamilyProperty, mono ? ArcDesignTokenKind.MonoFontFamily : ArcDesignTokenKind.BodyFontFamily);
        return block;
    }

    /// <summary>A Lucide glyph in a token color.</summary>
    public static ArcGlyph Glyph(Geometry data, double size, ArcDesignTokenKind color, double stroke = 1.75)
    {
        var glyph = new ArcGlyph { Data = data, Size = size, StrokeThickness = stroke, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, color);
        return glyph;
    }

    /// <summary>A horizontal row with a gap.</summary>
    public static StackPanel Row(double spacing, params Control[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing };
        foreach (var child in children)
        {
            row.Children.Add(child);
        }
        return row;
    }

    /// <summary>The 16px mark used while the host gives none: a rounded square with the first letter of the name.</summary>
    public static Control DefaultMark(string name)
    {
        var letter = Text(string.IsNullOrEmpty(name) ? "?" : name[..1].ToUpperInvariant(), ArcDesignTokenKind.TypeXS,
            ArcDesignTokenKind.AccentForeground, FontWeight.SemiBold);
        letter.HorizontalAlignment = HorizontalAlignment.Center;
        letter.VerticalAlignment = VerticalAlignment.Center;
        letter.FontSize = 10;
        var mark = new Border { Width = 16, Height = 16, Child = letter, VerticalAlignment = VerticalAlignment.Center }
            .BindCornerRadius(ArcDesignTokenKind.RadiusStep4);
        ArcTokens.Bind(mark, Border.BackgroundProperty, ArcDesignTokenKind.Accent);
        return mark;
    }

    /// <summary>A key cap of the send hint.</summary>
    public static Control KeyCap(string text, string name)
    {
        var label = Text(text, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextSecondary, FontWeight.Medium, mono: true);
        label.VerticalAlignment = VerticalAlignment.Center;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        var cap = new WebhookTesterBox { Tone = WebhookTesterTone.Key, Content = label, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(cap, name);
        return cap;
    }

    /// <summary>The label above a field.</summary>
    public static TextBlock FieldLabel(string text)
    {
        var label = Text(text, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground, FontWeight.Medium, lineHeight: 19.6);
        label.Margin = new Thickness(0, 0, 0, 6);
        return label;
    }

    /// <summary>A rounded status chip ("200", "Timeout") tinted by <paramref name="tone"/>.</summary>
    public static WebhookTesterBox Chip(string text, WebhookTesterChipTone tone)
    {
        var label = Text(text, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.Foreground, FontWeight.Medium, mono: true, lineHeight: 16);
        label.VerticalAlignment = VerticalAlignment.Center;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        return new WebhookTesterBox
        {
            Tone = tone switch
            {
                WebhookTesterChipTone.Success => WebhookTesterTone.ChipSuccess,
                WebhookTesterChipTone.Danger => WebhookTesterTone.ChipDanger,
                _ => WebhookTesterTone.ChipNeutral
            },
            Content = label,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }
}


/// <summary>The looks of a <see cref="WebhookTesterBox"/>.</summary>
internal enum WebhookTesterTone
{
    Plain,
    Header,
    Body,
    Recent,
    Badge,
    FailedBadge,
    ChipSuccess,
    ChipDanger,
    ChipNeutral,
    Key,
    Secret,
    StepPending,
    StepActive,
    StepDone,
    StepFailed
}

/// <summary>
/// A framed box with a look chosen by <see cref="Tone"/> (section hosts, badges, chips, key caps, the secret's box and the
/// timeline's step circles). Code-made parts cannot be reached by descendant selectors, so each look is a pseudo class of
/// this control's own theme.
/// </summary>
internal sealed class WebhookTesterBox : ContentControl
{
    /// <summary>The look of the box.</summary>
    public static readonly StyledProperty<WebhookTesterTone> ToneProperty =
        AvaloniaProperty.Register<WebhookTesterBox, WebhookTesterTone>(nameof(Tone));

    /// <summary>Tightens the section paddings (the block is narrow).</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<WebhookTesterBox, bool>(nameof(IsCompact));

    public WebhookTesterTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    protected override Type StyleKeyOverride => typeof(WebhookTesterBox);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ToneProperty)
        {
            if (change.OldValue is WebhookTesterTone old)
            {
                PseudoClasses.Set(":" + old.ToString().ToLowerInvariant(), false);
            }
            PseudoClasses.Set(":" + Tone.ToString().ToLowerInvariant(), true);
        }
        else if (change.Property == IsCompactProperty)
        {
            PseudoClasses.Set(":compact", IsCompact);
        }
    }
}

/// <summary>The tint of a status chip.</summary>
internal enum WebhookTesterChipTone
{
    Neutral,
    Success,
    Danger
}

/// <summary>
/// The pick button of the tester: a delivery row, a response tab or the secret's reveal toggle. <c>IsChecked</c> means selected
/// (or revealed). Rows and tabs (<see cref="TogglesItself"/> false) keep their state for the block to set; the reveal toggle
/// flips on click.
/// </summary>
internal sealed class WebhookTesterPick : ToggleButton
{
    /// <summary>Whether a click flips <c>IsChecked</c> (the reveal toggle) or leaves it to the block (rows, tabs).</summary>
    public bool TogglesItself { get; set; }

    protected override Type StyleKeyOverride => typeof(WebhookTesterPick);

    protected override void Toggle()
    {
        if (TogglesItself)
        {
            base.Toggle();
        }
    }
}

/// <summary>
/// The payload editor: a multi-line text box with a line number gutter that follows the vertical scroll, the JSON tinted in
/// one pass per change (the box itself draws transparent text over a text block of colored runs with the same metrics), and
/// Enter that keeps the indentation and indents after an opening brace or bracket.
/// </summary>
[TemplatePart(TintPart, typeof(TextBlock))]
[TemplatePart(GutterPart, typeof(TextBlock))]
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[PseudoClasses(PlainPseudoClass, NoGutterPseudoClass)]
internal sealed class WebhookTesterEditor : TextBox
{
    internal const string TintPart = "PART_Tint";
    internal const string GutterPart = "PART_Gutter";
    internal const string ScrollPart = "PART_ScrollViewer";
    internal const string PlainPseudoClass = ":plain";
    internal const string NoGutterPseudoClass = ":nogutter";

    /// <summary>Tints JSON (true) or shows the text in one color (false: headers and the raw request).</summary>
    public static readonly StyledProperty<bool> IsTintedProperty = AvaloniaProperty.Register<WebhookTesterEditor, bool>(nameof(IsTinted), true);

    /// <summary>Shows the line numbers.</summary>
    public static readonly StyledProperty<bool> ShowsGutterProperty = AvaloniaProperty.Register<WebhookTesterEditor, bool>(nameof(ShowsGutter), true);

    public static readonly StyledProperty<IBrush?> KeyBrushProperty = AvaloniaProperty.Register<WebhookTesterEditor, IBrush?>(nameof(KeyBrush));
    public static readonly StyledProperty<IBrush?> StringBrushProperty = AvaloniaProperty.Register<WebhookTesterEditor, IBrush?>(nameof(StringBrush));
    public static readonly StyledProperty<IBrush?> NumberBrushProperty = AvaloniaProperty.Register<WebhookTesterEditor, IBrush?>(nameof(NumberBrush));
    public static readonly StyledProperty<IBrush?> LiteralBrushProperty = AvaloniaProperty.Register<WebhookTesterEditor, IBrush?>(nameof(LiteralBrush));
    public static readonly StyledProperty<IBrush?> PunctuationBrushProperty = AvaloniaProperty.Register<WebhookTesterEditor, IBrush?>(nameof(PunctuationBrush));

    private TextBlock? _tint;
    private TextBlock? _gutter;
    private ScrollViewer? _scroll;
    private int _lines;

    public bool IsTinted { get => GetValue(IsTintedProperty); set => SetValue(IsTintedProperty, value); }

    public bool ShowsGutter { get => GetValue(ShowsGutterProperty); set => SetValue(ShowsGutterProperty, value); }

    public IBrush? KeyBrush { get => GetValue(KeyBrushProperty); set => SetValue(KeyBrushProperty, value); }

    public IBrush? StringBrush { get => GetValue(StringBrushProperty); set => SetValue(StringBrushProperty, value); }

    public IBrush? NumberBrush { get => GetValue(NumberBrushProperty); set => SetValue(NumberBrushProperty, value); }

    public IBrush? LiteralBrush { get => GetValue(LiteralBrushProperty); set => SetValue(LiteralBrushProperty, value); }

    public IBrush? PunctuationBrush { get => GetValue(PunctuationBrushProperty); set => SetValue(PunctuationBrushProperty, value); }

    /// <summary>The number of lines the gutter shows.</summary>
    public int LineCount => _lines;

    protected override Type StyleKeyOverride => typeof(WebhookTesterEditor);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_scroll is not null)
        {
            _scroll.ScrollChanged -= OnScrollChanged;
        }
        base.OnApplyTemplate(e);
        _tint = e.NameScope.Find<TextBlock>(TintPart);
        _gutter = e.NameScope.Find<TextBlock>(GutterPart);
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollPart);
        if (_scroll is not null)
        {
            _scroll.ScrollChanged += OnScrollChanged;
        }
        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsTintedProperty)
        {
            PseudoClasses.Set(PlainPseudoClass, !IsTinted);
        }
        else if (change.Property == ShowsGutterProperty)
        {
            PseudoClasses.Set(NoGutterPseudoClass, !ShowsGutter);
        }
        if (change.Property == TextProperty || change.Property == IsTintedProperty || change.Property == KeyBrushProperty || change.Property == StringBrushProperty
            || change.Property == NumberBrushProperty || change.Property == LiteralBrushProperty
            || change.Property == PunctuationBrushProperty)
        {
            Rebuild();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None && !IsReadOnly)
        {
            var text = Text ?? string.Empty;
            SelectedText = WebhookTesterJson.NewLine(text, Math.Min(SelectionStart, SelectionEnd));
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_gutter is not null && _scroll is not null)
        {
            _gutter.RenderTransform = new TranslateTransform(0, -_scroll.Offset.Y);
        }
    }

    private void Rebuild()
    {
        var text = Text ?? string.Empty;
        _lines = WebhookTesterJson.LineCount(text);
        if (_gutter is not null)
        {
            _gutter.Text = string.Join('\n', Enumerable.Range(1, _lines));
        }
        if (_tint is null)
        {
            return;
        }
        _tint.Inlines?.Clear();
        _tint.Inlines ??= new InlineCollection();
        if (!IsTinted)
        {
            return;
        }
        foreach (var (start, length, kind) in WebhookTesterJson.Tokenize(text))
        {
            var brush = kind switch
            {
                WebhookTesterSyntax.Key => KeyBrush,
                WebhookTesterSyntax.String => StringBrush,
                WebhookTesterSyntax.Number => NumberBrush,
                WebhookTesterSyntax.Literal => LiteralBrush,
                WebhookTesterSyntax.Punctuation => PunctuationBrush,
                _ => null
            };
            var run = new Run(text.Substring(start, length));
            if (brush is not null)
            {
                run.Foreground = brush;
            }
            _tint.Inlines.Add(run);
        }
    }
}

/// <summary>
/// The line between two steps of the timeline: a track, a fill that grows with <see cref="SetProgress"/> and, while
/// <see cref="IsTraveling"/>, a dot that loops along the line.
/// <para>
/// The fill is a <see cref="MotionValue{T}"/> rather than a theme transition because the block drives it from async phases
/// (signed, in flight, answered) and a new target must continue from wherever the fill is.
/// </para>
/// </summary>
internal sealed class WebhookTesterConnector : Control
{
    public static readonly StyledProperty<IBrush?> TrackBrushProperty = AvaloniaProperty.Register<WebhookTesterConnector, IBrush?>(nameof(TrackBrush));
    public static readonly StyledProperty<IBrush?> FillBrushProperty = AvaloniaProperty.Register<WebhookTesterConnector, IBrush?>(nameof(FillBrush));
    public static readonly StyledProperty<IBrush?> DotBrushProperty = AvaloniaProperty.Register<WebhookTesterConnector, IBrush?>(nameof(DotBrush));
    public static readonly StyledProperty<double> ThicknessProperty = AvaloniaProperty.Register<WebhookTesterConnector, double>(nameof(Thickness), 2);
    public static readonly StyledProperty<double> DotSizeProperty = AvaloniaProperty.Register<WebhookTesterConnector, double>(nameof(DotSize), 6);

    private readonly MotionValue<double> _fill;
    private readonly LiveLoop _loop;
    private double _phase;
    private bool _traveling;

    static WebhookTesterConnector()
    {
        AffectsRender<WebhookTesterConnector>(TrackBrushProperty, FillBrushProperty, DotBrushProperty, ThicknessProperty, DotSizeProperty);
    }

    public WebhookTesterConnector()
    {
        _fill = new MotionValue<double>(this, 0, MotionVectors.Double);
        _fill.Changed += _ => InvalidateVisual();
        _loop = new LiveLoop(this, dt =>
        {
            _phase = (_phase + dt / 1.1) % 1;
            InvalidateVisual();
            return true;
        });
        _loop.RunningChanged += _ => InvalidateVisual();
        MinWidth = 16;
        Height = 22;
        IsHitTestVisible = false;
    }

    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    public IBrush? DotBrush { get => GetValue(DotBrushProperty); set => SetValue(DotBrushProperty, value); }

    public double Thickness { get => GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }

    public double DotSize { get => GetValue(DotSizeProperty); set => SetValue(DotSizeProperty, value); }

    /// <summary>How much of the line is filled, 0 to 1 (the value the animation is heading for).</summary>
    public double Progress => _fill.Target;

    /// <summary>Whether the dot runs along the line (only at full motion).</summary>
    public bool IsTraveling
    {
        get => _traveling;
        set
        {
            if (_traveling == value)
            {
                return;
            }
            _traveling = value;
            if (value)
            {
                _phase = 0;
                _loop.Start();
            }
            else
            {
                _loop.Stop();
            }
            InvalidateVisual();
        }
    }

    /// <summary>Fills the line to <paramref name="target"/>: animated at full motion, at once otherwise.</summary>
    public void SetProgress(double target)
    {
        target = Math.Clamp(target, 0, 1);
        if (!ArcMotion.IsFull(this) || !this.IsAttachedToVisualTree())
        {
            _fill.Jump(target);
            return;
        }
        _ = _fill.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? MinWidth : availableSize.Width, Height);

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var y = Bounds.Height / 2;
        var thickness = Thickness;
        if (TrackBrush is { } track)
        {
            context.DrawRectangle(track, null, new Rect(0, y - thickness / 2, w, thickness), thickness / 2, thickness / 2);
        }
        var fill = _fill.Value;
        if (FillBrush is { } brush && fill > 0)
        {
            context.DrawRectangle(brush, null, new Rect(0, y - thickness / 2, w * fill, thickness), thickness / 2, thickness / 2);
        }
        if (_traveling && _loop.IsRunning && DotBrush is { } dot)
        {
            var size = DotSize;
            var eased = _phase * _phase * (3 - 2 * _phase);
            context.DrawEllipse(dot, null, new Point(eased * w, y), size / 2, size / 2);
        }
    }
}
