using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// One segment of a <see cref="ButtonGroup"/>: icon and label, the icon alone (<see cref="IsIconOnly"/>, the label moves
/// to the accessible name and a tooltip), or a live <see cref="Value"/> such as "100%". A label change crossfades in
/// place (the new one rises out of a soft blur); with <see cref="ReserveLabels"/> the segment already has the width of the
/// widest label, otherwise its slot springs to the new width. Pressing dips the content in place; the group and its
/// neighbours stay still. <see cref="IsDisabled"/> keeps the segment focusable (aria-disabled) but swallows activation;
/// <see cref="NavigateUri"/> opens a link.
/// </summary>
[PseudoClasses(IconOnlyPseudoClass, HasIconPseudoClass, CollapsedPseudoClass, ValuePseudoClass, InertPseudoClass,
    VerticalPseudoClass, SmallPseudoClass, SolidPseudoClass, JoinedPseudoClass, QuietPseudoClass, ActivePseudoClass)]
public class ButtonGroupItem : AvaloniaButton
{
    public const string IconOnlyPseudoClass = ":icon-only";
    public const string HasIconPseudoClass = ":has-icon";
    public const string CollapsedPseudoClass = ":collapsed";
    public const string ValuePseudoClass = ":value";
    public const string InertPseudoClass = ":inert";
    public const string VerticalPseudoClass = ":vertical";
    public const string SmallPseudoClass = ":small";
    public const string SolidPseudoClass = ":solid";
    public const string JoinedPseudoClass = ":joined";
    public const string QuietPseudoClass = ":quiet";
    public const string ActivePseudoClass = ":active";

    #region 公共属性定义

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ButtonGroupItem, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<ButtonGroupItem, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<bool> IsIconOnlyProperty =
        AvaloniaProperty.Register<ButtonGroupItem, bool>(nameof(IsIconOnly));

    public static readonly StyledProperty<object?> ValueProperty =
        AvaloniaProperty.Register<ButtonGroupItem, object?>(nameof(Value));

    public static readonly StyledProperty<IReadOnlyList<string>?> ReserveLabelsProperty =
        AvaloniaProperty.Register<ButtonGroupItem, IReadOnlyList<string>?>(nameof(ReserveLabels));

    public static readonly StyledProperty<Uri?> NavigateUriProperty =
        AvaloniaProperty.Register<ButtonGroupItem, Uri?>(nameof(NavigateUri));

    public static readonly StyledProperty<bool> IsDisabledProperty =
        AvaloniaProperty.Register<ButtonGroupItem, bool>(nameof(IsDisabled));

    /// <summary>Gap between icon and label (theme: 8 / 6px).</summary>
    internal static readonly StyledProperty<double> LabelGapProperty =
        AvaloniaProperty.Register<ButtonGroupItem, double>(nameof(LabelGap), 8);

    internal static readonly DirectProperty<ButtonGroupItem, Control?> PhaseProperty =
        AvaloniaProperty.RegisterDirect<ButtonGroupItem, Control?>(nameof(Phase), o => o.Phase);

    internal static readonly DirectProperty<ButtonGroupItem, IReadOnlyList<string>> SizerLabelsProperty =
        AvaloniaProperty.RegisterDirect<ButtonGroupItem, IReadOnlyList<string>>(nameof(SizerLabels), o => o.SizerLabels);

    /// <summary>Visible text and always the accessible name. Changing it crossfades in place, so Share can answer Copied.</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>16px Lucide glyph (24×24 path data, for example <c>ArcIcons.Share2</c>).</summary>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>Shows only the icon; the label becomes the accessible name and the tooltip.</summary>
    public bool IsIconOnly { get => GetValue(IsIconOnlyProperty); set => SetValue(IsIconOnlyProperty, value); }

    /// <summary>
    /// Visible content in place of the label, such as a live zoom value (text uses tabular figures; a control is shown as
    /// is). The label stays the accessible name. Changing the value updates in place.
    /// </summary>
    public object? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>Every other label this segment can show, such as "Copied"; the segment keeps the widest width.</summary>
    public IReadOnlyList<string>? ReserveLabels { get => GetValue(ReserveLabelsProperty); set => SetValue(ReserveLabelsProperty, value); }

    /// <summary>Renders the segment as a link: activation opens this address with the platform launcher.</summary>
    public Uri? NavigateUri { get => GetValue(NavigateUriProperty); set => SetValue(NavigateUriProperty, value); }

    /// <summary>Unavailable but still focusable (aria-disabled): activation is swallowed and the content dims.</summary>
    public bool IsDisabled { get => GetValue(IsDisabledProperty); set => SetValue(IsDisabledProperty, value); }

    internal double LabelGap { get => GetValue(LabelGapProperty); set => SetValue(LabelGapProperty, value); }

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

    private static readonly FontFeatureCollection TabularFigures = new() { FontFeature.Parse("tnum") };
    private static readonly object ValueKey = new();

    private bool _built;
    private (Geometry?, object?) _phaseKey;
    private TextBlock? _phaseLabel;
    private TextBlock? _phaseValue;

    internal ButtonGroup? Group { get; set; }

    /// <summary>A segment whose label can collapse to its icon when the row runs out of room.</summary>
    internal bool IsCollapsible => Icon is not null && !IsIconOnly && Value is null;

    /// <summary>Hover and press show nothing on a disabled segment; keyboard focus still does.</summary>
    internal bool IsInert => IsDisabled || !IsEffectivelyEnabled;

    internal void SyncFromGroup()
    {
        var group = Group;
        PseudoClasses.Set(VerticalPseudoClass, group?.Orientation == Orientation.Vertical);
        PseudoClasses.Set(SmallPseudoClass, group?.IsSmall == true);
        PseudoClasses.Set(SolidPseudoClass, group?.Variant == ButtonGroupVariant.Solid);
        Rebuild();
    }

    internal void SetSegmentState(bool joined, bool quiet, bool active)
    {
        PseudoClasses.Set(JoinedPseudoClass, joined);
        PseudoClasses.Set(QuietPseudoClass, quiet);
        PseudoClasses.Set(ActivePseudoClass, active);
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty || change.Property == IconProperty || change.Property == IsIconOnlyProperty ||
            change.Property == ValueProperty || change.Property == ReserveLabelsProperty)
        {
            if (_built)
            {
                Rebuild();
            }
        }
        else if (change.Property == IsDisabledProperty || change.Property == IsEffectivelyEnabledProperty)
        {
            PseudoClasses.Set(InertPseudoClass, IsDisabled);
        }
    }

    internal void Rebuild()
    {
        _built = true;
        var iconOnly = Icon is not null && IsIconOnly;
        var collapsed = !iconOnly && IsCollapsible && Group?.AreLabelsCollapsed == true;
        var hasValue = Value is not null;
        PseudoClasses.Set(IconOnlyPseudoClass, iconOnly);
        PseudoClasses.Set(CollapsedPseudoClass, collapsed);
        PseudoClasses.Set(HasIconPseudoClass, Icon is not null);
        PseudoClasses.Set(ValuePseudoClass, hasValue);
        PseudoClasses.Set(InertPseudoClass, IsDisabled);
        AutomationProperties.SetName(this, Label);

        // Every label the segment can show sits invisibly in the same cell, so a label change never resizes it.
        var labels = new List<string>();
        if (!iconOnly && !hasValue && !collapsed)
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
        if (!labels.SequenceEqual(SizerLabels))
        {
            SizerLabels = labels;
        }

        // A new phase (icon + label) crossfades in place; a live value or a collapse updates the current phase.
        object? textKey = hasValue ? Value as Control ?? ValueKey : iconOnly ? null : Label;
        var key = (Icon, textKey);
        if (Phase is null || !Equals(key, _phaseKey))
        {
            _phaseKey = key;
            if (Value is Control hosted && hosted.Parent is Panel previous)
            {
                // A control value moves into the new phase; the old phase leaves without it.
                previous.Children.Remove(hosted);
            }
            var phase = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            phase.Bind(StackPanel.SpacingProperty, this.GetObservable(LabelGapProperty));
            if (Icon is { } icon)
            {
                phase.Children.Add(new ArcGlyph { Data = icon, Size = 16, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center });
            }
            _phaseLabel = null;
            _phaseValue = null;
            if (hasValue)
            {
                if (Value is Control control)
                {
                    phase.Children.Add(control);
                }
                else
                {
                    _phaseValue = new TextBlock { FontFeatures = TabularFigures, VerticalAlignment = VerticalAlignment.Center };
                    phase.Children.Add(_phaseValue);
                }
            }
            else if (!iconOnly)
            {
                _phaseLabel = new TextBlock { Text = Label, VerticalAlignment = VerticalAlignment.Center };
                phase.Children.Add(_phaseLabel);
            }
            Phase = phase;
        }
        if (_phaseValue is not null)
        {
            _phaseValue.Text = Value?.ToString();
        }
        if (_phaseLabel is not null)
        {
            // Collapsing drops the label without a crossfade; the label moves to the name and the tooltip.
            _phaseLabel.IsVisible = !collapsed;
        }

        if (iconOnly || collapsed)
        {
            Tooltip.SetTip(this, Label);
            Tooltip.SetSide(this, Group?.Orientation == Orientation.Vertical ? TooltipSide.Right : TooltipSide.Top);
        }
        else
        {
            ClearValue(Tooltip.TipProperty);
        }
    }

    protected override void OnClick()
    {
        // Disabled segments stay focusable (aria-disabled) so the keyboard can find them; activation is swallowed.
        if (IsDisabled)
        {
            return;
        }
        // Recorded before the click handlers run, so a label they change (Copied) is announced.
        Group?.OnSegmentActivating(this);
        base.OnClick();
        if (NavigateUri is { } uri && TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            _ = launcher.LaunchUriAsync(uri);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        Group?.OnSegmentFocused(this, e.NavigationMethod);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ButtonGroupItemAutomationPeer(this);

    private sealed class ButtonGroupItemAutomationPeer : ButtonAutomationPeer
    {
        public ButtonGroupItemAutomationPeer(ButtonGroupItem owner) : base(owner)
        {
        }

        private ButtonGroupItem Item => (ButtonGroupItem)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() =>
            Item.NavigateUri is null ? AutomationControlType.Button : AutomationControlType.Hyperlink;

        protected override bool IsEnabledCore() => base.IsEnabledCore() && !Item.IsDisabled;
    }
}
