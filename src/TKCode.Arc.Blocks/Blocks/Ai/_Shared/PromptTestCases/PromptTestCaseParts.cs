using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// One option of <see cref="PromptTestCases"/>: a 30px text button with no background of its own (the gliding background
/// shows under the selected one), labelled by its case. <c>:selected</c> while its case is selected; <c>:hover</c> for
/// mouse and pen only. Reads as a radio button that is checked while selected.
/// </summary>
[PseudoClasses(SelectedPseudoClass, HoverPseudoClass)]
internal sealed class PromptTestCaseOption : AvaloniaButton
{
    public const string SelectedPseudoClass = ":selected";
    public const string HoverPseudoClass = ":hover";

    /// <summary>The visible and accessible label (the case's label).</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<PromptTestCaseOption, string?>(nameof(Label));

    /// <summary>Whether this option's case is selected.</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<PromptTestCaseOption, bool>(nameof(IsSelected));

    private PromptTestCase? _case;
    private IDisposable? _labelBinding;

    protected override Type StyleKeyOverride => typeof(PromptTestCaseOption);

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="IsSelectedProperty"/>
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    /// <summary>The case this option stands for; its label follows the case's.</summary>
    public PromptTestCase? Case
    {
        get => _case;
        set
        {
            if (ReferenceEquals(_case, value))
            {
                return;
            }
            _labelBinding?.Dispose();
            _labelBinding = null;
            _case = value;
            if (value is null)
            {
                ClearValue(LabelProperty);
            }
            else if (this.IsAttachedToVisualTree())
            {
                FollowCase();
            }
            else
            {
                // The label as it is now; attaching makes it follow the case.
                Label = value.Label;
            }
        }
    }

    /// <summary>The case is app data that can outlive the option: its label is followed only while the option is in a window.</summary>
    private void FollowCase()
    {
        if (_case is not null && _labelBinding is null)
        {
            _labelBinding = Bind(LabelProperty, _case.GetObservable(PromptTestCase.LabelProperty));
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        FollowCase();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _labelBinding?.Dispose();
        _labelBinding = null;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            var selected = change.GetNewValue<bool>();
            PseudoClasses.Set(SelectedPseudoClass, selected);
            if (ControlAutomationPeer.FromElement(this) is OptionPeer peer)
            {
                peer.RaiseSelectedChanged(selected);
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new OptionPeer(this);

    /// <summary>A radio button named by its label, selected while its case is (Arc's <c>role="radio"</c> + <c>aria-checked</c>).</summary>
    private sealed class OptionPeer(PromptTestCaseOption owner) : ButtonAutomationPeer(owner), ISelectionItemProvider
    {
        private PromptTestCaseOption Option => (PromptTestCaseOption)Owner;

        public bool IsSelected => Option.IsSelected;

        public ISelectionProvider? SelectionContainer =>
            Option.FindAncestorOfType<PromptTestCases>() is { } group ? CreatePeerForElement(group) as ISelectionProvider : null;

        public void Select()
        {
            if (Option.FindAncestorOfType<PromptTestCases>() is { } group)
            {
                group.Choose(Option);
            }
        }

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }

        public void RaiseSelectedChanged(bool selected) =>
            RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, !selected, selected);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.RadioButton;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Option.Label;

        protected override object? GetProviderCore(Type providerType) =>
            providerType == typeof(ISelectionItemProvider) ? this : base.GetProviderCore(providerType);
    }
}

/// <summary>
/// The row inside <see cref="PromptTestCases"/>' frame: lays the options side by side with <see cref="Spacing"/> between
/// them, vertically centered, and stretches every other child (the gliding background) over the whole row, so an
/// option's <see cref="Visual.Bounds"/> is also its rectangle in the background's coordinates. With
/// <see cref="IsUniform"/> the options share the arranged width equally (CSS <c>flex: 1 1 0</c>). Raises
/// <see cref="Arranged"/> after each arrange so the background can follow.
/// </summary>
internal sealed class PromptTestCaseTrack : Panel
{
    /// <summary>The gap between two options.</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<PromptTestCaseTrack, double>(nameof(Spacing));

    /// <summary>Whether the options share the width equally.</summary>
    public static readonly StyledProperty<bool> IsUniformProperty =
        AvaloniaProperty.Register<PromptTestCaseTrack, bool>(nameof(IsUniform));

    static PromptTestCaseTrack()
    {
        AffectsMeasure<PromptTestCaseTrack>(SpacingProperty, IsUniformProperty);
    }

    /// <inheritdoc cref="SpacingProperty"/>
    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    /// <inheritdoc cref="IsUniformProperty"/>
    public bool IsUniform { get => GetValue(IsUniformProperty); set => SetValue(IsUniformProperty, value); }

    /// <summary>Raised at the end of every arrange pass of the row.</summary>
    public event Action? Arranged;

    protected override Size MeasureOverride(Size availableSize)
    {
        var optionSize = new Size(double.PositiveInfinity, availableSize.Height);
        var width = 0.0;
        var widest = 0.0;
        var height = 0.0;
        var options = 0;
        foreach (var child in Children)
        {
            if (child is PromptTestCaseOption)
            {
                child.Measure(optionSize);
                if (child.IsVisible)
                {
                    width += child.DesiredSize.Width;
                    widest = Math.Max(widest, child.DesiredSize.Width);
                    height = Math.Max(height, child.DesiredSize.Height);
                    options++;
                }
            }
            else
            {
                child.Measure(default);
            }
        }
        if (IsUniform)
        {
            // Equal shares: as wide as the widest option in every slot (the stretched layout then fills the row).
            width = widest * options;
        }
        if (options > 1)
        {
            width += Spacing * (options - 1);
        }
        return new Size(width, height);
    }

    /// <summary>
    /// The width of a uniform option: CSS <c>flex: 1 1 0</c> with the automatic minimum, so options share the width
    /// equally but none gets less than its content; what the wider ones take is shared among the rest.
    /// </summary>
    internal static double UniformShare(IReadOnlyList<double> desired, double width, double spacing, int index)
    {
        var count = desired.Count;
        if (count == 0)
        {
            return 0;
        }
        var free = Math.Max(0, width - spacing * (count - 1));
        Span<bool> frozen = count <= 32 ? stackalloc bool[count] : new bool[count];
        var open = count;
        var share = free / count;
        var changed = true;
        while (changed && open > 0)
        {
            changed = false;
            share = free / open;
            for (var i = 0; i < count; i++)
            {
                if (!frozen[i] && desired[i] > share)
                {
                    frozen[i] = true;
                    free -= desired[i];
                    open--;
                    changed = true;
                }
            }
            free = Math.Max(0, free);
        }
        return frozen[index] ? desired[index] : open > 0 ? free / open : desired[index];
    }

    private readonly List<double> _desired = new();

    protected override Size ArrangeOverride(Size finalSize)
    {
        _desired.Clear();
        foreach (var child in Children)
        {
            if (child is PromptTestCaseOption && child.IsVisible)
            {
                _desired.Add(child.DesiredSize.Width);
            }
        }
        var x = 0.0;
        var index = 0;
        foreach (var child in Children)
        {
            if (child is PromptTestCaseOption)
            {
                if (!child.IsVisible)
                {
                    continue;
                }
                var size = child.DesiredSize;
                var width = IsUniform ? UniformShare(_desired, finalSize.Width, Spacing, index) : size.Width;
                index++;
                child.Arrange(new Rect(x, Math.Max(0, (finalSize.Height - size.Height) / 2), width, size.Height));
                x += width + Spacing;
            }
            else
            {
                child.Arrange(new Rect(finalSize));
            }
        }
        Arranged?.Invoke();
        return finalSize;
    }
}
