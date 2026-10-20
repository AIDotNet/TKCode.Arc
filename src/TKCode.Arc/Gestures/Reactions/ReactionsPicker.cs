using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>What the picker caption says: the active option, with a check when it is already chosen.</summary>
internal sealed record ReactionsCaption(string Label, bool Chosen);

/// <summary>One picker option: a 40px round toggle whose pressed state is "already chosen".</summary>
internal sealed class ReactionsPickerOption : ToggleButton
{
    public const string LitPseudoClass = ":lit";

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<ReactionsPickerOption, Geometry?>(nameof(Icon));

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    protected override Type StyleKeyOverride => typeof(ReactionsPickerOption);

    public string Id { get; set; } = string.Empty;

    public int Index { get; set; }

    /// <summary>Entrance progress values (scale on snappy, opacity on fast), driven by the picker.</summary>
    internal MotionValue<double>? EnterScale { get; set; }

    internal MotionValue<double>? EnterFade { get; set; }

    internal ScaleTransform EnterTransform { get; } = new();

    internal void SetLit(bool lit) => PseudoClasses.Set(LitPseudoClass, lit);

    /// <summary>Picking is the picker's job (it closes the tray and toggles the reaction).</summary>
    protected override void Toggle()
    {
    }
}

/// <summary>
/// The tray of every option (Arc's Picker): a toolbar of round options with roving focus (all four arrows move and
/// wrap, Home / End jump), a shared highlight that glides to the hovered or focused option on the morph spring (P7),
/// the lit icon lifting to 1.14, and a caption naming the active option (P2). Options arrive 40ms after opening, 35ms
/// apart, from scale 0.6 out of a subtle blur (P10). Shown inside the merged <see cref="Popover"/>. The entrance is
/// code-driven: it replays on every open with a per-option delay, which a ControlTheme transition cannot stagger.
/// </summary>
[TemplatePart(OptionsPart, typeof(StackPanel))]
[TemplatePart(HighlightPart, typeof(GlidingIndicator))]
[TemplatePart(CaptionPart, typeof(ArcContentSwap))]
internal sealed class ReactionsPicker : TemplatedControl
{
    public const string OptionsPart = "PART_Options";
    public const string HighlightPart = "PART_Highlight";
    public const string CaptionPart = "PART_Caption";

    /// <summary>Padding the hosting popover surface uses for this tray (the theme sets Arc's 5px).</summary>
    public static readonly StyledProperty<Thickness> SurfacePaddingProperty =
        AvaloniaProperty.Register<ReactionsPicker, Thickness>(nameof(SurfacePadding), new Thickness(5));

    public Thickness SurfacePadding { get => GetValue(SurfacePaddingProperty); set => SetValue(SurfacePaddingProperty, value); }

    private static readonly ImmutableBlurEffect s_subtleBlur = new(2);

    private readonly List<ReactionsPickerOption> _options = new();
    private IReadOnlyList<ReactionOption> _source = Array.Empty<ReactionOption>();
    private ISet<string> _reacted = new HashSet<string>();
    private StackPanel? _panel;
    private GlidingIndicator? _highlight;
    private ArcContentSwap? _caption;
    private int _focusIndex;
    private int? _hovered;
    private bool _placed;
    private bool _keyboard;

    /// <summary>An option was chosen (its reaction id).</summary>
    public event EventHandler<string>? Picked;

    protected override Type StyleKeyOverride => typeof(ReactionsPicker);

    public IReadOnlyList<ReactionsPickerOption> Options => _options;

    /// <summary>The option that is lit (hovered with a mouse, otherwise focused).</summary>
    public int ActiveIndex => _options.Count == 0 ? -1 : Math.Min(_hovered ?? _focusIndex, _options.Count - 1);

    /// <summary>True when the last pick came from the keyboard.</summary>
    public bool PickedWithKeyboard => _keyboard;

    public static readonly IDataTemplate CaptionTemplate = new FuncDataTemplate<ReactionsCaption>((caption, _) =>
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        if (caption?.Chosen == true)
        {
            row.Children.Add(new ArcGlyph { Data = ArcIcons.Check, Size = 12, StrokeThickness = 2, VerticalAlignment = VerticalAlignment.Center });
        }
        row.Children.Add(new TextBlock { Text = caption?.Label, VerticalAlignment = VerticalAlignment.Center });
        return row;
    });

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_panel is not null)
        {
            _panel.Children.Clear();
            _panel.PointerExited -= OnPanelPointerExited;
        }
        base.OnApplyTemplate(e);
        _panel = e.NameScope.Find<StackPanel>(OptionsPart);
        _highlight = e.NameScope.Find<GlidingIndicator>(HighlightPart);
        _caption = e.NameScope.Find<ArcContentSwap>(CaptionPart);
        if (_caption is not null)
        {
            _caption.ContentTemplate = CaptionTemplate;
        }
        if (_panel is not null)
        {
            _panel.PointerExited += OnPanelPointerExited;
            AutomationProperties.SetAccessibilityView(_panel, AccessibilityView.Content);
            foreach (var option in _options)
            {
                _panel.Children.Add(option);
            }
        }
        _placed = false;
        UpdateActive();
    }

    /// <summary>Shows <paramref name="options"/> (in picker order) with <paramref name="reacted"/> marked as chosen.</summary>
    public void SetOptions(IReadOnlyList<ReactionOption> options, ISet<string> reacted)
    {
        _reacted = reacted;
        if (!ReferenceEquals(options, _source) || options.Count != _options.Count)
        {
            _source = options;
            _panel?.Children.Clear();
            foreach (var old in _options)
            {
                old.Click -= OnOptionClick;
                old.GotFocus -= OnOptionGotFocus;
                old.PointerMoved -= OnOptionPointerMoved;
            }
            _options.Clear();
            for (var i = 0; i < options.Count; i++)
            {
                var option = new ReactionsPickerOption { Index = i };
                option.RenderTransformOrigin = RelativePoint.Center;
                option.RenderTransform = option.EnterTransform;
                option.Click += OnOptionClick;
                option.GotFocus += OnOptionGotFocus;
                option.PointerMoved += OnOptionPointerMoved;
                _options.Add(option);
                _panel?.Children.Add(option);
            }
        }
        for (var i = 0; i < options.Count; i++)
        {
            var source = options[i];
            var option = _options[i];
            option.Id = source.Id;
            option.Icon = source.Icon;
            option.IsChecked = reacted.Contains(source.Id);
            AutomationProperties.SetName(option, source.Label);
        }
        _focusIndex = Math.Clamp(_focusIndex, 0, Math.Max(0, _options.Count - 1));
        UpdateActive();
    }

    /// <summary>Resets roving focus and plays the staggered entrance (called on every open).</summary>
    public void Reset(ArcMotionLevel level)
    {
        _focusIndex = 0;
        _hovered = null;
        _placed = false;
        _keyboard = false;
        for (var i = 0; i < _options.Count; i++)
        {
            var option = _options[i];
            option.EnterScale ??= CreateValue(option);
            option.EnterFade ??= CreateValue(option);
            if (level == ArcMotionLevel.Off)
            {
                option.EnterScale.Jump(1);
                option.EnterFade.Jump(1);
                continue;
            }
            option.EnterFade.Jump(0);
            if (level == ArcMotionLevel.Full)
            {
                var delay = TimeSpan.FromSeconds(0.04 + i * ArcMotionPresets.StaggerItem.TotalSeconds);
                option.EnterScale.Jump(0.6);
                _ = option.EnterScale.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy, delay));
                _ = option.EnterFade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter, delay));
            }
            else
            {
                option.EnterScale.Jump(1);
                _ = option.EnterFade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            }
        }
        UpdateActive();
    }

    private MotionValue<double> CreateValue(ReactionsPickerOption option)
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += _ => ApplyEnter(option);
        return value;
    }

    private static void ApplyEnter(ReactionsPickerOption option)
    {
        var fade = Math.Clamp(option.EnterFade?.Value ?? 1, 0, 1);
        var scale = option.EnterScale?.Value ?? 1;
        option.Opacity = fade;
        option.EnterTransform.ScaleX = scale;
        option.EnterTransform.ScaleY = scale;
        option.Effect = scale < 0.999 && fade < 0.999 ? s_subtleBlur : null;
    }

    /// <summary>Focuses the roving option (the first one after opening).</summary>
    public void FocusActive(NavigationMethod method)
    {
        if (_focusIndex >= 0 && _focusIndex < _options.Count)
        {
            _options[_focusIndex].Focus(method);
        }
    }

    private void OnOptionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ReactionsPickerOption option)
        {
            e.Handled = true;
            Picked?.Invoke(this, option.Id);
        }
    }

    private void OnOptionGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is ReactionsPickerOption option)
        {
            _focusIndex = option.Index;
            UpdateActive();
        }
    }

    private void OnOptionPointerMoved(object? sender, PointerEventArgs e)
    {
        // Hover only follows a mouse; touch picks directly.
        if (sender is ReactionsPickerOption option && e.Pointer.Type == PointerType.Mouse && _hovered != option.Index)
        {
            _hovered = option.Index;
            UpdateActive();
        }
    }

    private void OnPanelPointerExited(object? sender, PointerEventArgs e)
    {
        _hovered = null;
        UpdateActive();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var count = _options.Count;
        if (count == 0 || e.Handled)
        {
            base.OnKeyDown(e);
            return;
        }
        int next = e.Key switch
        {
            Key.Right or Key.Down => (_focusIndex + 1) % count,
            Key.Left or Key.Up => (_focusIndex - 1 + count) % count,
            Key.Home => 0,
            Key.End => count - 1,
            _ => -1
        };
        if (e.Key is Key.Enter or Key.Space)
        {
            _keyboard = true;
        }
        if (next < 0)
        {
            base.OnKeyDown(e);
            return;
        }
        e.Handled = true;
        _hovered = null;
        _focusIndex = next;
        _options[next].Focus(NavigationMethod.Directional);
        UpdateActive();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        _keyboard = false;
        base.OnPointerPressed(e);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateActive();
    }

    private void UpdateActive()
    {
        var active = ActiveIndex;
        for (var i = 0; i < _options.Count; i++)
        {
            var option = _options[i];
            option.SetLit(i == active);
            // Roving focus: only the focused option is a Tab stop.
            option.IsTabStop = i == _focusIndex;
        }
        if (active < 0)
        {
            return;
        }
        var lit = _options[active];
        if (_highlight is not null && lit.Bounds.Width > 0)
        {
            // A new target retargets the running spring and keeps its velocity, so a fast sweep glides instead of stepping.
            _highlight.MoveTo(lit.Bounds, animate: _placed && IsLoaded);
            _placed = true;
        }
        if (_caption is not null && active < _source.Count)
        {
            var source = _source[active];
            _caption.Content = new ReactionsCaption(source.Label, _reacted.Contains(source.Id));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PickerPeer(this);

    private sealed class PickerPeer : ControlAutomationPeer
    {
        public PickerPeer(Control owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ToolBar;

        protected override bool IsControlElementCore() => true;

        protected override bool IsContentElementCore() => true;
    }
}
