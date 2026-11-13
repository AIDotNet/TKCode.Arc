// Ported from Arc's free comparison-table block (MIT, Copyright (c) 2026 Elia Kuratli): the rows and marks of comparison-table.tsx and .module.css.
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>Lays out one table row: the feature column and equal product columns by weight (Arc's <c>--grid</c>). Children stretch to the row's height.</summary>
internal sealed class ComparisonRowPanel : Panel
{
    public double FeatureWeight { get; private set; } = 1.7;

    public double ColumnWeight { get; private set; } = 1;

    public void SetWeights(double feature, double column)
    {
        if (feature != FeatureWeight || column != ColumnWeight)
        {
            FeatureWeight = feature;
            ColumnWeight = column;
            InvalidateMeasure();
        }
    }

    private (double Feature, double Column) Widths(double width)
    {
        var columns = 0;
        for (var i = 1; i < Children.Count; i++)
        {
            if (Children[i].IsVisible)
            {
                columns++;
            }
        }
        var unit = width / (FeatureWeight + columns * ColumnWeight);
        return (unit * FeatureWeight, unit * ColumnWeight);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 640 : availableSize.Width;
        var (feature, column) = Widths(width);
        var height = 0.0;
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            child.Measure(new Size(i == 0 ? feature : column, double.PositiveInfinity));
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (feature, column) = Widths(finalSize.Width);
        var x = 0.0;
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (!child.IsVisible)
            {
                continue;
            }
            var width = i == 0 ? feature : column;
            child.Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width;
        }
        return finalSize;
    }
}

/// <summary>
/// A table cell's ground: your column's band (the accent over the surface), the hover fill and the row's bottom rule, drawn
/// once per cell. The band and hover fade through <see cref="HoverAmount"/> in the theme.
/// </summary>
[PseudoClasses(":row-hover")]
internal class ComparisonCellGround : Decorator
{
    public static readonly StyledProperty<bool> IsOwnProperty =
        AvaloniaProperty.Register<ComparisonCellGround, bool>(nameof(IsOwn));

    public static readonly StyledProperty<bool> HasRuleProperty =
        AvaloniaProperty.Register<ComparisonCellGround, bool>(nameof(HasRule));

    public static readonly StyledProperty<double> HoverAmountProperty =
        AvaloniaProperty.Register<ComparisonCellGround, double>(nameof(HoverAmount));

    public static readonly StyledProperty<CornerRadius> BandCornersProperty =
        AvaloniaProperty.Register<ComparisonCellGround, CornerRadius>(nameof(BandCorners));

    public static readonly StyledProperty<IBrush?> BandBaseBrushProperty =
        AvaloniaProperty.Register<ComparisonCellGround, IBrush?>(nameof(BandBaseBrush));

    public static readonly StyledProperty<IBrush?> BandBrushProperty =
        AvaloniaProperty.Register<ComparisonCellGround, IBrush?>(nameof(BandBrush));

    public static readonly StyledProperty<IBrush?> HoverBrushProperty =
        AvaloniaProperty.Register<ComparisonCellGround, IBrush?>(nameof(HoverBrush));

    public static readonly StyledProperty<IBrush?> RuleBrushProperty =
        AvaloniaProperty.Register<ComparisonCellGround, IBrush?>(nameof(RuleBrush));

    public static readonly StyledProperty<double> BandOpacityProperty =
        AvaloniaProperty.Register<ComparisonCellGround, double>(nameof(BandOpacity), 0.06);

    public static readonly StyledProperty<double> BandHoverOpacityProperty =
        AvaloniaProperty.Register<ComparisonCellGround, double>(nameof(BandHoverOpacity), 0.09);

    static ComparisonCellGround()
    {
        AffectsRender<ComparisonCellGround>(IsOwnProperty, HasRuleProperty, HoverAmountProperty, BandCornersProperty, BandBaseBrushProperty,
            BandBrushProperty, HoverBrushProperty, RuleBrushProperty, BandOpacityProperty, BandHoverOpacityProperty);
    }

    public bool IsOwn { get => GetValue(IsOwnProperty); set => SetValue(IsOwnProperty, value); }

    public bool HasRule { get => GetValue(HasRuleProperty); set => SetValue(HasRuleProperty, value); }

    public double HoverAmount { get => GetValue(HoverAmountProperty); set => SetValue(HoverAmountProperty, value); }

    public CornerRadius BandCorners { get => GetValue(BandCornersProperty); set => SetValue(BandCornersProperty, value); }

    public IBrush? BandBaseBrush { get => GetValue(BandBaseBrushProperty); set => SetValue(BandBaseBrushProperty, value); }

    public IBrush? BandBrush { get => GetValue(BandBrushProperty); set => SetValue(BandBrushProperty, value); }

    public IBrush? HoverBrush { get => GetValue(HoverBrushProperty); set => SetValue(HoverBrushProperty, value); }

    public IBrush? RuleBrush { get => GetValue(RuleBrushProperty); set => SetValue(RuleBrushProperty, value); }

    public double BandOpacity { get => GetValue(BandOpacityProperty); set => SetValue(BandOpacityProperty, value); }

    public double BandHoverOpacity { get => GetValue(BandHoverOpacityProperty); set => SetValue(BandHoverOpacityProperty, value); }

    public void SetRowHover(bool hover) => PseudoClasses.Set(":row-hover", hover);

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var amount = HoverAmount;
        if (IsOwn)
        {
            var box = new RoundedRect(bounds, BandCorners);
            context.DrawRectangle(BandBaseBrush, null, box);
            using (context.PushOpacity(BandOpacity + (BandHoverOpacity - BandOpacity) * amount))
            {
                context.DrawRectangle(BandBrush, null, box);
            }
        }
        else if (amount > 0)
        {
            using (context.PushOpacity(amount))
            {
                context.DrawRectangle(HoverBrush, null, bounds);
            }
        }
        if (HasRule && RuleBrush is { } rule)
        {
            context.FillRectangle(rule, new Rect(0, bounds.Height - 1, bounds.Width, 1));
        }
    }
}

/// <summary>
/// One product's value in a row, drawn as one control: a check that can draw itself in, a half circle, a dash or printed text.
/// A change of column pops the old value out and the new one in (opacity and scale .8 on Arc's snappy spring).
/// </summary>
internal sealed class ComparisonCell : ComparisonCellGround
{
    private const double MarkSize = 22;
    private const double Inset = 8;

    public static readonly StyledProperty<IBrush?> OnBrushProperty =
        AvaloniaProperty.Register<ComparisonCell, IBrush?>(nameof(OnBrush));

    public static readonly StyledProperty<IBrush?> GlyphBrushProperty =
        AvaloniaProperty.Register<ComparisonCell, IBrush?>(nameof(GlyphBrush));

    public static readonly StyledProperty<IBrush?> CircleBrushProperty =
        AvaloniaProperty.Register<ComparisonCell, IBrush?>(nameof(CircleBrush));

    public static readonly StyledProperty<IBrush?> InkBrushProperty =
        AvaloniaProperty.Register<ComparisonCell, IBrush?>(nameof(InkBrush));

    public static readonly StyledProperty<IBrush?> SecondaryBrushProperty =
        AvaloniaProperty.Register<ComparisonCell, IBrush?>(nameof(SecondaryBrush));

    public static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<ComparisonCell, IBrush?>(nameof(MutedBrush));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<ComparisonCell>();

    private ComparisonValue _value;
    private ComparisonValue _previous;
    private bool _hasPrevious;
    private TextLayout? _text;
    private TextLayout? _note;
    private TextLayout? _previousText;
    private TextLayout? _previousNote;
    private double _measuredWidth = -1;
    private MotionValue<double>? _swap;
    private double _swapAmount = 1;
    private MotionValue<double>? _draw;
    private double _drawAmount = 1;
    private Pen? _checkPen;
    private Pen? _ownCheckPen;
    private Pen? _partialPen;
    private Pen? _dashPen;

    static ComparisonCell()
    {
        AffectsRender<ComparisonCell>(OnBrushProperty, GlyphBrushProperty, CircleBrushProperty, InkBrushProperty, SecondaryBrushProperty, MutedBrushProperty);
        AffectsMeasure<ComparisonCell>(FontFamilyProperty, IsOwnProperty);
    }

    public IBrush? OnBrush { get => GetValue(OnBrushProperty); set => SetValue(OnBrushProperty, value); }

    public IBrush? GlyphBrush { get => GetValue(GlyphBrushProperty); set => SetValue(GlyphBrushProperty, value); }

    public IBrush? CircleBrush { get => GetValue(CircleBrushProperty); set => SetValue(CircleBrushProperty, value); }

    public IBrush? InkBrush { get => GetValue(InkBrushProperty); set => SetValue(InkBrushProperty, value); }

    public IBrush? SecondaryBrush { get => GetValue(SecondaryBrushProperty); set => SetValue(SecondaryBrushProperty, value); }

    public IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public ComparisonValue Value => _value;

    /// <summary>How far the check has drawn itself, 0 to 1.</summary>
    public double DrawAmount => _drawAmount;

    /// <summary>Shows <paramref name="value"/> at once.</summary>
    public void SetValueInstantly(ComparisonValue value)
    {
        _value = value;
        _hasPrevious = false;
        _swap?.Jump(1);
        _swapAmount = 1;
        DropText();
    }

    /// <summary>Pops <paramref name="value"/> in over the current one.</summary>
    public void PopTo(ComparisonValue value)
    {
        _previous = _value;
        _previousText = _text;
        _previousNote = _note;
        _text = null;
        _note = null;
        _measuredWidth = -1;
        _hasPrevious = true;
        _value = value;
        if (_swap is null)
        {
            _swap = new MotionValue<double>(this, 0, MotionVectors.Double);
            _swap.Changed += amount =>
            {
                _swapAmount = amount;
                InvalidateVisual();
            };
        }
        _swap.Jump(0);
        _swapAmount = 0;
        InvalidateMeasure();
        _ = RunPop();
    }

    private async Task RunPop()
    {
        var spec = ArcMotion.IsFull(this) ? MotionSpec.Spring(ArcSpring.Snappy) : MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard);
        if (await _swap!.AnimateTo(1, spec))
        {
            _hasPrevious = false;
            _previousText?.Dispose();
            _previousNote?.Dispose();
            _previousText = null;
            _previousNote = null;
            _swapAmount = 1;
            InvalidateVisual();
        }
    }

    /// <summary>Hides the check's stroke until <see cref="PlayDraw"/>.</summary>
    public void PrepareDraw()
    {
        if (_value.Mark == ComparisonMark.Included)
        {
            _drawAmount = 0;
            InvalidateVisual();
        }
    }

    /// <summary>Draws the check in over 480 ms (Arc: considered, the enter curve, with a stagger in your column).</summary>
    public void PlayDraw(TimeSpan delay)
    {
        if (_drawAmount >= 1)
        {
            return;
        }
        if (_draw is null)
        {
            _draw = new MotionValue<double>(this, _drawAmount, MotionVectors.Double);
            _draw.Changed += amount =>
            {
                _drawAmount = amount;
                InvalidateVisual();
            };
        }
        _ = _draw.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Considered, ArcEasings.Enter, delay));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == FontFamilyProperty || property == IsOwnProperty || property == InkBrushProperty || property == SecondaryBrushProperty || property == MutedBrushProperty)
        {
            DropText();
        }
        if (property == OnBrushProperty || property == GlyphBrushProperty || property == InkBrushProperty || property == MutedBrushProperty)
        {
            _checkPen = _ownCheckPen = _partialPen = _dashPen = null;
        }
    }

    private void DropText()
    {
        _text?.Dispose();
        _note?.Dispose();
        _text = null;
        _note = null;
        _measuredWidth = -1;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void EnsureText(double width)
    {
        if (_measuredWidth == width && (_text is not null || _value.Mark != ComparisonMark.Text) && (_note is not null || string.IsNullOrEmpty(_value.Note)))
        {
            return;
        }
        _text?.Dispose();
        _note?.Dispose();
        _text = null;
        _note = null;
        _measuredWidth = width;
        var family = FontFamily;
        if (_value.Mark == ComparisonMark.Text && !string.IsNullOrEmpty(_value.Text))
        {
            var typeface = new Typeface(family, FontStyle.Normal, IsOwn ? FontWeight.Medium : FontWeight.Normal);
            _text = new TextLayout(_value.Text, typeface, 14, IsOwn ? InkBrush : SecondaryBrush, TextAlignment.Center, TextWrapping.Wrap,
                maxWidth: width, lineHeight: 19.6, letterSpacing: -0.14);
        }
        else if (_value.Mark == ComparisonMark.Partial && !string.IsNullOrEmpty(_value.Note))
        {
            // Arc: at most 12ch of extra small text.
            _note = new TextLayout(_value.Note, new Typeface(family), 12, MutedBrush, TextAlignment.Center, TextWrapping.Wrap,
                maxWidth: Math.Min(width, 84), lineHeight: 16.8, letterSpacing: -0.12);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = Math.Max(1, (double.IsInfinity(availableSize.Width) ? 160 : availableSize.Width) - Inset * 2);
        EnsureText(width);
        var height = _value.Mark switch
        {
            ComparisonMark.Text => _text?.Height ?? 0,
            ComparisonMark.Partial => MarkSize + (_note is null ? 0 : 2 + _note.Height),
            _ => MarkSize
        };
        return new Size(availableSize.Width, height + Inset * 2);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        if (_hasPrevious)
        {
            DrawLayer(context, center, _previous, _previousText, _previousNote, 1 - _swapAmount, 1 - 0.2 * Math.Clamp(_swapAmount, 0, 1));
            DrawLayer(context, center, _value, _text, _note, _swapAmount, 0.8 + 0.2 * _swapAmount);
        }
        else
        {
            DrawContent(context, center, _value, _text, _note);
        }
    }

    private void DrawLayer(DrawingContext context, Point center, ComparisonValue value, TextLayout? text, TextLayout? note, double opacity, double scale)
    {
        var motionScale = ArcMotion.IsFull(this) ? scale : 1;
        using (context.PushOpacity(Math.Clamp(opacity, 0, 1)))
        using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(motionScale, motionScale) * Matrix.CreateTranslation(center.X, center.Y)))
        {
            DrawContent(context, center, value, text, note);
        }
    }

    private void DrawContent(DrawingContext context, Point center, ComparisonValue value, TextLayout? text, TextLayout? note)
    {
        switch (value.Mark)
        {
            case ComparisonMark.Text:
                text?.Draw(context, new Point(center.X - (text.MaxWidth > 0 ? text.MaxWidth : text.Width) / 2, center.Y - text.Height / 2));
                break;
            case ComparisonMark.Included:
                DrawCheck(context, new Point(center.X - MarkSize / 2, center.Y - MarkSize / 2), IsOwn, _drawAmount);
                break;
            case ComparisonMark.Partial:
                var total = MarkSize + (note is null ? 0 : 2 + note.Height);
                var top = center.Y - total / 2;
                DrawPartial(context, new Point(center.X - MarkSize / 2, top));
                note?.Draw(context, new Point(center.X - note.MaxWidth / 2, top + MarkSize + 2));
                break;
            default:
                DrawDash(context, center);
                break;
        }
    }

    /// <summary>The check of a 22 box: a circle of radius 10 and the path <c>M6.6 11.3l3 3 5.9-6.4</c>, drawn up to <paramref name="amount"/>.</summary>
    private void DrawCheck(DrawingContext context, Point origin, bool own, double amount)
    {
        context.DrawEllipse(own ? OnBrush : CircleBrush, null, new Point(origin.X + 11, origin.Y + 11), 10, 10);
        var pen = own
            ? _ownCheckPen ??= new Pen(GlyphBrush, 1.9, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round)
            : _checkPen ??= new Pen(InkBrush, 1.9, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        if (amount <= 0)
        {
            return;
        }
        var a = new Point(origin.X + 6.6, origin.Y + 11.3);
        var b = new Point(a.X + 3, a.Y + 3);
        var c = new Point(b.X + 5.9, b.Y - 6.4);
        const double first = 4.2426;
        const double second = 8.7047;
        var drawn = Math.Clamp(amount, 0, 1) * (first + second);
        if (drawn <= first)
        {
            context.DrawLine(pen, a, a + (b - a) * (drawn / first));
        }
        else
        {
            context.DrawLine(pen, a, b);
            context.DrawLine(pen, b, b + (c - b) * ((drawn - first) / second));
        }
    }

    private static readonly Geometry s_half = Geometry.Parse("M11 1.75a9.25 9.25 0 0 1 0 18.5z");

    private void DrawPartial(DrawingContext context, Point origin)
    {
        _partialPen ??= new Pen(MutedBrush, 1.5);
        context.DrawEllipse(null, _partialPen, new Point(origin.X + 11, origin.Y + 11), 9.25, 9.25);
        using (context.PushTransform(Matrix.CreateTranslation(origin.X, origin.Y)))
        {
            context.DrawGeometry(MutedBrush, null, s_half);
        }
    }

    /// <summary>Lucide's minus at 18 with a stroke of 1.75 in its 24 box, at 70%.</summary>
    private void DrawDash(DrawingContext context, Point center)
    {
        _dashPen ??= new Pen(MutedBrush, 1.3125, lineCap: PenLineCap.Round);
        using (context.PushOpacity(0.7))
        {
            context.DrawLine(_dashPen, new Point(center.X - 5.25, center.Y), new Point(center.X + 5.25, center.Y));
        }
    }
}

/// <summary>One mark of the legend: 16 for the check and half circle, 14 for the dash.</summary>
internal sealed class ComparisonLegendMark : Control
{
    private static readonly Geometry s_check = Geometry.Parse("M6.6 11.3l3 3 5.9-6.4");
    private static readonly Geometry s_half = Geometry.Parse("M11 1.75a9.25 9.25 0 0 1 0 18.5z");

    public static readonly StyledProperty<ComparisonMark> MarkProperty =
        AvaloniaProperty.Register<ComparisonLegendMark, ComparisonMark>(nameof(Mark));

    public static readonly StyledProperty<IBrush?> InkBrushProperty =
        AvaloniaProperty.Register<ComparisonLegendMark, IBrush?>(nameof(InkBrush));

    public static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<ComparisonLegendMark, IBrush?>(nameof(MutedBrush));

    public static readonly StyledProperty<IBrush?> CircleBrushProperty =
        AvaloniaProperty.Register<ComparisonLegendMark, IBrush?>(nameof(CircleBrush));

    static ComparisonLegendMark()
    {
        AffectsRender<ComparisonLegendMark>(MarkProperty, InkBrushProperty, MutedBrushProperty, CircleBrushProperty);
    }

    public ComparisonMark Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    public IBrush? InkBrush { get => GetValue(InkBrushProperty); set => SetValue(InkBrushProperty, value); }

    public IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }

    public IBrush? CircleBrush { get => GetValue(CircleBrushProperty); set => SetValue(CircleBrushProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(16, 16);

    public override void Render(DrawingContext context)
    {
        var center = new Point(8, 8);
        if (Mark == ComparisonMark.NotIncluded)
        {
            using (context.PushOpacity(0.7))
            {
                context.DrawLine(new Pen(MutedBrush, 1.02, lineCap: PenLineCap.Round), new Point(center.X - 4.08, 8), new Point(center.X + 4.08, 8));
            }
            return;
        }
        var scale = 16.0 / 22;
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            if (Mark == ComparisonMark.Included)
            {
                context.DrawEllipse(CircleBrush, null, new Point(11, 11), 10, 10);
                context.DrawGeometry(null, new Pen(InkBrush, 2.2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), s_check);
            }
            else
            {
                context.DrawEllipse(null, new Pen(MutedBrush, 1.5), new Point(11, 11), 9.25, 9.25);
                context.DrawGeometry(MutedBrush, null, s_half);
            }
        }
    }
}

/// <summary>The call to action at the foot of your column: a pill whose width and label morph to the done state.</summary>
[PseudoClasses(":done")]
internal sealed class ComparisonCtaButton : BlockButton
{
    private BlockSwap? _swap;
    private bool _done;

    private static readonly BlockSwapMotion s_motion = new()
    {
        EnterY = 8,
        EnterBlur = 2,
        ExitY = -8,
        ExitBlur = 2,
        EnterFade = MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard),
        EnterMove = MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard),
        Exit = MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard),
    };

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ComparisonCtaButton, string?>(nameof(Label));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The label after a press.</summary>
    public string? DoneLabel { get; set; }

    public bool IsDone => _done;

    /// <summary>The label shown.</summary>
    public string? CurrentText => _done ? DoneLabel : Label;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _swap = e.NameScope.Find<BlockSwap>("PART_Swap");
        _swap?.Set(CreateContent());
        AutomationProperties.SetName(this, CurrentText);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty && !_done)
        {
            _swap?.Set(CreateContent());
            AutomationProperties.SetName(this, CurrentText);
        }
    }

    public void MarkDone()
    {
        if (_done || string.IsNullOrEmpty(DoneLabel))
        {
            return;
        }
        _done = true;
        PseudoClasses.Set(":done", true);
        AutomationProperties.SetName(this, DoneLabel);
        _swap?.Swap(CreateContent(), s_motion);
    }

    private Control CreateContent()
    {
        var text = new TextBlock { Text = CurrentText, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.NoWrap };
        if (!_done)
        {
            return text;
        }
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        row.Children.Add(new ArcGlyph { Data = ArcIcons.Check, Size = 14, StrokeThickness = 2, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(text);
        return row;
    }
}
