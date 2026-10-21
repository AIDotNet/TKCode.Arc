using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>The palette's search field: a bare text box that reads as the panel's header (no field frame).</summary>
internal sealed class SettingsCommandSearchBox : AvaloniaTextBox
{
    protected override Type StyleKeyOverride => typeof(SettingsCommandSearchBox);
}

/// <summary>A small key cap showing a key name ("Esc", "⌘") or a key glyph (arrows, Return).</summary>
internal sealed class SettingsCommandKeyCap : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SettingsCommandKeyCap, string?>(nameof(Text));

    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<SettingsCommandKeyCap, Geometry?>(nameof(Glyph));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Geometry? Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    static SettingsCommandKeyCap()
    {
        FocusableProperty.OverrideDefaultValue<SettingsCommandKeyCap>(false);
        IsHitTestVisibleProperty.OverrideDefaultValue<SettingsCommandKeyCap>(false);
    }

    protected override Type StyleKeyOverride => typeof(SettingsCommandKeyCap);

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);
}

/// <summary>A footer hint: key caps and a short label, such as "↵ Toggle". Hints with a low <see cref="PriorityProperty"/> give way first.</summary>
internal sealed class SettingsCommandHintsPanel : Panel
{
    /// <summary>Which hints stay when the footer is narrow: lower values are dropped first.</summary>
    public static readonly AttachedProperty<int> PriorityProperty =
        AvaloniaProperty.RegisterAttached<SettingsCommandHintsPanel, Control, int>("Priority");

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<SettingsCommandHintsPanel, double>(nameof(Gap));

    private readonly List<Control> _dropped = new();
    private readonly List<Control> _byPriority = new();

    public static int GetPriority(Control control) => control.GetValue(PriorityProperty);

    public static void SetPriority(Control control, int value) => control.SetValue(PriorityProperty, value);

    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    static SettingsCommandHintsPanel()
    {
        AffectsMeasure<SettingsCommandHintsPanel>(GapProperty);
        // Dropped hints are arranged out of sight; the clip keeps them there.
        ClipToBoundsProperty.OverrideDefaultValue<SettingsCommandHintsPanel>(true);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _dropped.Clear();
        _byPriority.Clear();
        double width = 0, height = 0;
        var count = 0;
        foreach (var child in Children)
        {
            child.Measure(Size.Infinity);
            if (!child.IsVisible)
            {
                continue;
            }
            width += child.DesiredSize.Width + (count > 0 ? Gap : 0);
            height = Math.Max(height, child.DesiredSize.Height);
            count++;
            _byPriority.Add(child);
        }
        if (width > availableSize.Width)
        {
            _byPriority.Sort((a, b) => GetPriority(a).CompareTo(GetPriority(b)));
            foreach (var child in _byPriority)
            {
                if (width <= availableSize.Width)
                {
                    break;
                }
                _dropped.Add(child);
                count--;
                width -= child.DesiredSize.Width + (count > 0 ? Gap : 0);
            }
        }
        return new Size(Math.Max(0, Math.Min(width, availableSize.Width)), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        foreach (var child in Children)
        {
            if (_dropped.Contains(child) || !child.IsVisible)
            {
                child.Arrange(new Rect(-10000, 0, child.DesiredSize.Width, child.DesiredSize.Height));
                continue;
            }
            var y = (finalSize.Height - child.DesiredSize.Height) / 2;
            child.Arrange(new Rect(x, y, child.DesiredSize.Width, child.DesiredSize.Height));
            x += child.DesiredSize.Width + Gap;
        }
        return finalSize;
    }

    /// <summary>Whether <paramref name="child"/> was dropped for lack of room (tests).</summary>
    internal bool IsDropped(Control child) => _dropped.Contains(child);
}

/// <summary>The title of a group of results ("Appearance").</summary>
internal sealed class SettingsCommandGroupHeader : TextBlock
{
    public SettingsCommandGroupHeader(string key)
    {
        Key = key;
    }

    /// <summary>The group name as written on the items ("" for ungrouped items).</summary>
    public string Key { get; }

    /// <summary>The filter pass that last showed this title.</summary>
    internal int ShownStamp { get; set; }

    protected override Type StyleKeyOverride => typeof(SettingsCommandGroupHeader);
}

/// <summary>
/// The result list: group titles and rows stacked in the order the search decides, with the active-row highlight
/// (its first child) underneath. Rows are created once; filtering only changes the order and which ones show.
/// </summary>
internal sealed class SettingsCommandResults : Panel
{
    private readonly List<Control> _order = new();

    /// <summary>Called after every arrange so the owner can keep the highlight on the active row.</summary>
    internal Action? Arranged { get; set; }

    /// <summary>The headers and rows on show, top to bottom.</summary>
    internal IReadOnlyList<Control> Order => _order;

    internal void SetOrder(List<Control> order)
    {
        _order.Clear();
        _order.AddRange(order);
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var childAvailable = new Size(availableSize.Width, double.PositiveInfinity);
        double width = 0, height = 0;
        foreach (var child in _order)
        {
            child.Measure(childAvailable);
            width = Math.Max(width, child.DesiredSize.Width);
            height += child.DesiredSize.Height;
        }
        foreach (var child in Children)
        {
            if (child is GlidingIndicator)
            {
                child.Measure(availableSize);
            }
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            if (child is GlidingIndicator)
            {
                child.Arrange(new Rect(finalSize));
            }
        }
        var y = 0.0;
        foreach (var child in _order)
        {
            var height = child.DesiredSize.Height;
            child.Arrange(new Rect(0, y, finalSize.Width, height));
            y += height;
        }
        Arranged?.Invoke();
        return finalSize;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SettingsCommandResultsAutomationPeer(this);
}

/// <summary>The result list reads as a list.</summary>
internal sealed class SettingsCommandResultsAutomationPeer : ControlAutomationPeer
{
    public SettingsCommandResultsAutomationPeer(Control owner) : base(owner)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}

/// <summary>The small round arrows of the choice pill. Not focusable: the keyboard stays in the search field.</summary>
internal sealed class SettingsCommandStepButton : AvaloniaButton
{
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<SettingsCommandStepButton, Geometry?>(nameof(Glyph));

    public Geometry? Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    static SettingsCommandStepButton()
    {
        FocusableProperty.OverrideDefaultValue<SettingsCommandStepButton>(false);
        IsTabStopProperty.OverrideDefaultValue<SettingsCommandStepButton>(false);
    }

    protected override Type StyleKeyOverride => typeof(SettingsCommandStepButton);
}

/// <summary>
/// The "‹ value ›" pill of a choice row. The value rolls in the direction of the change; the pill is as wide as the
/// longest option, so it never jumps. At the first or last option the matching arrow dims and does nothing.
/// </summary>
[TemplatePart(PreviousPart, typeof(SettingsCommandStepButton))]
[TemplatePart(NextPart, typeof(SettingsCommandStepButton))]
[TemplatePart(SizerPart, typeof(Panel))]
[PseudoClasses(FirstPseudoClass, LastPseudoClass)]
internal sealed class SettingsCommandChoicePill : TemplatedControl
{
    public const string PreviousPart = "PART_Previous";
    public const string NextPart = "PART_Next";
    public const string SizerPart = "PART_Sizer";
    public const string FirstPseudoClass = ":first";
    public const string LastPseudoClass = ":last";

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<SettingsCommandChoicePill, string?>(nameof(Value));

    public static readonly StyledProperty<int> DirectionProperty =
        AvaloniaProperty.Register<SettingsCommandChoicePill, int>(nameof(Direction), 1);

    public static readonly StyledProperty<string?> PreviousNameProperty =
        AvaloniaProperty.Register<SettingsCommandChoicePill, string?>(nameof(PreviousName));

    public static readonly StyledProperty<string?> NextNameProperty =
        AvaloniaProperty.Register<SettingsCommandChoicePill, string?>(nameof(NextName));

    private readonly SettingsCommandChoice _choice;
    private SettingsCommandStepButton? _previous;
    private SettingsCommandStepButton? _next;
    private Panel? _sizer;

    public SettingsCommandChoicePill(SettingsCommandChoice choice)
    {
        _choice = choice;
    }

    /// <summary>Raised with -1 or +1 when an arrow is clicked.</summary>
    public event Action<int>? Stepped;

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public int Direction
    {
        get => GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    public string? PreviousName
    {
        get => GetValue(PreviousNameProperty);
        set => SetValue(PreviousNameProperty, value);
    }

    public string? NextName
    {
        get => GetValue(NextNameProperty);
        set => SetValue(NextNameProperty, value);
    }

    static SettingsCommandChoicePill()
    {
        FocusableProperty.OverrideDefaultValue<SettingsCommandChoicePill>(false);
    }

    protected override Type StyleKeyOverride => typeof(SettingsCommandChoicePill);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_previous is not null)
        {
            _previous.Click -= OnPreviousClick;
        }
        if (_next is not null)
        {
            _next.Click -= OnNextClick;
        }
        base.OnApplyTemplate(e);
        _previous = e.NameScope.Find<SettingsCommandStepButton>(PreviousPart);
        _next = e.NameScope.Find<SettingsCommandStepButton>(NextPart);
        _sizer = e.NameScope.Find<Panel>(SizerPart);
        if (_previous is not null)
        {
            _previous.Click += OnPreviousClick;
        }
        if (_next is not null)
        {
            _next.Click += OnNextClick;
        }
        RefreshOptions();
    }

    /// <summary>Shows the choice's current value (rolling in <paramref name="direction"/> when it changed).</summary>
    public void Refresh(int direction)
    {
        Direction = direction >= 0 ? 1 : -1;
        Value = _choice.SelectedOption;
        var index = _choice.SelectedIndex;
        var count = _choice.Options.Count;
        PseudoClasses.Set(FirstPseudoClass, index <= 0);
        PseudoClasses.Set(LastPseudoClass, index < 0 || index >= count - 1);
    }

    /// <summary>Rebuilds the invisible copies of every option that give the pill the width of the longest one.</summary>
    public void RefreshOptions()
    {
        if (_sizer is null)
        {
            return;
        }
        var options = _choice.Options;
        var children = _sizer.Children;
        while (children.Count > options.Count)
        {
            children.RemoveAt(children.Count - 1);
        }
        for (var i = 0; i < options.Count; i++)
        {
            if (i < children.Count)
            {
                ((TextBlock)children[i]).Text = options[i];
            }
            else
            {
                children.Add(new TextBlock { Text = options[i] });
            }
        }
    }

    private void OnPreviousClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Stepped?.Invoke(-1);
    }

    private void OnNextClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Stepped?.Invoke(1);
    }
}

/// <summary>
/// One result row: icon tile, header (query matches in the foreground, the rest secondary) and description, and the
/// setting's control: a switch, the choice pill, or key caps and an arrow. Created once per item and reused.
/// </summary>
[TemplatePart(HeaderPart, typeof(TextBlock))]
[PseudoClasses(ActivePseudoClass, IconPseudoClass)]
internal sealed class SettingsCommandRow : TemplatedControl
{
    public const string HeaderPart = "PART_Header";
    public const string ActivePseudoClass = ":active";
    public const string IconPseudoClass = ":icon";

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<SettingsCommandRow, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<SettingsCommandRow, string?>(nameof(Description));

    public static readonly StyledProperty<Control?> AccessoryProperty =
        AvaloniaProperty.Register<SettingsCommandRow, Control?>(nameof(Accessory));

    /// <summary>The color of header text that does not match the query (theme: TextSecondary).</summary>
    public static readonly StyledProperty<IBrush?> DimForegroundProperty =
        AvaloniaProperty.Register<SettingsCommandRow, IBrush?>(nameof(DimForeground));

    /// <summary>The color of the navigation arrow (theme: TextMuted).</summary>
    public static readonly StyledProperty<IBrush?> MutedForegroundProperty =
        AvaloniaProperty.Register<SettingsCommandRow, IBrush?>(nameof(MutedForeground));

    // Spacing of the action accessory, built in code: 3px between the caps of a shortcut, 8px before the arrow.
    private const double CapGap = 3;
    private const double AccessoryGap = 8;
    private const double ArrowSide = 16;

    private readonly SettingsCommand _owner;
    private TextBlock? _header;
    private string[] _tokens = [];
    private bool[]? _mask;
    private bool _pressed;
    private Switch? _switch;
    private SettingsCommandChoicePill? _pill;

    public SettingsCommandRow(SettingsCommand owner, SettingsCommandItem item)
    {
        _owner = owner;
        Item = item;
        BuildAccessory();
    }

    public SettingsCommandItem Item { get; }

    /// <summary>The filter pass that last looked at this row (skips duplicate items).</summary>
    internal int Stamp { get; set; }

    /// <summary>The filter pass that last showed this row.</summary>
    internal int ShownStamp { get; set; }

    /// <summary>The direction the next choice change rolls in (set by the palette before it changes the value).</summary>
    internal int PendingDirection { get; set; } = 1;

    /// <summary>The group title right above this row when it is the first of its group, else null.</summary>
    internal SettingsCommandGroupHeader? LeadingHeader { get; set; }

    /// <summary>Whether the row is the active one (highlighted, acted on by Enter).</summary>
    internal bool IsActive => PseudoClasses.Contains(ActivePseudoClass);

    internal Switch? Switch => _switch;

    internal SettingsCommandChoicePill? Pill => _pill;

    /// <summary>The header with its match runs (tests).</summary>
    internal TextBlock? HeaderText => _header;

    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public Control? Accessory
    {
        get => GetValue(AccessoryProperty);
        set => SetValue(AccessoryProperty, value);
    }

    public IBrush? DimForeground
    {
        get => GetValue(DimForegroundProperty);
        set => SetValue(DimForegroundProperty, value);
    }

    public IBrush? MutedForeground
    {
        get => GetValue(MutedForegroundProperty);
        set => SetValue(MutedForegroundProperty, value);
    }

    static SettingsCommandRow()
    {
        FocusableProperty.OverrideDefaultValue<SettingsCommandRow>(false);
    }

    protected override Type StyleKeyOverride => typeof(SettingsCommandRow);

    protected override AutomationPeer OnCreateAutomationPeer() => new SettingsCommandRowAutomationPeer(this);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _header = e.NameScope.Find<TextBlock>(HeaderPart);
        RebuildHeader();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DimForegroundProperty)
        {
            RebuildHeader();
        }
        else if (change.Property == IconProperty)
        {
            PseudoClasses.Set(IconPseudoClass, Icon is not null);
        }
    }

    internal void SetActive(bool active) => PseudoClasses.Set(ActivePseudoClass, active);

    /// <summary>Highlights the words of the query in the header.</summary>
    internal void SetTokens(string[] tokens)
    {
        if (_tokens.AsSpan().SequenceEqual(tokens))
        {
            return;
        }
        _tokens = tokens;
        RebuildHeader();
    }

    /// <summary>Copies the item's values into the row; <paramref name="property"/> is what changed (null: everything).</summary>
    internal void Refresh(AvaloniaProperty? property)
    {
        var all = property is null;
        if (all || property == SettingsCommandItem.IconProperty)
        {
            Icon = Item.Icon;
        }
        if (all || property == SettingsCommandItem.DescriptionProperty)
        {
            Description = Item.Description;
        }
        if (all || property == SettingsCommandItem.IsEnabledProperty)
        {
            IsEnabled = Item.IsEnabled;
        }
        if (all || property == SettingsCommandItem.HeaderProperty)
        {
            RebuildHeader();
        }
        switch (Item)
        {
            case SettingsCommandToggle toggle:
                _switch?.SetCurrentValue(ToggleButton.IsCheckedProperty, toggle.IsChecked);
                break;
            case SettingsCommandChoice:
                if (all || property == SettingsCommandChoice.OptionsProperty)
                {
                    _pill?.RefreshOptions();
                }
                _pill?.Refresh(PendingDirection);
                PendingDirection = 1;
                break;
            case SettingsCommandAction:
                if (all || property == SettingsCommandAction.ShortcutProperty || property == SettingsCommandAction.IsNavigationProperty)
                {
                    Accessory = BuildActionAccessory();
                }
                break;
        }
        ApplyLabels();
    }

    /// <summary>Applies the palette's labels (state words, arrow names) and the accessible name.</summary>
    internal void ApplyLabels()
    {
        var labels = _owner.Labels;
        if (_pill is not null)
        {
            _pill.PreviousName = labels.PreviousOption;
            _pill.NextName = labels.NextOption;
        }
        AutomationProperties.SetName(this, Item.Header);
        AutomationProperties.SetHelpText(this, StateText(labels));
    }

    /// <summary>The state read with the row: "on" / "off" for a toggle, the value for a choice.</summary>
    internal string? StateText(SettingsCommandLabels labels) => Item switch
    {
        SettingsCommandToggle toggle => toggle.IsChecked ? labels.On : labels.Off,
        SettingsCommandChoice choice => choice.SelectedOption,
        _ => Item.Description
    };

    private void BuildAccessory()
    {
        switch (Item)
        {
            case SettingsCommandToggle:
                // Not focusable and not hit-testable: a click anywhere on the row (the switch included) flips it.
                _switch = new Switch
                {
                    Focusable = false,
                    IsTabStop = false,
                    IsHitTestVisible = false
                };
                AutomationProperties.SetAccessibilityView(_switch, AccessibilityView.Raw);
                Accessory = _switch;
                break;
            case SettingsCommandChoice choice:
                _pill = new SettingsCommandChoicePill(choice);
                _pill.Stepped += delta => _owner.StepChoice(this, choice, delta, wrap: false);
                Accessory = _pill;
                break;
        }
        Refresh(null);
    }

    private Control? BuildActionAccessory()
    {
        if (Item is not SettingsCommandAction action)
        {
            return null;
        }
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Spacing = AccessoryGap };
        if (ShortcutFormatter.TryParse(action.Shortcut, ShortcutFormatter.CurrentPlatform, out var gesture))
        {
            var caps = new StackPanel { Orientation = Orientation.Horizontal, Spacing = CapGap };
            foreach (var cap in ShortcutFormatter.Tokens(gesture, ShortcutFormatter.CurrentPlatform))
            {
                caps.Children.Add(new SettingsCommandKeyCap { Text = cap.Label });
            }
            panel.Children.Add(caps);
        }
        else if (!string.IsNullOrWhiteSpace(action.Shortcut))
        {
            panel.Children.Add(new SettingsCommandKeyCap { Text = action.Shortcut });
        }
        if (action.IsNavigation)
        {
            var arrow = new ArcGlyph { Data = ArcIcons.ChevronRight, Size = ArrowSide, VerticalAlignment = VerticalAlignment.Center };
            arrow.Bind(ArcGlyph.ForegroundProperty, this.GetObservable(MutedForegroundProperty));
            panel.Children.Add(arrow);
        }
        return panel.Children.Count == 0 ? null : panel;
    }

    private void RebuildHeader()
    {
        if (_header is null)
        {
            return;
        }
        var text = Item.Header ?? string.Empty;
        var inlines = _header.Inlines ??= new InlineCollection();
        inlines.Clear();
        if (_tokens.Length == 0)
        {
            inlines.Add(new Run(text));
            return;
        }
        var dim = DimForeground;
        if (!SettingsCommandMatcher.MarkMatches(text, _tokens, ref _mask))
        {
            inlines.Add(new Run(text) { Foreground = dim });
            return;
        }
        var mask = _mask!;
        var start = 0;
        while (start < text.Length)
        {
            var matched = mask[start];
            var end = start + 1;
            while (end < text.Length && mask[end] == matched)
            {
                end++;
            }
            var run = new Run(text.Substring(start, end - start));
            if (!matched)
            {
                run.Foreground = dim;
            }
            inlines.Add(run);
            start = end;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || !IsEffectivelyEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pressed = true;
        e.Pointer.Capture(this);
        e.Handled = true;
        _owner.OnRowPressed(this);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        _pressed = false;
        e.Pointer.Capture(null);
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)) && IsEffectivelyEnabled)
        {
            e.Handled = true;
            _owner.InvokeRow(this);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
    }
}

/// <summary>A result row reads as a list item named by its header, with its state as help text.</summary>
internal sealed class SettingsCommandRowAutomationPeer : ControlAutomationPeer
{
    public SettingsCommandRowAutomationPeer(SettingsCommandRow owner) : base(owner)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

    protected override string? GetNameCore() => ((SettingsCommandRow)Owner).Item.Header;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}
