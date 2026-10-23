using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A status of <see cref="PromptCache"/> (a row's outcome, the cache panel's state): a 14px icon and a 14px label in
/// secondary text, swapped together with one crossfade (160ms in, 100ms out). While <see cref="IsBusy"/> the label carries a
/// calm shimmer and no icon, as measured ("Processing", "Reading cache", "Writing"); the shared explainer status adds a
/// spinner instead, which this card does not show. Countdown changes ("Expires in 4:21") replace the label in place.
/// </summary>
internal sealed class PromptCacheStatus : Control
{
    /// <summary>Defines the <see cref="Text"/> property.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<PromptCacheStatus, string?>(nameof(Text));

    /// <summary>Defines the <see cref="HorizontalContentAlignment"/> property.</summary>
    public static readonly StyledProperty<HorizontalAlignment> HorizontalContentAlignmentProperty =
        AvaloniaProperty.Register<PromptCacheStatus, HorizontalAlignment>(nameof(HorizontalContentAlignment), HorizontalAlignment.Left);

    private readonly ArcContentSwap _swap;
    private string? _key;
    private TextBlock? _plain;

    public PromptCacheStatus()
    {
        _swap = new ArcContentSwap
        {
            Mode = ContentSwapMode.Crossfade,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        VisualChildren.Add(_swap);
        LogicalChildren.Add(_swap);
        IsHitTestVisible = false;
        // Every state keeps one line height (an icon row, plain text or a shimmer), so the panel never jumps.
        MinHeight = 19.6;
    }

    /// <summary>The label (also the accessible name).</summary>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>Where the status sits in its slot (rows: right; the cache panel: left).</summary>
    public HorizontalAlignment HorizontalContentAlignment
    {
        get => GetValue(HorizontalContentAlignmentProperty);
        set => SetValue(HorizontalContentAlignmentProperty, value);
    }

    /// <summary>The current icon (null while busy or without one).</summary>
    public Geometry? Icon { get; private set; }

    /// <summary>The icon's color token.</summary>
    public ArcDesignTokenKind IconTone { get; private set; }

    /// <summary>True while the label shimmers.</summary>
    public bool IsBusy { get; private set; }

    /// <summary>
    /// Shows a state. A different icon, tone or busy flag crossfades the whole status; a new label with the same look (a
    /// ticking countdown) only replaces the text.
    /// </summary>
    public void Show(string? text, Geometry? icon, ArcDesignTokenKind tone, bool busy)
    {
        var key = (busy ? "b|" : "s|") + (icon is null ? "-" : icon.GetHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture)) +
                  "|" + (int)tone + "|" + (IsCountdown(text) ? "#" : text);
        Text = text;
        Icon = busy ? null : icon;
        IconTone = tone;
        IsBusy = busy;
        AutomationProperties.SetName(this, text ?? string.Empty);
        if (key == _key)
        {
            if (_plain is not null)
            {
                _plain.Text = text;
            }
            return;
        }
        _key = key;
        _swap.Content = Build(text);
    }

    /// <summary>Clears the status (a queued row) with a crossfade.</summary>
    public void Hide()
    {
        if (_key is null)
        {
            return;
        }
        _key = null;
        _plain = null;
        Text = null;
        Icon = null;
        IsBusy = false;
        AutomationProperties.SetName(this, string.Empty);
        _swap.Content = null;
    }

    // A countdown keeps its slot: only its digits change ("Expires in 4:59" → "4:58").
    private static bool IsCountdown(string? text) => text is not null && text.Length > 0 && char.IsDigit(text[^1]);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HorizontalContentAlignmentProperty)
        {
            _swap.HorizontalContentAlignment = HorizontalContentAlignment;
            _swap.HorizontalAlignment = HorizontalContentAlignment;
        }
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

    private Control Build(string? text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (Icon is { } data)
        {
            var glyph = new ArcGlyph { Data = data, Size = 14, VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, IconTone);
            row.Children.Add(glyph);
        }
        _plain = null;
        if (IsBusy)
        {
            // Work in progress: the label shimmers (it never changes while busy).
            var shimmer = new TextShimmer { Text = text, IsActive = true, FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(shimmer, TextShimmer.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            row.Children.Add(shimmer);
            return row;
        }
        // A resting label is plain text, so a ticking countdown can change in place without a swap.
        _plain = new TextBlock
        {
            Text = text,
            FontSize = 14,
            LineHeight = 19.6,
            VerticalAlignment = VerticalAlignment.Center,
            FontFeatures = PromptCacheText.Tabular
        };
        ArcTokens.Bind(_plain, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        row.Children.Add(_plain);
        return row;
    }
}
