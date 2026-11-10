using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// One segment of a <see cref="BookingPill"/>: a small label over the value ("Where" over "Lisbon"). It is a button that
/// opens its picker; while open it sits on the pill's raised chip.
/// </summary>
[PseudoClasses(OpenPseudoClass, EmptyPseudoClass)]
[TemplatePart(RootPart, typeof(Panel))]
internal sealed class BookingPillSegmentButton : AvaloniaButton
{
    internal const string OpenPseudoClass = ":open";
    internal const string EmptyPseudoClass = ":empty";
    internal const string RootPart = "PART_Root";

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<BookingPillSegmentButton, string?>(nameof(Label));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<BookingPillSegmentButton, string?>(nameof(Value));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<BookingPillSegmentButton, bool>(nameof(IsOpen));

    public static readonly StyledProperty<bool> IsEmptyProperty =
        AvaloniaProperty.Register<BookingPillSegmentButton, bool>(nameof(IsEmpty), true);

    private LazyFocusRing? _focusRing;

    static BookingPillSegmentButton()
    {
        ClipToBoundsProperty.OverrideDefaultValue<BookingPillSegmentButton>(false);
    }

    public BookingPillSegmentButton()
    {
        PseudoClasses.Set(EmptyPseudoClass, true);
    }

    public BookingPillPart Part { get; set; }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public bool IsEmpty
    {
        get => GetValue(IsEmptyProperty);
        set => SetValue(IsEmptyProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, BookingPillTokens.Key(BookingPillTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusPill));
        _focusRing.Attach(e.NameScope.Find<Panel>(RootPart));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            PseudoClasses.Set(OpenPseudoClass, change.GetNewValue<bool>());
            UpdateName();
        }
        else if (change.Property == IsEmptyProperty)
        {
            PseudoClasses.Set(EmptyPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == LabelProperty || change.Property == ValueProperty)
        {
            UpdateName();
        }
    }

    /// <summary>Read as "Where, Lisbon".</summary>
    private void UpdateName() => AutomationProperties.SetName(this, $"{Label}, {Value}");

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }
}

/// <summary>
/// The round search button at the end of a <see cref="BookingPill"/>. While a segment is open it grows on a width spring
/// to show its word next to the magnifier.
/// </summary>
[PseudoClasses(ExpandedPseudoClass)]
[TemplatePart(RootPart, typeof(Panel))]
internal sealed class BookingPillSearchButton : AvaloniaButton
{
    internal const string ExpandedPseudoClass = ":expanded";
    internal const string RootPart = "PART_Root";

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<BookingPillSearchButton, string?>(nameof(Text));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<BookingPillSearchButton, bool>(nameof(IsExpanded));

    private LazyFocusRing? _focusRing;

    static BookingPillSearchButton()
    {
        ClipToBoundsProperty.OverrideDefaultValue<BookingPillSearchButton>(false);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, BookingPillTokens.Key(BookingPillTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusPill));
        _focusRing.Attach(e.NameScope.Find<Panel>(RootPart));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
        {
            PseudoClasses.Set(ExpandedPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>());
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }
}
