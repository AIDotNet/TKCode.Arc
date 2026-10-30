using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Binds code-built parts of <see cref="UpgradePrompt"/> to its control tokens and to ArcDesign tokens.</summary>
internal static class UpgradePromptTokenBinding
{
    /// <summary>Binds <paramref name="property"/> to an <see cref="UpgradePromptToken"/> value and returns the target.</summary>
    public static T PromptTok<T>(this T target, AvaloniaProperty property, UpgradePromptTokenKind kind) where T : StyledElement
    {
        target.Bind(property, target.GetResourceObservable(UpgradePromptTokens.Key(kind)));
        return target;
    }

    /// <summary>Binds <paramref name="property"/> to an ArcDesign token and returns the target.</summary>
    public static T PromptArc<T>(this T target, AvaloniaProperty property, ArcDesignTokenKind kind) where T : StyledElement
    {
        ArcTokens.Bind(target, property, kind);
        return target;
    }
}

/// <summary>
/// A token binding that a state change can point at another token (a status color that turns from warning to success).
/// Rebinding to the same token does nothing.
/// </summary>
internal sealed class UpgradePromptTone
{
    private readonly StyledElement _target;
    private readonly AvaloniaProperty _property;
    private IDisposable? _binding;
    private ArcDesignTokenKind? _kind;

    public UpgradePromptTone(StyledElement target, AvaloniaProperty property)
    {
        _target = target;
        _property = property;
    }

    /// <summary>The token the property follows now.</summary>
    public ArcDesignTokenKind? Kind => _kind;

    public void Set(ArcDesignTokenKind kind)
    {
        if (_kind == kind)
        {
            return;
        }
        _binding?.Dispose();
        _kind = kind;
        _binding = ArcTokens.Bind(_target, _property, kind);
    }
}

/// <summary>
/// A text value of <see cref="UpgradePrompt"/> that crossfades in place when it changes (Core <see cref="ArcContentSwap"/>,
/// Label: out 100ms, in 160ms with a 2px rise; reduced motion: a plain fade) and reserves <see cref="Lines"/> lines, so a
/// longer or shorter value never moves what is below it.
/// </summary>
internal sealed class UpgradePromptText : Control
{
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<UpgradePromptText>();

    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<UpgradePromptText>();

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<UpgradePromptText>();

    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<UpgradePromptText>();

    public static readonly StyledProperty<double> LetterSpacingProperty = TextElement.LetterSpacingProperty.AddOwner<UpgradePromptText>();

    /// <summary>The height of one line; the reserved height is <see cref="Lines"/> times this.</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<UpgradePromptText, double>(nameof(LineHeight), 20);

    /// <summary>How many lines are reserved; longer text is clamped with an ellipsis.</summary>
    public static readonly StyledProperty<int> LinesProperty =
        AvaloniaProperty.Register<UpgradePromptText, int>(nameof(Lines), 1);

    /// <summary>Which edge the text keeps to.</summary>
    public static readonly StyledProperty<TextAlignment> TextAlignmentProperty =
        AvaloniaProperty.Register<UpgradePromptText, TextAlignment>(nameof(TextAlignment));

    private readonly ArcContentSwap _swap;
    private TextBlock? _current;

    static UpgradePromptText()
    {
        AffectsMeasure<UpgradePromptText>(LineHeightProperty, LinesProperty);
    }

    public UpgradePromptText()
    {
        _swap = new ArcContentSwap
        {
            Mode = ContentSwapMode.None,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Top
        };
        VisualChildren.Add(_swap);
        LogicalChildren.Add(_swap);
    }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public double LetterSpacing { get => GetValue(LetterSpacingProperty); set => SetValue(LetterSpacingProperty, value); }

    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    public int Lines { get => GetValue(LinesProperty); set => SetValue(LinesProperty, value); }

    public TextAlignment TextAlignment { get => GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }

    /// <summary>The text on screen.</summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>Shows <paramref name="text"/>; <paramref name="animate"/> crossfades it in (a plain fade under reduced motion).</summary>
    public void Show(string text, bool animate)
    {
        if (_current is not null && text == Text)
        {
            return;
        }
        Text = text;
        AutomationProperties.SetName(this, text);
        var level = ArcMotion.GetEffectiveLevel(this);
        _swap.Mode = animate && _current is not null && IsLoaded && level != ArcMotionLevel.Off ? ContentSwapMode.Label : ContentSwapMode.None;
        _current = new TextBlock { Text = text };
        Style(_current);
        _swap.Content = _current;
    }

    private void Style(TextBlock block)
    {
        var lines = Math.Max(1, Lines);
        block.LineHeight = LineHeight;
        block.MaxLines = lines;
        block.TextWrapping = lines > 1 ? TextWrapping.Wrap : TextWrapping.NoWrap;
        block.TextTrimming = TextTrimming.CharacterEllipsis;
        block.TextAlignment = TextAlignment;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LineHeightProperty || change.Property == LinesProperty || change.Property == TextAlignmentProperty)
        {
            _swap.HorizontalContentAlignment = TextAlignment == TextAlignment.Right ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            if (_current is not null)
            {
                Style(_current);
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _swap.Measure(availableSize);
        var height = Math.Max(1, Lines) * LineHeight;
        var width = double.IsFinite(availableSize.Width) ? Math.Min(_swap.DesiredSize.Width, availableSize.Width) : _swap.DesiredSize.Width;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _swap.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>
/// The usage bar of <see cref="UpgradePrompt"/>: a pill track with a fill that slides in from the left. The fill is a pill
/// as wide as the track moved by a translation and clipped to the track, so its rounded end never stretches. The share
/// moves on a 0.5s bounce-free spring through a <see cref="MotionValue{T}"/> (code-driven because the target comes from
/// usage and the plan, and the first fill waits for the bar to scroll into view); reduced motion and Off jump.
/// </summary>
internal sealed class UpgradePromptMeterBar : Control
{
    /// <summary>The fill spring: visual duration 0.5s, no bounce.</summary>
    internal static readonly SpringSpec FillSpring = SpringSpec.FromVisual(0.5, 0);

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<UpgradePromptMeterBar, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<UpgradePromptMeterBar, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<double> BarHeightProperty =
        AvaloniaProperty.Register<UpgradePromptMeterBar, double>(nameof(BarHeight), 8);

    private MotionValue<double>? _fill;
    private double _shown;

    static UpgradePromptMeterBar()
    {
        AffectsRender<UpgradePromptMeterBar>(TrackBrushProperty, FillBrushProperty);
        AffectsMeasure<UpgradePromptMeterBar>(BarHeightProperty);
    }

    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    public double BarHeight { get => GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }

    /// <summary>The share the fill is heading to (0–1).</summary>
    public double Target { get; private set; }

    /// <summary>The share drawn now (0–1).</summary>
    public double Shown => _shown;

    /// <summary>Moves the fill to <paramref name="share"/>; <paramref name="animate"/> uses the spring (Full motion only).</summary>
    public void SetShare(double share, bool animate)
    {
        share = Math.Clamp(share, 0, 1);
        Target = share;
        _fill ??= CreateFill();
        if (animate && IsLoaded && ArcMotion.IsFull(this))
        {
            _ = _fill.AnimateTo(share, MotionSpec.Spring(FillSpring));
        }
        else
        {
            _fill.Jump(share);
            _shown = share;
            InvalidateVisual();
        }
    }

    private MotionValue<double> CreateFill()
    {
        var fill = new MotionValue<double>(this, _shown, MotionVectors.Double);
        fill.Changed += value =>
        {
            _shown = value;
            InvalidateVisual();
        };
        return fill;
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsFinite(availableSize.Width) ? availableSize.Width : 0, BarHeight);

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }
        var radius = height / 2;
        var track = new RoundedRect(new Rect(0, 0, width, height), radius);
        if (TrackBrush is { } trackBrush)
        {
            context.DrawRectangle(trackBrush, null, track);
        }
        if (_shown <= 0 || FillBrush is not { } fillBrush)
        {
            return;
        }
        using (context.PushClip(track))
        {
            var offset = (_shown - 1) * width;
            context.DrawRectangle(fillBrush, null, new RoundedRect(new Rect(offset, 0, width, height), radius));
        }
    }
}

/// <summary>
/// Lays out the price and the billing toggle side by side (price on the left, the toggle on the right, centered on each
/// other) while both fit, and puts the toggle under the price when they do not.
/// </summary>
internal sealed class UpgradePromptPriceRow : Panel
{
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<UpgradePromptPriceRow, double>(nameof(Gap), 16);

    static UpgradePromptPriceRow()
    {
        AffectsMeasure<UpgradePromptPriceRow>(GapProperty);
    }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>True when the toggle sits under the price.</summary>
    public bool IsStacked { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return default;
        }
        var unbounded = new Size(double.PositiveInfinity, availableSize.Height);
        var price = Children[0];
        var toggle = Children[1];
        price.Measure(unbounded);
        toggle.Measure(unbounded);
        var sideBySide = price.DesiredSize.Width + Gap + toggle.DesiredSize.Width;
        IsStacked = double.IsFinite(availableSize.Width) && sideBySide > availableSize.Width;
        if (IsStacked)
        {
            price.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            toggle.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            return new Size(Math.Max(price.DesiredSize.Width, toggle.DesiredSize.Width),
                price.DesiredSize.Height + Gap / 2 + toggle.DesiredSize.Height);
        }
        return new Size(sideBySide, Math.Max(price.DesiredSize.Height, toggle.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return finalSize;
        }
        var price = Children[0];
        var toggle = Children[1];
        if (IsStacked)
        {
            price.Arrange(new Rect(0, 0, finalSize.Width, price.DesiredSize.Height));
            toggle.Arrange(new Rect(0, price.DesiredSize.Height + Gap / 2, toggle.DesiredSize.Width, toggle.DesiredSize.Height));
            return finalSize;
        }
        var height = finalSize.Height;
        price.Arrange(new Rect(0, (height - price.DesiredSize.Height) / 2, price.DesiredSize.Width, price.DesiredSize.Height));
        var toggleWidth = toggle.DesiredSize.Width;
        toggle.Arrange(new Rect(finalSize.Width - toggleWidth, (height - toggle.DesiredSize.Height) / 2, toggleWidth, toggle.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>
/// The panels of one cell of <see cref="UpgradePrompt"/> (the body or the footer). They stay mounted in one grid cell, so
/// the cell keeps the tallest panel's size; the active one fades in over 160ms with a 4px rise while the old one fades out
/// in 100ms. Inactive panels are inert: not hit-testable, skipped by Tab and hidden from assistive technology.
/// </summary>
/// <remarks>
/// Code-driven (<see cref="MotionValue{T}"/>) because the two directions have different durations and delays and a
/// change can interrupt another mid-fade; <see cref="ArcContentSwap"/> lets the outgoing content leave the layout, which
/// would let the card change size.
/// </remarks>
internal sealed class UpgradePromptPanels
{
    internal static readonly TimeSpan FadeIn = TimeSpan.FromMilliseconds(160);
    internal static readonly TimeSpan FadeOut = TimeSpan.FromMilliseconds(100);
    internal const double Rise = 4;

    private readonly Grid _grid = new();
    private readonly List<Control> _panels = [];
    private readonly List<MotionValue<double>> _fades = [];
    private readonly List<TranslateTransform> _shifts = [];

    public Grid Host => _grid;

    /// <summary>The index of the panel on screen, or −1 before the first <see cref="Show"/>.</summary>
    public int Active { get; private set; } = -1;

    public IReadOnlyList<Control> Panels => _panels;

    public int Add(Control panel)
    {
        var shift = new TranslateTransform();
        panel.RenderTransform = shift;
        panel.Opacity = 0;
        panel.IsHitTestVisible = false;
        KeyboardNavigation.SetTabNavigation(panel, KeyboardNavigationMode.None);
        AutomationProperties.SetAccessibilityView(panel, AccessibilityView.Raw);
        _panels.Add(panel);
        _shifts.Add(shift);
        var fade = new MotionValue<double>(panel, 0, MotionVectors.Double);
        fade.Changed += value =>
        {
            panel.Opacity = value;
            shift.Y = panel.IsHitTestVisible ? (1 - value) * Rise * RiseScale(panel) : 0;
        };
        _fades.Add(fade);
        _grid.Children.Add(panel);
        return _panels.Count - 1;
    }

    private static double RiseScale(Control panel) => ArcMotion.IsFull(panel) ? 1 : 0;

    /// <summary>Shows panel <paramref name="index"/>; returns true when keyboard focus was inside a panel that just left.</summary>
    public bool Show(int index, bool animate, Visual? focused)
    {
        if (Active == index)
        {
            return false;
        }
        var first = Active < 0;
        Active = index;
        var lostFocus = false;
        for (var i = 0; i < _panels.Count; i++)
        {
            var panel = _panels[i];
            var active = i == index;
            panel.IsHitTestVisible = active;
            KeyboardNavigation.SetTabNavigation(panel, active ? KeyboardNavigationMode.Continue : KeyboardNavigationMode.None);
            AutomationProperties.SetAccessibilityView(panel, active ? AccessibilityView.Default : AccessibilityView.Raw);
            if (!active && focused is not null && (panel == focused || panel.IsVisualAncestorOf(focused)))
            {
                lostFocus = true;
            }
            var fade = _fades[i];
            var goal = active ? 1.0 : 0.0;
            var level = ArcMotion.GetEffectiveLevel(panel);
            if (animate && !first && panel.IsLoaded && level != ArcMotionLevel.Off)
            {
                // The new panel waits for the old one to clear (out 100ms, in 160ms after it).
                var spec = active
                    ? MotionSpec.Tween(FadeIn, ArcEasings.Enter, FadeOut)
                    : MotionSpec.Tween(FadeOut, ArcEasings.Exit);
                if (active && level == ArcMotionLevel.Full)
                {
                    _shifts[i].Y = (1 - fade.Value) * Rise;
                }
                _ = fade.AnimateTo(goal, spec);
            }
            else
            {
                fade.Jump(goal);
                panel.Opacity = goal;
                _shifts[i].Y = 0;
            }
        }
        return lostFocus;
    }
}
