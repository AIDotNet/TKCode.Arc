using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The card's status line: an optional 15 px icon and a label in secondary text that wraps (narrow cards reserve two
/// lines for it), swapped with one short crossfade (the old line fades out in 100 ms while the new one fades in and rises
/// 2 px). While busy it shows a spinner and a shimmering single line instead. Its accessible name is the label.
/// </summary>
/// <remarks>
/// Shared-code request: <c>ExplainerStatus</c> draws one line through <c>TextShimmer</c>, which cannot wrap; a wrapping
/// mode there would make this private copy unnecessary.
/// </remarks>
internal sealed class SpeculativeDecodingStatusLine : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SpeculativeDecodingStatusLine, string?>(nameof(Text));

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<SpeculativeDecodingStatusLine, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<ExplainerTone> ToneProperty =
        AvaloniaProperty.Register<SpeculativeDecodingStatusLine, ExplainerTone>(nameof(Tone));

    public static readonly StyledProperty<bool> IsBusyProperty =
        AvaloniaProperty.Register<SpeculativeDecodingStatusLine, bool>(nameof(IsBusy));

    private readonly ArcContentSwap _swap;
    private bool _dirty = true;

    public SpeculativeDecodingStatusLine()
    {
        _swap = new ArcContentSwap
        {
            Mode = ContentSwapMode.Crossfade,
            EnterOffset = 2,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        VisualChildren.Add(_swap);
        LogicalChildren.Add(_swap);
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public ExplainerTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    public bool IsBusy { get => GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }

    /// <summary>Sets the text, icon, tone and busy state together (one swap).</summary>
    public void Show(string? text, Geometry? icon, ExplainerTone tone, bool busy)
    {
        if (Text == text && ReferenceEquals(Icon, icon) && Tone == tone && IsBusy == busy)
        {
            return;
        }
        SetCurrentValue(TextProperty, text);
        SetCurrentValue(IconProperty, icon);
        SetCurrentValue(ToneProperty, tone);
        SetCurrentValue(IsBusyProperty, busy);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == IconProperty || change.Property == ToneProperty || change.Property == IsBusyProperty)
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
        var row = new DockPanel { LastChildFill = true, VerticalAlignment = VerticalAlignment.Center };
        Control? icon = null;
        if (IsBusy)
        {
            var spinner = new ArcSpinner { Width = 15, Height = 15 };
            ArcTokens.Bind(spinner, ArcSpinner.ForegroundProperty, ExplainerStatus.TokenOf(Tone));
            icon = spinner;
        }
        else if (Icon is { } data)
        {
            var glyph = new ArcGlyph { Data = data, Size = 15, StrokeThickness = 2 };
            ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, ExplainerStatus.TokenOf(Tone));
            icon = glyph;
        }
        if (icon is not null)
        {
            // The icon sits on the first line of a wrapped label.
            icon.VerticalAlignment = VerticalAlignment.Top;
            icon.Margin = new Thickness(0, 2.3, 6, 0);
            DockPanel.SetDock(icon, Dock.Left);
            row.Children.Add(icon);
        }
        if (IsBusy)
        {
            var shimmer = new TextShimmer { Text = Text, IsActive = true, FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(shimmer, TextShimmer.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            row.Children.Add(shimmer);
        }
        else
        {
            var label = new TextBlock { Text = Text, FontSize = 14, LineHeight = 19.6, TextWrapping = TextWrapping.Wrap };
            ArcTokens.Bind(label, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            row.Children.Add(label);
        }
        return row;
    }
}

/// <summary>
/// One statistic of the card: a dash before the first round settles, then rolling digits with a suffix in secondary text
/// over a small label. The digits are decorative; the accessible name speaks the label and the value.
/// </summary>
[PseudoClasses(ValuePseudoClass)]
internal sealed class SpeculativeDecodingStat : TemplatedControl
{
    public const string ValuePseudoClass = ":value";

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<SpeculativeDecodingStat, double>(nameof(Value));

    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<SpeculativeDecodingStat, int>(nameof(Decimals));

    public static readonly StyledProperty<string?> SuffixProperty =
        AvaloniaProperty.Register<SpeculativeDecodingStat, string?>(nameof(Suffix));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<SpeculativeDecodingStat, string?>(nameof(Label));

    public static readonly StyledProperty<bool> HasValueProperty =
        AvaloniaProperty.Register<SpeculativeDecodingStat, bool>(nameof(HasValue));

    private string? _spoken;

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public int Decimals { get => GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }

    public string? Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public bool HasValue { get => GetValue(HasValueProperty); set => SetValue(HasValueProperty, value); }

    /// <summary>The spoken value (for example "2.6 times"; "not yet" before the first round).</summary>
    public string? Spoken
    {
        get => _spoken;
        set
        {
            _spoken = value;
            UpdateName();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HasValueProperty)
        {
            PseudoClasses.Set(ValuePseudoClass, HasValue);
        }
        else if (change.Property == LabelProperty)
        {
            UpdateName();
        }
    }

    private void UpdateName() => AutomationProperties.SetName(this, $"{Label}: {_spoken}");
}

/// <summary>
/// Text for assistive technology only (a line's written text, a round's summary): no size, no input, read as text.
/// </summary>
internal sealed class SpeculativeDecodingSpeech : Control
{
    private string? _text;

    public SpeculativeDecodingSpeech()
    {
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <summary>The text read out; null or empty hides it.</summary>
    public string? Text
    {
        get => _text;
        set
        {
            if (_text == value)
            {
                return;
            }
            _text = value;
            AutomationProperties.SetName(this, value ?? string.Empty);
            AutomationProperties.SetAccessibilityView(this, string.IsNullOrEmpty(value) ? AccessibilityView.Raw : AccessibilityView.Content);
        }
    }

    protected override Size MeasureOverride(Size availableSize) => default;

    protected override AutomationPeer OnCreateAutomationPeer() => new SpeechPeer(this);

    private sealed class SpeechPeer : ControlAutomationPeer
    {
        public SpeechPeer(Control owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override bool IsContentElementCore() => !string.IsNullOrEmpty(GetNameCore());
    }
}
