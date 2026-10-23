using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The color family of an explainer's status icon or mark.</summary>
internal enum ExplainerTone
{
    /// <summary>Secondary text color.</summary>
    Neutral,

    /// <summary>The explainer's accent (chart series 1 by default).</summary>
    Accent,

    /// <summary>Success (done, passed, cache hit).</summary>
    Success,

    /// <summary>Warning (retry, partial, at capacity).</summary>
    Warning,

    /// <summary>Danger (failed, stopped, expired).</summary>
    Danger,

    /// <summary>Muted text color.</summary>
    Muted
}

/// <summary>
/// An explainer's status line: a 16px icon and a label in secondary text, swapped together with one short crossfade when
/// either changes (never a pill). While <see cref="IsBusy"/> the icon is a spinner and the label carries a calm shimmer.
/// Its accessible name is the label. Status never relies on color alone: each state pairs an icon with words.
/// </summary>
internal sealed class ExplainerStatus : Control
{
    /// <summary>Defines the <see cref="Text"/> property.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ExplainerStatus, string?>(nameof(Text));

    /// <summary>Defines the <see cref="Icon"/> property.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<ExplainerStatus, Geometry?>(nameof(Icon));

    /// <summary>Defines the <see cref="Tone"/> property.</summary>
    public static readonly StyledProperty<ExplainerTone> ToneProperty =
        AvaloniaProperty.Register<ExplainerStatus, ExplainerTone>(nameof(Tone));

    /// <summary>Defines the <see cref="IsBusy"/> property.</summary>
    public static readonly StyledProperty<bool> IsBusyProperty =
        AvaloniaProperty.Register<ExplainerStatus, bool>(nameof(IsBusy));

    /// <summary>Defines the <see cref="ShowsSpinner"/> property.</summary>
    public static readonly StyledProperty<bool> ShowsSpinnerProperty =
        AvaloniaProperty.Register<ExplainerStatus, bool>(nameof(ShowsSpinner), true);

    /// <summary>Defines the <see cref="IconSize"/> property.</summary>
    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<ExplainerStatus, double>(nameof(IconSize), 16);

    private readonly ArcContentSwap _swap;
    private bool _dirty = true;

    public ExplainerStatus()
    {
        _swap = new ArcContentSwap { Mode = ContentSwapMode.Crossfade, VerticalAlignment = VerticalAlignment.Center };
        VisualChildren.Add(_swap);
        LogicalChildren.Add(_swap);
    }

    /// <summary>The label.</summary>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>The icon (a 24×24 stroked glyph); ignored while <see cref="IsBusy"/>.</summary>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>The icon's color family.</summary>
    public ExplainerTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>Work in progress: a spinner instead of the icon and a shimmer on the label.</summary>
    public bool IsBusy { get => GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }

    /// <summary>While busy, show a spinner in place of the icon (default true); false shows only the shimmering label.</summary>
    public bool ShowsSpinner { get => GetValue(ShowsSpinnerProperty); set => SetValue(ShowsSpinnerProperty, value); }

    /// <summary>The icon size in px (default 16).</summary>
    public double IconSize { get => GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }

    /// <summary>The token for a tone's color.</summary>
    public static ArcDesignTokenKind TokenOf(ExplainerTone tone) => tone switch
    {
        ExplainerTone.Accent => ArcDesignTokenKind.Series1,
        ExplainerTone.Success => ArcDesignTokenKind.Success,
        ExplainerTone.Warning => ArcDesignTokenKind.Warning,
        ExplainerTone.Danger => ArcDesignTokenKind.Danger,
        ExplainerTone.Muted => ArcDesignTokenKind.TextMuted,
        _ => ArcDesignTokenKind.TextSecondary
    };

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == IconProperty || change.Property == ToneProperty ||
            change.Property == IsBusyProperty || change.Property == IconSizeProperty || change.Property == ShowsSpinnerProperty)
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
        Control? icon = null;
        if (IsBusy && !ShowsSpinner)
        {
            // A busy label without an icon: only the shimmer says it is working.
        }
        else if (IsBusy)
        {
            var spinner = new ArcSpinner { Width = IconSize, Height = IconSize, VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(spinner, ArcSpinner.ForegroundProperty, TokenOf(Tone));
            icon = spinner;
        }
        else if (Icon is { } data)
        {
            var glyph = new ArcGlyph { Data = data, Size = IconSize, VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, TokenOf(Tone));
            icon = glyph;
        }
        if (icon is not null)
        {
            row.Children.Add(icon);
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
