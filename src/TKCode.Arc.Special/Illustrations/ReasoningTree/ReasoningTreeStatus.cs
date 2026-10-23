using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The header status of a <see cref="ReasoningTree"/>: an optional small icon (pause, check) and a label in secondary
/// text, swapped together with one quiet label change (the old one fades in 100ms, the new one rises 2px in 160ms), both
/// aligned right so a shorter label never leaves the old one sticking out. While working, the label carries the calm
/// shimmer and has no icon, as measured on the reference. (The shared <c>ExplainerStatus</c> crossfades left-aligned
/// content; this card keeps its own for the right-aligned rise.) Its accessible name is the label.
/// </summary>
internal sealed class ReasoningTreeStatus : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ReasoningTreeStatus, string?>(nameof(Text));

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<ReasoningTreeStatus, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<ExplainerTone> ToneProperty =
        AvaloniaProperty.Register<ReasoningTreeStatus, ExplainerTone>(nameof(Tone));

    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<ReasoningTreeStatus, double>(nameof(IconSize), 14);

    public static readonly StyledProperty<bool> IsBusyProperty =
        AvaloniaProperty.Register<ReasoningTreeStatus, bool>(nameof(IsBusy));

    private readonly ArcContentSwap _swap;
    private bool _dirty = true;

    public ReasoningTreeStatus()
    {
        _swap = new ArcContentSwap
        {
            Mode = ContentSwapMode.Label,
            HorizontalContentAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        VisualChildren.Add(_swap);
        LogicalChildren.Add(_swap);
    }

    /// <summary>The label.</summary>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>The icon (a 24×24 stroked glyph) or null.</summary>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>The icon's color.</summary>
    public ExplainerTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>The icon size in px.</summary>
    public double IconSize { get => GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }

    /// <summary>Work in progress: the label shimmers.</summary>
    public bool IsBusy { get => GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }

    /// <summary>Sets every part at once (one swap).</summary>
    public void Set(string text, Geometry? icon, ExplainerTone tone, double iconSize, bool busy)
    {
        SetCurrentValue(TextProperty, text);
        SetCurrentValue(IconProperty, icon);
        SetCurrentValue(ToneProperty, tone);
        SetCurrentValue(IconSizeProperty, iconSize);
        SetCurrentValue(IsBusyProperty, busy);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == IconProperty || change.Property == ToneProperty ||
            change.Property == IsBusyProperty || change.Property == IconSizeProperty)
        {
            _dirty = true;
            AutomationProperties.SetName(this, Text ?? string.Empty);
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_dirty)
        {
            _dirty = false;
            _swap.Content = BuildContent();
        }
        _swap.Measure(availableSize);
        return _swap.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _swap.Arrange(new Rect(finalSize));
        return finalSize;
    }

    private Control BuildContent()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (Icon is { } data)
        {
            var glyph = new ArcGlyph { Data = data, Size = IconSize, VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, ExplainerStatus.TokenOf(Tone));
            row.Children.Add(glyph);
        }
        var label = new TextShimmer
        {
            Text = Text,
            IsActive = IsBusy,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center
        };
        ArcTokens.Bind(label, TextShimmer.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        row.Children.Add(label);
        return row;
    }
}
