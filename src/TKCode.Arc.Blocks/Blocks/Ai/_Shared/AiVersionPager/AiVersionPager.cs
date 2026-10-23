using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="AiVersionPager.IndexChangedEvent"/>: the version the person moved to.</summary>
internal sealed class AiVersionPagerEventArgs : RoutedEventArgs
{
    public AiVersionPagerEventArgs(RoutedEvent routedEvent, object source, int index, int count) : base(routedEvent, source)
    {
        Index = index;
        Count = count;
    }

    /// <summary>The version shown now, from 0.</summary>
    public int Index { get; }

    /// <summary>How many versions there are.</summary>
    public int Count { get; }
}

/// <summary>
/// Switches between the versions of an AI reply (Arc chat and answer): "‹ 2 / 2 ›". Previous and next are 26 × 28 buttons,
/// radius 8, with 15px chevrons in secondary text, disabled at the ends (opacity 0.35, out of the tab order); between them the
/// count in 14px secondary text with tabular figures, its numbers rolling on <see cref="ArcSpring.Snappy"/> 0.6em up or down
/// (<see cref="RollingDigits"/>). It is hidden while there is one version or none. The group is named by
/// <see cref="GroupName"/> ("Version 2 of 2"), which follows the index; the count itself is not read (the name says it).
/// <see cref="IndexChanged"/> reports the person's moves; <see cref="Index"/> is two-way.
/// </summary>
[TemplatePart(PreviousPart, typeof(AvaloniaButton))]
[TemplatePart(NextPart, typeof(AvaloniaButton))]
[TemplatePart(NumberPart, typeof(RollingDigits))]
[TemplatePart(TotalPart, typeof(RollingDigits))]
[TemplatePart(SeparatorPart, typeof(TextBlock))]
[TemplatePart(CountTextPart, typeof(TextBlock))]
[PseudoClasses(TextCountPseudoClass)]
internal class AiVersionPager : TemplatedControl
{
    public const string PreviousPart = "PART_Previous";
    public const string NextPart = "PART_Next";
    public const string NumberPart = "PART_Number";
    public const string TotalPart = "PART_Total";
    public const string SeparatorPart = "PART_Separator";
    public const string CountTextPart = "PART_CountText";

    /// <summary>Set when <see cref="CountText"/> is not "number, separator, number": the count shows as plain text.</summary>
    public const string TextCountPseudoClass = ":text-count";

    /// <summary>The separator of the rolling count when <see cref="CountText"/> is not set.</summary>
    internal const string DefaultSeparator = " / ";

    #region Properties

    /// <summary>How many versions there are; one or none hides the pager.</summary>
    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<AiVersionPager, int>(nameof(Count));

    /// <summary>The version shown, from 0 (two-way); kept within the versions.</summary>
    public static readonly StyledProperty<int> IndexProperty =
        AvaloniaProperty.Register<AiVersionPager, int>(nameof(Index), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay,
            coerce: CoerceIndex);

    /// <summary>Names the group from the shown version and the count, both from 1 ("Version 2 of 2").</summary>
    public static readonly StyledProperty<Func<int, int, string>?> GroupNameProperty =
        AvaloniaProperty.Register<AiVersionPager, Func<int, int, string>?>(nameof(GroupName));

    /// <summary>
    /// The visible count from the shown version and the count ("2 / 2"). When it reads as the number, a separator and the count,
    /// the numbers roll; otherwise it shows as plain text. Null rolls with " / ".
    /// </summary>
    public static readonly StyledProperty<Func<int, int, string>?> CountTextProperty =
        AvaloniaProperty.Register<AiVersionPager, Func<int, int, string>?>(nameof(CountText));

    /// <summary>The previous button's name and tooltip ("Previous version").</summary>
    public static readonly StyledProperty<string?> PreviousTextProperty =
        AvaloniaProperty.Register<AiVersionPager, string?>(nameof(PreviousText));

    /// <summary>The next button's name and tooltip ("Next version").</summary>
    public static readonly StyledProperty<string?> NextTextProperty =
        AvaloniaProperty.Register<AiVersionPager, string?>(nameof(NextText));

    /// <summary>Raised (bubbling) when the person moves to another version.</summary>
    public static readonly RoutedEvent<AiVersionPagerEventArgs> IndexChangedEvent =
        RoutedEvent.Register<AiVersionPager, AiVersionPagerEventArgs>(nameof(IndexChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="CountProperty"/>
    public int Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }

    /// <inheritdoc cref="IndexProperty"/>
    public int Index { get => GetValue(IndexProperty); set => SetValue(IndexProperty, value); }

    /// <inheritdoc cref="GroupNameProperty"/>
    public Func<int, int, string>? GroupName { get => GetValue(GroupNameProperty); set => SetValue(GroupNameProperty, value); }

    /// <inheritdoc cref="CountTextProperty"/>
    public Func<int, int, string>? CountText { get => GetValue(CountTextProperty); set => SetValue(CountTextProperty, value); }

    /// <inheritdoc cref="PreviousTextProperty"/>
    public string? PreviousText { get => GetValue(PreviousTextProperty); set => SetValue(PreviousTextProperty, value); }

    /// <inheritdoc cref="NextTextProperty"/>
    public string? NextText { get => GetValue(NextTextProperty); set => SetValue(NextTextProperty, value); }

    /// <inheritdoc cref="IndexChangedEvent"/>
    public event EventHandler<AiVersionPagerEventArgs>? IndexChanged
    {
        add => AddHandler(IndexChangedEvent, value);
        remove => RemoveHandler(IndexChangedEvent, value);
    }

    #endregion

    private AvaloniaButton? _previous;
    private AvaloniaButton? _next;
    private RollingDigits? _number;
    private RollingDigits? _total;
    private TextBlock? _separator;
    private TextBlock? _countText;

    static AiVersionPager()
    {
        FocusableProperty.OverrideDefaultValue<AiVersionPager>(false);
    }

    public AiVersionPager()
    {
        IsVisible = false;
    }

    /// <summary>The previous button.</summary>
    internal AvaloniaButton? PreviousButton => _previous;

    /// <summary>The next button.</summary>
    internal AvaloniaButton? NextButton => _next;

    /// <summary>The count as the person sees it ("2 / 2").</summary>
    internal string ShownCount => CountString(Index + 1, Math.Max(Count, 0));

    private static int CoerceIndex(AvaloniaObject owner, int value)
    {
        var count = ((AiVersionPager)owner).Count;
        return count <= 0 ? 0 : Math.Clamp(value, 0, count - 1);
    }

    /// <summary>
    /// The separator of <paramref name="format"/> when it reads as the number, the separator and the count (checked on two
    /// sample values), or null when it does not and the count must show as text.
    /// </summary>
    internal static string? SeparatorOf(Func<int, int, string>? format)
    {
        if (format is null)
        {
            return DefaultSeparator;
        }
        var sample = format(7, 9);
        if (sample.Length < 3 || !sample.StartsWith('7') || !sample.EndsWith('9'))
        {
            return null;
        }
        var separator = sample[1..^1];
        foreach (var c in separator)
        {
            if (char.IsDigit(c))
            {
                return null;
            }
        }
        return format(12, 34) == "12" + separator + "34" ? separator : null;
    }

    private string CountString(int number, int count) =>
        CountText?.Invoke(number, count) ?? number.ToString(System.Globalization.CultureInfo.InvariantCulture) + DefaultSeparator
        + count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_previous is not null)
        {
            _previous.Click -= OnPreviousClick;
        }
        if (_next is not null)
        {
            _next.Click -= OnNextClick;
        }
        _previous = e.NameScope.Find<AvaloniaButton>(PreviousPart);
        _next = e.NameScope.Find<AvaloniaButton>(NextPart);
        _number = e.NameScope.Find<RollingDigits>(NumberPart);
        _total = e.NameScope.Find<RollingDigits>(TotalPart);
        _separator = e.NameScope.Find<TextBlock>(SeparatorPart);
        _countText = e.NameScope.Find<TextBlock>(CountTextPart);
        if (_previous is not null)
        {
            _previous.Click += OnPreviousClick;
        }
        if (_next is not null)
        {
            _next.Click += OnNextClick;
        }
        Update();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CountProperty)
        {
            CoerceValue(IndexProperty);
            Update();
        }
        else if (change.Property == IndexProperty || change.Property == GroupNameProperty || change.Property == CountTextProperty
                 || change.Property == PreviousTextProperty || change.Property == NextTextProperty)
        {
            Update();
        }
    }

    private void Update()
    {
        var count = Math.Max(Count, 0);
        var index = Index;
        IsVisible = count > 1;
        var number = index + 1;
        AutomationProperties.SetName(this, GroupName?.Invoke(number, count) ?? string.Empty);
        if (_previous is not null)
        {
            _previous.IsEnabled = index > 0;
            SetButtonText(_previous, PreviousText);
        }
        if (_next is not null)
        {
            _next.IsEnabled = index < count - 1;
            SetButtonText(_next, NextText);
        }
        var separator = SeparatorOf(CountText);
        PseudoClasses.Set(TextCountPseudoClass, separator is null);
        if (_number is not null)
        {
            _number.Value = number;
        }
        if (_total is not null)
        {
            _total.Value = count;
        }
        if (_separator is not null)
        {
            _separator.Text = separator ?? DefaultSeparator;
        }
        if (_countText is not null)
        {
            _countText.Text = separator is null ? CountString(number, count) : null;
        }
    }

    private static void SetButtonText(AvaloniaButton button, string? text)
    {
        AutomationProperties.SetName(button, text ?? string.Empty);
        Tooltip.SetTip(button, string.IsNullOrEmpty(text) ? null : text);
    }

    private void OnPreviousClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Move(-1, _previous, _next);
    }

    private void OnNextClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Move(1, _next, _previous);
    }

    /// <summary>Moves one version; a button that just became disabled hands its focus to the other one, ring included.</summary>
    private void Move(int step, AvaloniaButton? pressed, AvaloniaButton? other)
    {
        var target = Index + step;
        if (target < 0 || target >= Count)
        {
            return;
        }
        var keyboard = pressed is not null && pressed.Classes.Contains(":focus-visible");
        var hadFocus = pressed?.IsFocused == true;
        SetCurrentValue(IndexProperty, target);
        if (hadFocus && pressed is { IsEffectivelyEnabled: false } && other is { IsEffectivelyEnabled: true })
        {
            other.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
        }
        RaiseEvent(new AiVersionPagerEventArgs(IndexChangedEvent, this, Index, Count));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AiVersionPagerAutomationPeer(this);

    /// <summary>A named group ("Version 2 of 2") holding the two buttons.</summary>
    private sealed class AiVersionPagerAutomationPeer(AiVersionPager owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// The previous or next button of an <see cref="AiVersionPager"/>: 26 × 28, radius 8, a 15px chevron in secondary text
/// (Foreground on hover), 0.35 opacity and out of the tab order at an end.
/// </summary>
[PseudoClasses(HoverPseudoClass)]
internal sealed class AiVersionPagerButton : AvaloniaButton
{
    /// <summary>Mouse or pen over the button.</summary>
    public const string HoverPseudoClass = ":hover";

    /// <summary>The chevron (ChevronLeft or ChevronRight).</summary>
    public static readonly StyledProperty<Avalonia.Media.Geometry?> GlyphProperty =
        AvaloniaProperty.Register<AiVersionPagerButton, Avalonia.Media.Geometry?>(nameof(Glyph));

    /// <inheritdoc cref="GlyphProperty"/>
    public Avalonia.Media.Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    protected override Type StyleKeyOverride => typeof(AiVersionPagerButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEffectivelyEnabledProperty && !change.GetNewValue<bool>())
        {
            // A button disabled under the pointer (it reached an end) gets no PointerExited.
            PseudoClasses.Set(HoverPseudoClass, false);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc's hover is (hover: hover) and (pointer: fine): touch never leaves a hover look behind.
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch && IsEffectivelyEnabled);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AiVersionPagerButtonAutomationPeer(this);

    /// <summary>A button named by the pager's wording; the chevron is not read.</summary>
    private sealed class AiVersionPagerButtonAutomationPeer(AiVersionPagerButton owner) : ButtonAutomationPeer(owner)
    {
        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}
