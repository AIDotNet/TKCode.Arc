using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// One soft button of a <see cref="FloatingButtonGroup"/>: icon and label, or the icon alone with the label in a
/// tooltip. A label change crossfades in place (P2); with <see cref="ReserveLabels"/> the button already has the width
/// of the widest label, so nothing around it moves. <see cref="IsChecked"/> marks a mode that is on (aria-pressed).
/// </summary>
[PseudoClasses(ActivePseudoClass, IconOnlyPseudoClass, HasIconPseudoClass, VerticalPseudoClass, SmallPseudoClass, CheckedPseudoClass)]
public class FloatingButtonGroupItem : AvaloniaButton
{
    public const string ActivePseudoClass = ":active";
    public const string IconOnlyPseudoClass = ":icon-only";
    public const string HasIconPseudoClass = ":has-icon";
    public const string VerticalPseudoClass = ":vertical";
    public const string SmallPseudoClass = ":small";
    public const string CheckedPseudoClass = ":checked";

    #region 公共属性定义

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<FloatingButtonGroupItem, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<FloatingButtonGroupItem, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<bool?> IsIconOnlyProperty =
        AvaloniaProperty.Register<FloatingButtonGroupItem, bool?>(nameof(IsIconOnly));

    public static readonly StyledProperty<bool?> IsCheckedProperty =
        AvaloniaProperty.Register<FloatingButtonGroupItem, bool?>(nameof(IsChecked));

    public static readonly StyledProperty<string?> ShortcutProperty =
        AvaloniaProperty.Register<FloatingButtonGroupItem, string?>(nameof(Shortcut));

    public static readonly StyledProperty<IReadOnlyList<string>?> ReserveLabelsProperty =
        AvaloniaProperty.Register<FloatingButtonGroupItem, IReadOnlyList<string>?>(nameof(ReserveLabels));

    internal static readonly DirectProperty<FloatingButtonGroupItem, Control?> PhaseProperty =
        AvaloniaProperty.RegisterDirect<FloatingButtonGroupItem, Control?>(nameof(Phase), o => o.Phase);

    internal static readonly DirectProperty<FloatingButtonGroupItem, IReadOnlyList<string>> SizerLabelsProperty =
        AvaloniaProperty.RegisterDirect<FloatingButtonGroupItem, IReadOnlyList<string>>(nameof(SizerLabels), o => o.SizerLabels);

    /// <summary>Visible label, and the accessible name when only the icon shows.</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>16px Lucide glyph (24×24 path data, for example <c>ArcIcons.Undo2</c>).</summary>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>Shows only the icon; null follows the group's <see cref="FloatingButtonGroup.IsIconOnly"/>.</summary>
    public bool? IsIconOnly { get => GetValue(IsIconOnlyProperty); set => SetValue(IsIconOnlyProperty, value); }

    /// <summary>A mode that switches on (such as Present): true keeps a quiet tint and reports pressed. Null is a plain action.</summary>
    public bool? IsChecked { get => GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    /// <summary>A shortcut hint shown in the tooltip, such as "⌘Z". The group does not bind the key.</summary>
    public string? Shortcut { get => GetValue(ShortcutProperty); set => SetValue(ShortcutProperty, value); }

    /// <summary>Other labels this item can switch to (such as "Copied"); the button keeps the widest width.</summary>
    public IReadOnlyList<string>? ReserveLabels { get => GetValue(ReserveLabelsProperty); set => SetValue(ReserveLabelsProperty, value); }

    private Control? _phase;

    internal Control? Phase
    {
        get => _phase;
        private set => SetAndRaise(PhaseProperty, ref _phase, value);
    }

    private IReadOnlyList<string> _sizerLabels = [];

    internal IReadOnlyList<string> SizerLabels
    {
        get => _sizerLabels;
        private set => SetAndRaise(SizerLabelsProperty, ref _sizerLabels, value);
    }

    #endregion

    internal FloatingButtonGroup? Group { get; set; }

    private bool _effectiveIconOnly;
    private bool _built;
    private (string, Geometry?, bool) _phaseKey;

    internal bool EffectiveIconOnly => _effectiveIconOnly;

    internal void SyncFromGroup()
    {
        var group = Group;
        PseudoClasses.Set(VerticalPseudoClass, group?.Orientation == Orientation.Vertical);
        PseudoClasses.Set(SmallPseudoClass, group?.IsSmall == true);
        Rebuild();
    }

    internal void SetActive(bool active) => PseudoClasses.Set(ActivePseudoClass, active);

    protected override void OnInitialized()
    {
        base.OnInitialized();
        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty || change.Property == IconProperty || change.Property == IsIconOnlyProperty ||
            change.Property == ShortcutProperty || change.Property == ReserveLabelsProperty)
        {
            if (_built)
            {
                Rebuild();
            }
        }
        else if (change.Property == IsCheckedProperty)
        {
            PseudoClasses.Set(CheckedPseudoClass, IsChecked == true);
            if (ControlAutomationPeer.FromElement(this) is FloatingButtonGroupItemToggleAutomationPeer peer)
            {
                var oldState = change.GetOldValue<bool?>() == true ? ToggleState.On : ToggleState.Off;
                peer.RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty, oldState, peer.ToggleState);
            }
        }
    }

    private void Rebuild()
    {
        _built = true;
        var iconOnly = Icon is not null && (IsIconOnly ?? Group?.IsIconOnly ?? false);
        _effectiveIconOnly = iconOnly;
        PseudoClasses.Set(IconOnlyPseudoClass, iconOnly);
        PseudoClasses.Set(HasIconPseudoClass, Icon is not null);
        PseudoClasses.Set(CheckedPseudoClass, IsChecked == true);
        AutomationProperties.SetName(this, Label);

        // Every label the item can show is laid out invisibly in the same cell, so the width never changes.
        var labels = new List<string>();
        if (!iconOnly)
        {
            labels.Add(Label);
            if (ReserveLabels is { } reserve)
            {
                foreach (var text in reserve)
                {
                    if (!labels.Contains(text))
                    {
                        labels.Add(text);
                    }
                }
            }
        }
        SizerLabels = labels;

        // A new phase (icon + label) crossfades in place; only a visible change makes one.
        var key = (Label, Icon, iconOnly);
        if (Phase is null || !key.Equals(_phaseKey))
        {
            _phaseKey = key;
            var phase = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            if (Icon is { } icon)
            {
                phase.Children.Add(new ArcGlyph { Data = icon, Size = 16, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center });
            }
            if (!iconOnly)
            {
                phase.Children.Add(new TextBlock { Text = Label, VerticalAlignment = VerticalAlignment.Center });
            }
            Phase = phase;
        }

        // Icon-only items and shortcut hints get the library tooltip, labelled and with the hint in a quieter tone.
        if (iconOnly || !string.IsNullOrEmpty(Shortcut))
        {
            Tooltip.SetTip(this, CreateTip());
            Tooltip.SetSide(this, Group?.EffectiveTooltipSide ?? TooltipSide.Top);
            AutomationProperties.SetHelpText(this, string.IsNullOrEmpty(Shortcut) ? string.Empty : Shortcut);
        }
        else
        {
            ClearValue(Tooltip.TipProperty);
            AutomationProperties.SetHelpText(this, string.Empty);
        }
    }

    private object CreateTip()
    {
        if (string.IsNullOrEmpty(Shortcut))
        {
            return Label;
        }
        var shortcut = new TextBlock { Text = Shortcut, Opacity = 0.58 };
        ArcTokens.Bind(shortcut, TextBlock.ForegroundProperty, DesignTokens.ArcDesignTokenKind.Background);
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children = { new TextBlock { Text = Label }, shortcut }
        };
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        Group?.OnItemFocused(this, e.NavigationMethod);
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        IsChecked is null ? new ButtonAutomationPeer(this) : new FloatingButtonGroupItemToggleAutomationPeer(this);

    private sealed class FloatingButtonGroupItemToggleAutomationPeer : ButtonAutomationPeer, IToggleProvider
    {
        public FloatingButtonGroupItemToggleAutomationPeer(FloatingButtonGroupItem owner) : base(owner)
        {
        }

        public ToggleState ToggleState => ((FloatingButtonGroupItem)Owner).IsChecked == true ? ToggleState.On : ToggleState.Off;

        public void Toggle() => Invoke();
    }
}

/// <summary>A thin rule between groups of buttons in a <see cref="FloatingButtonGroup"/>.</summary>
[PseudoClasses(VerticalPseudoClass, SmallPseudoClass)]
public class FloatingButtonGroupSeparator : Control
{
    public const string VerticalPseudoClass = ":vertical";
    public const string SmallPseudoClass = ":small";

    public static readonly StyledProperty<IBrush?> RuleBrushProperty =
        AvaloniaProperty.Register<FloatingButtonGroupSeparator, IBrush?>(nameof(RuleBrush));

    /// <summary>Color of the rule (theme: foreground at 12%).</summary>
    public IBrush? RuleBrush { get => GetValue(RuleBrushProperty); set => SetValue(RuleBrushProperty, value); }

    static FloatingButtonGroupSeparator()
    {
        AffectsRender<FloatingButtonGroupSeparator>(RuleBrushProperty);
        FocusableProperty.OverrideDefaultValue<FloatingButtonGroupSeparator>(false);
    }

    internal void Sync(bool vertical, bool small)
    {
        PseudoClasses.Set(VerticalPseudoClass, vertical);
        PseudoClasses.Set(SmallPseudoClass, small);
    }

    public override void Render(DrawingContext context)
    {
        if (RuleBrush is { } brush)
        {
            context.DrawRectangle(brush, null, new RoundedRect(new Rect(Bounds.Size), 1));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new FloatingButtonGroupSeparatorAutomationPeer(this);

    private sealed class FloatingButtonGroupSeparatorAutomationPeer : ControlAutomationPeer
    {
        public FloatingButtonGroupSeparatorAutomationPeer(Control owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Separator;
    }
}
