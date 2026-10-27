using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>
/// The invite field: a 36px pill (radius 18) holding the address chips and a bare text input that takes the rest of the
/// line (wrapping to a new line when less than its minimum is left). The field only reports what the person does
/// (<see cref="Committed"/>, <see cref="EnterOnEmpty"/>, <see cref="BackspaceOnEmpty"/>, <see cref="ChipRemoveRequested"/>);
/// the block judges the text and tells the field which chips to add or remove. Chips pop in (scale 0.9 → 1 on the Quick
/// spring with a 100ms fade and blur) and the others slide to their new places on the Smooth spring.
/// </summary>
[TemplatePart(ChipsPart, typeof(TeamMembersChipPanel))]
[TemplatePart(InputPart, typeof(TeamMembersEmailInput))]
[PseudoClasses(InvalidPseudoClass, BusyPseudoClass)]
internal sealed class TeamMembersEmailField : TemplatedControl
{
    public const string ChipsPart = "PART_Chips";
    public const string InputPart = "PART_Input";
    public const string InvalidPseudoClass = ":invalid";
    public const string BusyPseudoClass = ":busy";

    /// <summary>The chip's pop: ω ≈ 19.5, critically damped (measured), the library's Quick spring.</summary>
    internal static readonly MotionSpec ChipPop = MotionSpec.Spring(ArcSpring.Quick);

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<TeamMembersEmailField, string?>(nameof(Placeholder));

    public static readonly StyledProperty<double> ChipGapProperty =
        AvaloniaProperty.Register<TeamMembersEmailField, double>(nameof(ChipGap), 4);

    public static readonly StyledProperty<double> InputMinWidthProperty =
        AvaloniaProperty.Register<TeamMembersEmailField, double>(nameof(InputMinWidth), 72);

    private readonly List<TeamMembersChip> _chips = new();
    private TeamMembersChipPanel? _panel;
    private TeamMembersEmailInput? _input;
    private bool _isInvalid;
    private bool _isBusy;
    private Func<string, string>? _removeName;

    static TeamMembersEmailField()
    {
        FocusableProperty.OverrideDefaultValue<TeamMembersEmailField>(false);
    }

    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    public double ChipGap { get => GetValue(ChipGapProperty); set => SetValue(ChipGapProperty, value); }

    public double InputMinWidth { get => GetValue(InputMinWidthProperty); set => SetValue(InputMinWidthProperty, value); }

    /// <summary>The chips' addresses, in order (leaving chips excluded).</summary>
    public IReadOnlyList<string> Chips => _chips.Select(c => c.Text ?? string.Empty).ToList();

    internal IReadOnlyList<TeamMembersChip> ChipControls => _chips;

    /// <summary>The text input (null before the template is applied).</summary>
    public TeamMembersEmailInput? Input => _input;

    /// <summary>The text being typed.</summary>
    public string Draft
    {
        get => _input?.Text ?? _pendingDraft;
        set
        {
            _pendingDraft = value;
            if (_input is not null)
            {
                _input.Text = value;
                _input.CaretIndex = value.Length;
            }
        }
    }

    private string _pendingDraft = string.Empty;

    /// <summary>Shows the field's error look (Danger border).</summary>
    public bool IsInvalid
    {
        get => _isInvalid;
        set
        {
            _isInvalid = value;
            PseudoClasses.Set(InvalidPseudoClass, value);
        }
    }

    /// <summary>While sending: the input is read only and the chips cannot be removed.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            PseudoClasses.Set(BusyPseudoClass, value);
            if (_input is not null)
            {
                _input.IsReadOnly = value;
            }
            foreach (var chip in _chips)
            {
                chip.IsRemoveEnabled = !value;
            }
        }
    }

    /// <summary>The accessible name of a chip's remove button.</summary>
    public Func<string, string>? RemoveName
    {
        get => _removeName;
        set
        {
            _removeName = value;
            foreach (var chip in _chips)
            {
                chip.RemoveName = value?.Invoke(chip.Text ?? string.Empty);
            }
        }
    }

    /// <summary>Text to judge: a separator was typed (the text includes everything typed so far) or Enter was pressed on a draft.</summary>
    public event Action<string>? Committed;

    /// <summary>Text pasted that contains separators (already taken out of the normal paste).</summary>
    public event Action<string>? Pasted;

    /// <summary>Enter on an empty input.</summary>
    public event Action? EnterOnEmpty;

    /// <summary>Backspace on an empty input.</summary>
    public event Action? BackspaceOnEmpty;

    /// <summary>A chip's remove button was pressed; the argument is its address.</summary>
    public event Action<string>? ChipRemoveRequested;

    /// <summary>The draft text changed by typing.</summary>
    public event Action<string>? DraftChanged;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_input is not null)
        {
            _input.Owner = null;
            _input.TextChanged -= OnInputTextChanged;
        }
        _panel?.Children.Clear();
        _panel = e.NameScope.Find<TeamMembersChipPanel>(ChipsPart);
        _input = e.NameScope.Find<TeamMembersEmailInput>(InputPart);
        if (_input is not null)
        {
            _input.Owner = this;
            _input.Text = _pendingDraft;
            _input.IsReadOnly = _isBusy;
            _input.TextChanged += OnInputTextChanged;
        }
        if (_panel is not null)
        {
            _panel.Gap = ChipGap;
            _panel.InputMinWidth = InputMinWidth;
            foreach (var chip in _chips)
            {
                _panel.Children.Insert(_panel.Children.Count - (_input is null ? 0 : 1), chip);
            }
            if (_input is not null && !_panel.Children.Contains(_input))
            {
                _panel.Children.Add(_input);
            }
        }
        UpdatePlaceholder();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PlaceholderProperty)
        {
            UpdatePlaceholder();
        }
        else if (change.Property == ChipGapProperty && _panel is not null)
        {
            _panel.Gap = ChipGap;
        }
        else if (change.Property == InputMinWidthProperty && _panel is not null)
        {
            _panel.InputMinWidth = InputMinWidth;
        }
    }

    private void OnInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        _pendingDraft = _input?.Text ?? string.Empty;
        UpdatePlaceholder();
        if (!_suppressDraftEvent)
        {
            DraftChanged?.Invoke(_pendingDraft);
        }
    }

    private bool _suppressDraftEvent;

    /// <summary>Sets the draft without raising <see cref="DraftChanged"/>.</summary>
    public void SetDraftSilently(string text)
    {
        _suppressDraftEvent = true;
        try
        {
            Draft = text;
        }
        finally
        {
            _suppressDraftEvent = false;
        }
    }

    private void UpdatePlaceholder()
    {
        if (_input is not null)
        {
            _input.PlaceholderText = _chips.Count == 0 ? Placeholder : null;
        }
    }

    /// <summary>Adds a chip at the end; with <paramref name="animate"/> it pops in and the input slides.</summary>
    public TeamMembersChip AddChip(string email, bool animate)
    {
        var chip = new TeamMembersChip
        {
            Text = email,
            RemoveName = _removeName?.Invoke(email),
            IsRemoveEnabled = !_isBusy
        };
        chip.RemoveRequested += OnChipRemoveRequested;
        _chips.Add(chip);
        if (_panel is not null)
        {
            if (animate)
            {
                _panel.Flip.Arm(MotionSpec.Spring(ArcSpring.Smooth));
            }
            var index = _input is not null ? _panel.Children.IndexOf(_input) : -1;
            if (index < 0)
            {
                _panel.Children.Add(chip);
            }
            else
            {
                _panel.Children.Insert(index, chip);
            }
            if (animate)
            {
                chip.PopIn(_panel.Flip.Get(chip));
            }
        }
        UpdatePlaceholder();
        return chip;
    }

    /// <summary>
    /// Removes the chip of <paramref name="email"/>; with <paramref name="animate"/> it shrinks and fades in place while the
    /// chips after it (and the input) slide over. Returns false when there is no such chip.
    /// </summary>
    public bool RemoveChip(string email, bool animate)
    {
        var chip = _chips.FirstOrDefault(c => string.Equals(c.Text, email, StringComparison.OrdinalIgnoreCase));
        if (chip is null)
        {
            return false;
        }
        RemoveChip(chip, animate);
        return true;
    }

    /// <summary>Takes every chip out at once (after a send: the flying copies take their place).</summary>
    public void ClearChips(bool animate)
    {
        foreach (var chip in _chips.ToArray())
        {
            RemoveChip(chip, animate, fade: false);
        }
    }

    private void RemoveChip(TeamMembersChip chip, bool animate, bool fade = true)
    {
        chip.RemoveRequested -= OnChipRemoveRequested;
        _chips.Remove(chip);
        UpdatePlaceholder();
        if (_panel is null)
        {
            return;
        }
        if (!animate || !ArcMotion.IsFull(this))
        {
            _panel.Flip.Forget(chip);
            _panel.Children.Remove(chip);
            return;
        }
        _panel.Flip.Arm(MotionSpec.Spring(ArcSpring.Smooth));
        var motion = _panel.Flip.Get(chip);
        motion.IsLeaving = true;
        chip.IsHitTestVisible = false;
        if (!fade)
        {
            motion.Opacity.Jump(0);
            _panel.Flip.Forget(chip);
            _panel.Children.Remove(chip);
            return;
        }
        _ = LeaveAsync(chip, motion);
    }

    private async Task LeaveAsync(TeamMembersChip chip, ProjectsItemMotion motion)
    {
        var exit = MotionSpec.Tween(TimeSpan.FromMilliseconds(130), ArcEasings.Exit);
        _ = motion.Scale.AnimateTo(0.9, exit);
        await motion.Opacity.AnimateTo(0, exit);
        if (_panel is not null)
        {
            _panel.Flip.Forget(chip);
            _panel.Children.Remove(chip);
        }
    }

    /// <summary>The chip of an address, or null.</summary>
    public TeamMembersChip? ChipOf(string email) =>
        _chips.FirstOrDefault(c => string.Equals(c.Text, email, StringComparison.OrdinalIgnoreCase));

    /// <summary>Puts the caret in the input.</summary>
    public void FocusInput(NavigationMethod method = NavigationMethod.Tab) => _input?.Focus(method);

    private void OnChipRemoveRequested(TeamMembersChip chip)
    {
        if (!_isBusy && chip.Text is { } text)
        {
            ChipRemoveRequested?.Invoke(text);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // A press on the field's empty space puts the caret in the input (Arc: cursor text on the whole field).
        if (!e.Handled && _input is not null && e.Source is Visual source && !IsInChipButton(source))
        {
            _input.Focus(NavigationMethod.Pointer);
            _input.CaretIndex = _input.Text?.Length ?? 0;
        }
    }

    private static bool IsInChipButton(Visual source)
    {
        for (Visual? v = source; v is not null; v = Avalonia.VisualTree.VisualExtensions.GetVisualParent(v))
        {
            if (v is AvaloniaButton or TeamMembersEmailInput)
            {
                return true;
            }
            if (v is TeamMembersEmailField)
            {
                return false;
            }
        }
        return false;
    }

    internal bool HandleInputKey(KeyEventArgs e)
    {
        var text = _input?.Text ?? string.Empty;
        switch (e.Key)
        {
            case Key.Enter:
                if (_isBusy)
                {
                    return true;
                }
                if (string.IsNullOrWhiteSpace(text))
                {
                    EnterOnEmpty?.Invoke();
                }
                else
                {
                    Committed?.Invoke(text);
                }
                return true;
            case Key.Back when text.Length == 0 && !_isBusy:
                BackspaceOnEmpty?.Invoke();
                return true;
        }
        return false;
    }

    internal bool HandleInputText(string typed)
    {
        if (_isBusy || !TeamMembersEmailParser.HasSeparator(typed) || _input is null)
        {
            return false;
        }
        var text = _input.Text ?? string.Empty;
        var caret = Math.Clamp(_input.CaretIndex, 0, text.Length);
        Committed?.Invoke(text[..caret] + typed + text[caret..]);
        return true;
    }

    internal bool HandlePaste(string text)
    {
        if (_isBusy)
        {
            return true;
        }
        if (!TeamMembersEmailParser.HasSeparator(text.Trim()))
        {
            return false;
        }
        var current = _input?.Text ?? string.Empty;
        Pasted?.Invoke(string.IsNullOrWhiteSpace(current) ? text : current + "," + text);
        return true;
    }
}

/// <summary>
/// The field's text input: bare (no frame or focus adorner), 26 tall, 6 on each side. Enter, Backspace on an empty input,
/// typed separators and pastes go to the owning field first.
/// </summary>
internal sealed class TeamMembersEmailInput : AvaloniaTextBox
{
    private bool _bypassPaste;

    public TeamMembersEmailInput()
    {
        AddHandler(PastingFromClipboardEvent, OnPastingFromClipboard);
    }

    internal TeamMembersEmailField? Owner { get; set; }

    protected override Type StyleKeyOverride => typeof(TeamMembersEmailInput);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Owner is { } owner && owner.HandleInputKey(e))
        {
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (Owner is { } owner && !string.IsNullOrEmpty(e.Text) && owner.HandleInputText(e.Text))
        {
            e.Handled = true;
            return;
        }
        base.OnTextInput(e);
    }

    private async void OnPastingFromClipboard(object? sender, RoutedEventArgs e)
    {
        if (_bypassPaste)
        {
            _bypassPaste = false;
            return;
        }
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (Owner is null || clipboard is null)
        {
            return;
        }
        e.Handled = true;
        string? text;
        try
        {
            text = await Avalonia.Input.Platform.ClipboardExtensions.TryGetTextAsync(clipboard);
        }
        catch (Exception)
        {
            text = null;
        }
        if (string.IsNullOrEmpty(text) || Owner is null)
        {
            return;
        }
        if (Owner.HandlePaste(text))
        {
            return;
        }
        _bypassPaste = true;
        Paste();
    }
}

/// <summary>
/// One address chip: 26 tall, pill shaped, SurfaceMuted with a 1px Border, the address (13) and a 20px round remove button
/// with a 12px cross.
/// </summary>
[TemplatePart(RemovePart, typeof(AvaloniaButton))]
internal sealed class TeamMembersChip : TemplatedControl
{
    public const string RemovePart = "PART_Remove";

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<TeamMembersChip, string?>(nameof(Text));

    public static readonly StyledProperty<string?> RemoveNameProperty =
        AvaloniaProperty.Register<TeamMembersChip, string?>(nameof(RemoveName));

    public static readonly StyledProperty<bool> IsRemoveEnabledProperty =
        AvaloniaProperty.Register<TeamMembersChip, bool>(nameof(IsRemoveEnabled), true);

    private AvaloniaButton? _remove;
    private MotionValue<double>? _blur;

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public string? RemoveName { get => GetValue(RemoveNameProperty); set => SetValue(RemoveNameProperty, value); }

    public bool IsRemoveEnabled { get => GetValue(IsRemoveEnabledProperty); set => SetValue(IsRemoveEnabledProperty, value); }

    /// <summary>The remove button (null before the template is applied).</summary>
    public AvaloniaButton? RemoveButton => _remove;

    public event Action<TeamMembersChip>? RemoveRequested;

    protected override AutomationPeer OnCreateAutomationPeer() => new TeamMembersChipPeer(this);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_remove is not null)
        {
            _remove.Click -= OnRemoveClick;
        }
        _remove = e.NameScope.Find<AvaloniaButton>(RemovePart);
        if (_remove is not null)
        {
            _remove.Click += OnRemoveClick;
        }
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RemoveRequested?.Invoke(this);
    }

    /// <summary>Pops in: scale 0.9 → 1 (Quick spring), opacity 0 → 1 and blur 4 → 0 in 100ms. Full motion only.</summary>
    public void PopIn(ProjectsItemMotion motion)
    {
        if (!ArcMotion.IsFull(this))
        {
            return;
        }
        var fade = MotionSpec.Tween(TimeSpan.FromMilliseconds(100), ArcEasings.Enter);
        motion.Scale.Jump(0.9);
        motion.Opacity.Jump(0);
        _ = motion.Scale.AnimateTo(1, TeamMembersEmailField.ChipPop);
        _ = motion.Opacity.AnimateTo(1, fade);
        _blur ??= CreateBlur();
        _blur.Jump(4);
        _ = _blur.AnimateTo(0, fade);
    }

    private MotionValue<double> CreateBlur()
    {
        var blur = new MotionValue<double>(this, 0, MotionVectors.Double);
        blur.Changed += r => Effect = ArcBlur.Get(r);
        return blur;
    }
}

/// <summary>A chip's 20px round remove button (its own theme: a template style cannot reach into a nested template).</summary>
internal sealed class TeamMembersChipRemoveButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(TeamMembersChipRemoveButton);
}

/// <summary>Reports a chip as a list item named by its address.</summary>
internal sealed class TeamMembersChipPeer(TeamMembersChip owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

    protected override string? GetNameCore() => ((TeamMembersChip)Owner).Text;
}

/// <summary>
/// Lays out the chips left to right with <see cref="Gap"/> between them, wrapping to new lines; the last child (the input)
/// takes what is left of the last line, or a whole new line when less than <see cref="InputMinWidth"/> is left. Chips
/// marked as leaving keep their last place and do not push the others. Moves are FLIPped (<see cref="Flip"/>).
/// </summary>
internal sealed class TeamMembersChipPanel : Panel
{
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<TeamMembersChipPanel, double>(nameof(Gap), 4);

    public static readonly StyledProperty<double> InputMinWidthProperty =
        AvaloniaProperty.Register<TeamMembersChipPanel, double>(nameof(InputMinWidth), 72);

    private readonly List<Rect> _rects = new();

    static TeamMembersChipPanel()
    {
        AffectsMeasure<TeamMembersChipPanel>(GapProperty, InputMinWidthProperty);
    }

    public TeamMembersChipPanel()
    {
        Flip = new ProjectsFlip(this);
    }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public double InputMinWidth { get => GetValue(InputMinWidthProperty); set => SetValue(InputMinWidthProperty, value); }

    public ProjectsFlip Flip { get; }

    private bool IsLeaving(Control child) => Flip.TryGet(child, out var motion) && motion.IsLeaving;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 2000;
        foreach (var child in Children)
        {
            child.Measure(new Size(width, double.PositiveInfinity));
        }
        return Layout(width, false);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Layout(finalSize.Width, true);
        return finalSize;
    }

    private Size Layout(double width, bool arrange)
    {
        _rects.Clear();
        double x = 0;
        double y = 0;
        double lineHeight = 0;
        var gap = Gap;
        var count = Children.Count;
        for (var i = 0; i < count; i++)
        {
            var child = Children[i];
            if (IsLeaving(child))
            {
                if (arrange && Flip.TryGet(child, out var leaving) && leaving.Layout is { } last)
                {
                    child.Arrange(last);
                }
                continue;
            }
            var desired = child.DesiredSize;
            var isInput = i == count - 1 && child is TeamMembersEmailInput;
            double childWidth;
            if (isInput)
            {
                var left = width - x;
                if (x > 0 && left < InputMinWidth)
                {
                    y += lineHeight + gap;
                    x = 0;
                    lineHeight = 0;
                    left = width;
                }
                childWidth = Math.Max(0, left);
            }
            else
            {
                childWidth = Math.Min(desired.Width, width);
                if (x > 0 && x + childWidth > width)
                {
                    y += lineHeight + gap;
                    x = 0;
                    lineHeight = 0;
                }
            }
            var rect = new Rect(x, y, childWidth, desired.Height);
            lineHeight = Math.Max(lineHeight, desired.Height);
            if (arrange)
            {
                child.Arrange(rect);
                Flip.Place(child, rect);
            }
            x += childWidth + gap;
        }
        return new Size(width, y + lineHeight);
    }
}
