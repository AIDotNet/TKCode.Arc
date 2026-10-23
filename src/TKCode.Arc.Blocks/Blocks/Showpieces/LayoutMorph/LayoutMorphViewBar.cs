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
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaRadioButton = Avalonia.Controls.RadioButton;

namespace TKCode.Arc;

/// <summary>
/// The view switch of a <see cref="LayoutMorph"/> (Arc's "Gallery view" radio group): one option per layout with a
/// Lucide icon and a label, a white pill gliding to the chosen one (k 390 / c 39), arrows (wrapping), Home and End
/// choosing as they move, and only the chosen option in the tab order. Compact (narrow containers): icons only,
/// options stretched to equal widths, the label as a tooltip and the accessible name.
/// </summary>
[TemplatePart(OptionsPart, typeof(LayoutMorphViewBarPanel))]
[TemplatePart(PillPart, typeof(GlidingIndicator))]
[PseudoClasses(CompactPseudoClass)]
internal sealed class LayoutMorphViewBar : TemplatedControl
{
    public const string OptionsPart = "PART_Options";
    public const string PillPart = "PART_Pill";
    public const string CompactPseudoClass = ":compact";

    /// <summary>The pill's spring (Arc: k 390, c 39, about 600ms to rest).</summary>
    internal static readonly SpringSpec PillSpring = SpringSpec.FromPhysical(390, 39);

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<LayoutMorphViewBar, bool>(nameof(IsCompact));

    public static readonly StyledProperty<LayoutMorphMode> SelectedModeProperty =
        AvaloniaProperty.Register<LayoutMorphViewBar, LayoutMorphMode>(nameof(SelectedMode));

    private readonly List<LayoutMorphViewOption> _options = new();
    private readonly string _group = "LayoutMorphView" + Guid.NewGuid().ToString("N");
    private LayoutMorphViewBarPanel? _panel;
    private GlidingIndicator? _pill;
    private bool _syncing;
    private bool _pillPlaced;

    public LayoutMorphViewBar()
    {
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Once);
    }

    /// <summary>Icons only, options stretched to equal widths.</summary>
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    /// <summary>The chosen layout.</summary>
    public LayoutMorphMode SelectedMode { get => GetValue(SelectedModeProperty); set => SetValue(SelectedModeProperty, value); }

    /// <summary>A person chose an option (click, keys): the mode and the option, whose center starts the stagger.</summary>
    public event Action<LayoutMorphMode, LayoutMorphViewOption>? Chosen;

    internal IReadOnlyList<LayoutMorphViewOption> Options => _options;

    internal GlidingIndicator? Pill => _pill;

    /// <summary>Replaces the options (order and subset as given).</summary>
    public void SetOptions(IReadOnlyList<(LayoutMorphMode Mode, string Label, Geometry Icon)> options)
    {
        foreach (var option in _options)
        {
            option.IsCheckedChanged -= OnOptionChecked;
        }
        _options.Clear();
        foreach (var (mode, label, icon) in options)
        {
            var option = new LayoutMorphViewOption
            {
                Mode = mode,
                Label = label,
                Icon = icon,
                GroupName = _group,
                IsCompact = IsCompact
            };
            option.IsCheckedChanged += OnOptionChecked;
            _options.Add(option);
        }
        if (_panel is not null)
        {
            _panel.Children.Clear();
            _panel.Children.AddRange(_options);
        }
        SyncChecked();
    }

    /// <summary>Renames the options (labels changed).</summary>
    public void Relabel(Func<LayoutMorphMode, string> label)
    {
        foreach (var option in _options)
        {
            option.Label = label(option.Mode);
        }
    }

    internal LayoutMorphViewOption? OptionFor(LayoutMorphMode mode) => _options.FirstOrDefault(o => o.Mode == mode);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_panel is not null)
        {
            _panel.Children.Clear();
            _panel.LayoutUpdated -= OnPanelLayoutUpdated;
        }
        _panel = e.NameScope.Find<LayoutMorphViewBarPanel>(OptionsPart);
        _pill = e.NameScope.Find<GlidingIndicator>(PillPart);
        _pillPlaced = false;
        if (_panel is not null)
        {
            _panel.IsUniform = IsCompact;
            _panel.Children.AddRange(_options);
            _panel.LayoutUpdated += OnPanelLayoutUpdated;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedModeProperty)
        {
            SyncChecked();
            MovePill(animate: true);
        }
        else if (change.Property == IsCompactProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, IsCompact);
            foreach (var option in _options)
            {
                option.IsCompact = IsCompact;
            }
            if (_panel is not null)
            {
                _panel.IsUniform = IsCompact;
            }
            _pillPlaced = false;
        }
    }

    private void SyncChecked()
    {
        _syncing = true;
        foreach (var option in _options)
        {
            var on = option.Mode == SelectedMode;
            option.IsChecked = on;
            KeyboardNavigation.SetIsTabStop(option, on);
        }
        _syncing = false;
    }

    private void OnOptionChecked(object? sender, RoutedEventArgs e)
    {
        if (_syncing || sender is not LayoutMorphViewOption { IsChecked: true } option)
        {
            return;
        }
        Choose(option, focus: false);
    }

    private void Choose(LayoutMorphViewOption option, bool focus)
    {
        if (focus)
        {
            option.Focus(NavigationMethod.Directional);
        }
        if (option.Mode == SelectedMode)
        {
            SyncChecked();
            return;
        }
        SetCurrentValue(SelectedModeProperty, option.Mode);
        Chosen?.Invoke(option.Mode, option);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && e.Source is Visual && _options.Count > 0 && e.KeyModifiers == KeyModifiers.None)
        {
            var current = _options.FindIndex(o => o.Mode == SelectedMode);
            var next = e.Key switch
            {
                Key.Left or Key.Up => (current - 1 + _options.Count) % _options.Count,
                Key.Right or Key.Down => (current + 1) % _options.Count,
                Key.Home => 0,
                Key.End => _options.Count - 1,
                _ => -1
            };
            if (next >= 0)
            {
                Choose(_options[next], focus: true);
                e.Handled = true;
                return;
            }
        }
        base.OnKeyDown(e);
    }

    private void OnPanelLayoutUpdated(object? sender, EventArgs e) => MovePill(animate: false, onlyIfMisplaced: true);

    private void MovePill(bool animate, bool onlyIfMisplaced = false)
    {
        if (_pill is null || OptionFor(SelectedMode) is not { } option || option.Bounds.Width <= 0)
        {
            return;
        }
        var target = option.Bounds;
        if (onlyIfMisplaced && _pillPlaced && (_pill.HasTarget && !animate) && Near(LastTarget, target))
        {
            return;
        }
        LastTarget = target;
        _pill.MoveTo(target, PillSpring, animate && _pillPlaced);
        _pillPlaced = true;
    }

    private Rect LastTarget { get; set; }

    private static bool Near(Rect a, Rect b) =>
        Math.Abs(a.X - b.X) < 0.25 && Math.Abs(a.Y - b.Y) < 0.25 && Math.Abs(a.Width - b.Width) < 0.25 && Math.Abs(a.Height - b.Height) < 0.25;

    protected override AutomationPeer OnCreateAutomationPeer() => new LayoutMorphViewBarAutomationPeer(this);
}

/// <summary>The view switch is a named group ("radio group") of radio buttons.</summary>
internal sealed class LayoutMorphViewBarAutomationPeer : ControlAutomationPeer
{
    public LayoutMorphViewBarAutomationPeer(LayoutMorphViewBar owner) : base(owner)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string GetLocalizedControlTypeCore() => "radio group";

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}

/// <summary>One option of the view switch: an icon and a label (the label hides on compact bars).</summary>
internal sealed class LayoutMorphViewOption : AvaloniaRadioButton
{
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<LayoutMorphViewOption, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<LayoutMorphViewOption, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<LayoutMorphViewOption, bool>(nameof(IsCompact));

    public LayoutMorphMode Mode { get; init; }

    /// <summary>The Lucide icon (16px, stroke 2).</summary>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>The label, also the accessible name and the compact tooltip.</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Icon only.</summary>
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    protected override Type StyleKeyOverride => typeof(LayoutMorphViewOption);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty || change.Property == IsCompactProperty)
        {
            AutomationProperties.SetName(this, Label);
            Tooltip.SetTip(this, IsCompact ? Label : null);
            PseudoClasses.Set(":compact", IsCompact);
        }
    }
}

/// <summary>Lays the options out in a row with a gap; uniform: equal widths that fill the bar.</summary>
internal sealed class LayoutMorphViewBarPanel : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<LayoutMorphViewBarPanel, double>(nameof(Spacing), 2);

    public static readonly StyledProperty<bool> IsUniformProperty =
        AvaloniaProperty.Register<LayoutMorphViewBarPanel, bool>(nameof(IsUniform));

    static LayoutMorphViewBarPanel()
    {
        AffectsMeasure<LayoutMorphViewBarPanel>(SpacingProperty, IsUniformProperty);
    }

    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    public bool IsUniform { get => GetValue(IsUniformProperty); set => SetValue(IsUniformProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var n = Children.Count;
        if (n == 0)
        {
            return default;
        }
        var gaps = Spacing * (n - 1);
        double width = 0, height = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }
        if (IsUniform && double.IsFinite(availableSize.Width))
        {
            return new Size(availableSize.Width, height);
        }
        return new Size(width + gaps, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var n = Children.Count;
        if (n == 0)
        {
            return finalSize;
        }
        var gaps = Spacing * (n - 1);
        var x = 0.0;
        var uniform = IsUniform ? Math.Max(0, (finalSize.Width - gaps) / n) : 0;
        foreach (var child in Children)
        {
            var w = IsUniform ? uniform : child.DesiredSize.Width;
            child.Arrange(new Rect(x, 0, w, finalSize.Height));
            x += w + Spacing;
        }
        return finalSize;
    }
}
