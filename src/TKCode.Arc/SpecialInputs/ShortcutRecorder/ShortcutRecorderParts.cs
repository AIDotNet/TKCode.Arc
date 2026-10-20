using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The reset and clear buttons of a <see cref="ShortcutRecorder"/>: 32px, muted glyph, <c>SurfaceMuted</c> on hover,
/// press and keyboard focus (Arc draws no ring here). They pop in and out (opacity + scale .6 on the snappy spring,
/// exit 0.1s) and leave the layout only once the exit has finished.
/// </summary>
/// <remarks>
/// Code-driven presence (<see cref="MotionValue{T}"/>): a ControlTheme transition cannot keep the button in the
/// layout while it leaves and collapse it afterwards.
/// </remarks>
internal sealed class ShortcutRecorderActionButton : AvaloniaButton
{
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<ShortcutRecorderActionButton, Geometry?>(nameof(Glyph));

    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<ShortcutRecorderActionButton, double>(nameof(GlyphSize), 16);

    public static readonly StyledProperty<bool> IsShownProperty =
        AvaloniaProperty.Register<ShortcutRecorderActionButton, bool>(nameof(IsShown));

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }

    /// <summary>Arc's <c>canReset</c> / <c>canClear</c>: present (animated) or gone.</summary>
    public bool IsShown { get => GetValue(IsShownProperty); set => SetValue(IsShownProperty, value); }

    /// <summary>True when the last activation came from the keyboard (focus then returns with a visible ring).</summary>
    internal bool ActivatedByKeyboard { get; private set; }

    private static readonly Easing s_easeInOut = new SplineEasing(0.42, 0, 0.58, 1);
    private static readonly TimeSpan s_exit = TimeSpan.FromMilliseconds(100);
    private readonly ScaleTransform _scale = new();
    private MotionValue<double>? _presence;
    private bool _fadeOnly;

    public ShortcutRecorderActionButton()
    {
        RenderTransform = _scale;
        IsVisible = false;
    }

    protected override Type StyleKeyOverride => typeof(ShortcutRecorderActionButton);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        ActivatedByKeyboard = true;
        base.OnKeyDown(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        ActivatedByKeyboard = false;
        base.OnPointerPressed(e);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc's hover only answers fine pointers (@media (hover: hover) and (pointer: fine)).
        PseudoClasses.Set(ShortcutRecorder.HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(ShortcutRecorder.HoverPseudoClass, false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsShownProperty)
        {
            Run(change.GetNewValue<bool>());
        }
    }

    private void Run(bool shown)
    {
        var level = IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        _presence ??= CreatePresence(shown ? 0 : 1);
        IsHitTestVisible = shown;
        if (!shown)
        {
            PseudoClasses.Set(ShortcutRecorder.HoverPseudoClass, false);
        }
        if (shown)
        {
            IsVisible = true;
            _fadeOnly = false;
            // Reduced: Arc's enter transition is { duration: 0 }.
            if (level == ArcMotionLevel.Full)
            {
                if (_presence.Value >= 1 && !_presence.IsAnimating)
                {
                    _presence.Jump(0);
                }
                _ = _presence.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
            }
            else
            {
                _presence.Jump(1);
            }
            return;
        }
        if (level == ArcMotionLevel.Off)
        {
            _presence.Jump(0);
            IsVisible = false;
            return;
        }
        // Exit: opacity 0 and scale .6 in 0.1s; reduced motion keeps only the fade.
        _fadeOnly = level == ArcMotionLevel.Reduced;
        _ = HideWhenDone(_presence.AnimateTo(0, MotionSpec.Tween(s_exit, s_easeInOut)));
    }

    private async Task HideWhenDone(Task<bool> exit)
    {
        if (await exit && !IsShown)
        {
            IsVisible = false;
        }
    }

    private MotionValue<double> CreatePresence(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += Apply;
        return value;
    }

    private void Apply(double p)
    {
        Opacity = Math.Clamp(p, 0, 1);
        var scale = _fadeOnly ? 1 : 0.6 + 0.4 * p;
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
    }
}

/// <summary>
/// The text buttons of a conflict message: "Use anyway" (Foreground, weight 500, a <c>BorderStrong</c> underline 3px
/// under the baseline that takes the text colour on hover and keyboard focus) and the quiet "Cancel" (TextMuted, no
/// underline, Foreground on hover).
/// </summary>
internal sealed class ShortcutRecorderLink : AvaloniaButton
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ShortcutRecorderLink, string?>(nameof(Text));

    public static readonly StyledProperty<bool> IsQuietProperty =
        AvaloniaProperty.Register<ShortcutRecorderLink, bool>(nameof(IsQuiet));

    public static readonly StyledProperty<IBrush?> UnderlineBrushProperty =
        AvaloniaProperty.Register<ShortcutRecorderLink, IBrush?>(nameof(UnderlineBrush));

    public static readonly StyledProperty<double> UnderlineOffsetProperty =
        AvaloniaProperty.Register<ShortcutRecorderLink, double>(nameof(UnderlineOffset), 3);

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>The quiet link (Arc <c>data-quiet</c>).</summary>
    public bool IsQuiet { get => GetValue(IsQuietProperty); set => SetValue(IsQuietProperty, value); }

    public IBrush? UnderlineBrush { get => GetValue(UnderlineBrushProperty); set => SetValue(UnderlineBrushProperty, value); }

    public double UnderlineOffset { get => GetValue(UnderlineOffsetProperty); set => SetValue(UnderlineOffsetProperty, value); }

    /// <summary>True when the last activation came from the keyboard.</summary>
    internal bool ActivatedByKeyboard { get; private set; }

    private TextBlock? _text;

    protected override Type StyleKeyOverride => typeof(ShortcutRecorderLink);

    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _text = e.NameScope.Find<TextBlock>("PART_Text");
        UpdateUnderline();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        ActivatedByKeyboard = true;
        base.OnKeyDown(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        ActivatedByKeyboard = false;
        base.OnPointerPressed(e);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc's hover only answers fine pointers (@media (hover: hover) and (pointer: fine)).
        PseudoClasses.Set(ShortcutRecorder.HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(ShortcutRecorder.HoverPseudoClass, false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsQuietProperty || change.Property == UnderlineBrushProperty || change.Property == UnderlineOffsetProperty)
        {
            UpdateUnderline();
        }
    }

    private void UpdateUnderline()
    {
        if (_text is null)
        {
            return;
        }
        if (IsQuiet || UnderlineBrush is null)
        {
            _text.TextDecorations = null;
            return;
        }
        // text-underline-offset: 3px from the alphabetic baseline; the colour follows UnderlineBrush (BorderStrong at
        // rest, currentColor on hover / focus-visible, with the theme's colour transition).
        _text.TextDecorations = new TextDecorationCollection
        {
            new TextDecoration
            {
                Location = TextDecorationLocation.Baseline,
                Stroke = UnderlineBrush,
                StrokeThickness = 1,
                StrokeThicknessUnit = TextDecorationUnit.Pixel,
                StrokeOffset = UnderlineOffset,
                StrokeOffsetUnit = TextDecorationUnit.Pixel
            }
        };
    }
}

/// <summary>One message of the recorder (Arc's keyed message): its tone, copy and whether it carries the conflict actions.</summary>
internal sealed record ShortcutRecorderMessageModel(string Key, bool IsWarning, string Text, string? Strong, bool HasActions)
{
    /// <summary>The plain copy (also the recorder's help text).</summary>
    public string PlainText => Strong is null ? Text : $"{Text} {Strong}";
}

/// <summary>
/// The message row under a <see cref="ShortcutRecorder"/> (Arc's message slot). The slot opens and closes its height
/// on the smooth spring (opacity fast in, 0.1s out); a new message rises 0.35em out of a soft blur on standard + enter
/// ease while the old one fades in 0.08s where it stood. Reduced motion: the slot jumps and messages fade 0.1s; Off
/// jumps. <see cref="FieldMessage"/> cannot host the conflict buttons, so the row is built here.
/// </summary>
/// <remarks>
/// Code-driven (<see cref="MotionValue{T}"/>): keyed presence with a leaving copy outside the layout and a measured
/// height spring are not expressible as ControlTheme transitions.
/// </remarks>
internal sealed class ShortcutRecorderMessageSlot : Panel
{
    public static readonly StyledProperty<IBrush?> HintBrushProperty =
        AvaloniaProperty.Register<ShortcutRecorderMessageSlot, IBrush?>(nameof(HintBrush));

    public static readonly StyledProperty<IBrush?> WarningToneBrushProperty =
        AvaloniaProperty.Register<ShortcutRecorderMessageSlot, IBrush?>(nameof(WarningToneBrush));

    public static readonly StyledProperty<IBrush?> StrongBrushProperty =
        AvaloniaProperty.Register<ShortcutRecorderMessageSlot, IBrush?>(nameof(StrongBrush));

    public static readonly StyledProperty<IBrush?> WarningDotBrushProperty =
        AvaloniaProperty.Register<ShortcutRecorderMessageSlot, IBrush?>(nameof(WarningDotBrush));

    public static readonly StyledProperty<FontWeight> StrongWeightProperty =
        AvaloniaProperty.Register<ShortcutRecorderMessageSlot, FontWeight>(nameof(StrongWeight), FontWeight.Medium);

    public static readonly StyledProperty<Thickness> MessagePaddingProperty =
        AvaloniaProperty.Register<ShortcutRecorderMessageSlot, Thickness>(nameof(MessagePadding), new Thickness(0, 8, 0, 0));

    public static readonly StyledProperty<double> MessageLineHeightProperty =
        AvaloniaProperty.Register<ShortcutRecorderMessageSlot, double>(nameof(MessageLineHeight), 16.8);

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<ShortcutRecorderMessageSlot, double>(nameof(ColumnGap), 12);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<ShortcutRecorderMessageSlot, double>(nameof(RowGap), 2);

    public static readonly StyledProperty<double> WarningDotDiameterProperty =
        AvaloniaProperty.Register<ShortcutRecorderMessageSlot, double>(nameof(WarningDotDiameter), 6);

    public static readonly StyledProperty<Thickness> WarningDotMarginProperty =
        AvaloniaProperty.Register<ShortcutRecorderMessageSlot, Thickness>(nameof(WarningDotMargin), new Thickness(1, 0, 7, 1));

    public static readonly StyledProperty<double> UnderlineOffsetProperty =
        AvaloniaProperty.Register<ShortcutRecorderMessageSlot, double>(nameof(UnderlineOffset), 3);

    public IBrush? HintBrush { get => GetValue(HintBrushProperty); set => SetValue(HintBrushProperty, value); }
    public IBrush? WarningToneBrush { get => GetValue(WarningToneBrushProperty); set => SetValue(WarningToneBrushProperty, value); }
    public IBrush? StrongBrush { get => GetValue(StrongBrushProperty); set => SetValue(StrongBrushProperty, value); }
    public IBrush? WarningDotBrush { get => GetValue(WarningDotBrushProperty); set => SetValue(WarningDotBrushProperty, value); }
    public FontWeight StrongWeight { get => GetValue(StrongWeightProperty); set => SetValue(StrongWeightProperty, value); }
    public Thickness MessagePadding { get => GetValue(MessagePaddingProperty); set => SetValue(MessagePaddingProperty, value); }
    public double MessageLineHeight { get => GetValue(MessageLineHeightProperty); set => SetValue(MessageLineHeightProperty, value); }
    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }
    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }
    public double WarningDotDiameter { get => GetValue(WarningDotDiameterProperty); set => SetValue(WarningDotDiameterProperty, value); }
    public Thickness WarningDotMargin { get => GetValue(WarningDotMarginProperty); set => SetValue(WarningDotMarginProperty, value); }
    public double UnderlineOffset { get => GetValue(UnderlineOffsetProperty); set => SetValue(UnderlineOffsetProperty, value); }

    /// <summary>"Use anyway" was activated (the argument: activated from the keyboard).</summary>
    public event EventHandler<bool>? UseAnyway;

    /// <summary>"Cancel" was activated (the argument: activated from the keyboard).</summary>
    public event EventHandler<bool>? Cancel;

    /// <summary>Copy of the conflict actions.</summary>
    public string UseAnywayText { get; set; } = "Use anyway";

    /// <summary>Copy of the quiet conflict action.</summary>
    public string CancelText { get; set; } = "Cancel";

    /// <summary>The message shown (null while the row is closed or closing).</summary>
    internal ShortcutRecorderMessageModel? Current => _present ? _current?.Model : null;

    /// <summary>True while the row is open or opening.</summary>
    internal bool IsOpen => _present;

    /// <summary>The links of the current message, for tests.</summary>
    internal (ShortcutRecorderLink UseAnyway, ShortcutRecorderLink Cancel)? Links => _current?.Actions;

    private static readonly Easing s_easeInOut = new SplineEasing(0.42, 0, 0.58, 1);
    private static readonly TimeSpan s_slotExit = TimeSpan.FromMilliseconds(100);
    private ShortcutRecorderMessageView? _current;
    private bool _present;
    private bool _measuring;
    private MotionValue<double>? _height;
    private MotionValue<double>? _opacity;

    static ShortcutRecorderMessageSlot()
    {
        ClipToBoundsProperty.OverrideDefaultValue<ShortcutRecorderMessageSlot>(true);
        AffectsMeasure<ShortcutRecorderMessageSlot>(MessagePaddingProperty, MessageLineHeightProperty, ColumnGapProperty, RowGapProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == HintBrushProperty || property == WarningToneBrushProperty || property == StrongBrushProperty
            || property == WarningDotBrushProperty || property == StrongWeightProperty || property == MessagePaddingProperty
            || property == MessageLineHeightProperty || property == WarningDotDiameterProperty || property == WarningDotMarginProperty
            || property == UnderlineOffsetProperty)
        {
            foreach (var child in Children)
            {
                if (child is ShortcutRecorderMessageView view)
                {
                    view.Restyle();
                }
            }
        }
    }

    /// <summary>Shows <paramref name="model"/> (null closes the row).</summary>
    public void Show(ShortcutRecorderMessageModel? model)
    {
        var level = IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        if (model is null)
        {
            if (!_present)
            {
                return;
            }
            _present = false;
            if (level == ArcMotionLevel.Full)
            {
                // Exit: height to 0 on the smooth spring (in Measure), opacity in 0.1s; the copy stays until closed.
                EnsureOpacity();
                _ = _opacity!.AnimateTo(0, MotionSpec.Tween(s_slotExit, s_easeInOut));
                foreach (var child in Children)
                {
                    child.IsEnabled = false;
                }
            }
            else
            {
                ClearViews();
                _opacity?.Jump(1);
            }
            InvalidateMeasure();
            return;
        }

        if (!_present)
        {
            _present = true;
            var closed = _current is null || (_height?.Value ?? 0) < 0.5;
            if (_current is not null && !closed)
            {
                // Re-opened while closing: keep the copy (or swap it) and fade back.
                Swap(model, level);
                if (level == ArcMotionLevel.Full)
                {
                    EnsureOpacity();
                    _ = _opacity!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, s_easeInOut));
                }
                else
                {
                    _opacity?.Jump(1);
                }
            }
            else
            {
                // A fresh slot: the message arrives with it (the inner presence has initial={false}).
                ClearViews();
                _current = CreateView(model);
                if (level == ArcMotionLevel.Full)
                {
                    EnsureOpacity();
                    _opacity!.Jump(0);
                    _ = _opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, s_easeInOut));
                }
                else
                {
                    _opacity?.Jump(1);
                }
            }
            InvalidateMeasure();
            return;
        }

        Swap(model, level);
        InvalidateMeasure();
    }

    private void Swap(ShortcutRecorderMessageModel model, ArcMotionLevel level)
    {
        if (_current is not null && _current.Model.Key == model.Key)
        {
            _current.IsEnabled = true;
            _current.Update(model);
            return;
        }
        if (_current is not null)
        {
            var leaving = _current;
            if (level == ArcMotionLevel.Off)
            {
                Children.Remove(leaving);
            }
            else
            {
                leaving.Leave(() => Children.Remove(leaving));
            }
        }
        _current = CreateView(model);
        _current.Enter(level);
    }

    private ShortcutRecorderMessageView CreateView(ShortcutRecorderMessageModel model)
    {
        var view = new ShortcutRecorderMessageView(this, model);
        Children.Add(view);
        return view;
    }

    internal void RaiseUseAnyway(bool keyboard) => UseAnyway?.Invoke(this, keyboard);

    internal void RaiseCancel(bool keyboard) => Cancel?.Invoke(this, keyboard);

    private void ClearViews()
    {
        foreach (var child in Children)
        {
            if (child is ShortcutRecorderMessageView view)
            {
                view.Dispose();
            }
        }
        Children.Clear();
        _current = null;
    }

    private void EnsureOpacity()
    {
        if (_opacity is null)
        {
            _opacity = new MotionValue<double>(this, 1, MotionVectors.Double);
            _opacity.Changed += v => Opacity = Math.Clamp(v, 0, 1);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _measuring = true;
        try
        {
            var width = availableSize.Width;
            double content = 0;
            double natural = 0;
            foreach (var child in Children)
            {
                child.Measure(new Size(width, double.PositiveInfinity));
                if (ReferenceEquals(child, _current))
                {
                    content = child.DesiredSize.Height;
                    natural = child.DesiredSize.Width;
                }
            }
            var target = _present ? content : 0;
            if (_height is null)
            {
                _height = new MotionValue<double>(this, target, MotionVectors.Double);
                _height.Changed += _ =>
                {
                    if (!_measuring)
                    {
                        InvalidateMeasure();
                    }
                };
            }
            else if (Math.Abs(_height.Target - target) > 0.25)
            {
                if (IsLoaded && ArcMotion.IsFull(this))
                {
                    var task = _height.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
                    if (target <= 0)
                    {
                        _ = ClearWhenClosed(task);
                    }
                }
                else
                {
                    _height.Jump(target);
                    if (target <= 0)
                    {
                        ClearViews();
                    }
                }
            }
            return new Size(double.IsInfinity(width) ? natural : 0, Math.Max(0, _height.Value));
        }
        finally
        {
            _measuring = false;
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            // popLayout: every copy (the leaving one too) sits at the top of the slot.
            child.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(child.DesiredSize.Height, finalSize.Height)));
        }
        return finalSize;
    }

    private async Task ClearWhenClosed(Task<bool> closing)
    {
        if (await closing && !_present)
        {
            ClearViews();
            _opacity?.Jump(1);
            InvalidateMeasure();
        }
    }
}

/// <summary>
/// One keyed message: the copy (with the warning dot and the strong label for a conflict) and, for a conflict, the
/// "Use anyway" / "Cancel" links. Lays out like Arc's <c>flex-wrap</c> row: copy left and links right when both fit,
/// otherwise the links wrap under the copy.
/// </summary>
internal sealed class ShortcutRecorderMessageView : Panel, IDisposable
{
    private readonly ShortcutRecorderMessageSlot _slot;
    private readonly TextBlock _text;
    private readonly StackPanel? _actionsPanel;
    private readonly TranslateTransform _rise = new();
    private MotionValue<double>? _enter;
    private MotionValue<double>? _exit;
    private bool _fadeOnly;
    private double _exitFrom = 1;
    private bool _singleRow;
    private Run? _strongRun;
    private Ellipse? _dot;

    public ShortcutRecorderMessageView(ShortcutRecorderMessageSlot slot, ShortcutRecorderMessageModel model)
    {
        _slot = slot;
        Model = model;
        RenderTransform = _rise;
        _text = new TextBlock { TextWrapping = TextWrapping.Wrap };
        Children.Add(_text);
        if (model.HasActions)
        {
            var useAnyway = new ShortcutRecorderLink { Text = slot.UseAnywayText };
            var cancel = new ShortcutRecorderLink { Text = slot.CancelText, IsQuiet = true };
            useAnyway.Click += (_, _) => _slot.RaiseUseAnyway(useAnyway.ActivatedByKeyboard);
            cancel.Click += (_, _) => _slot.RaiseCancel(cancel.ActivatedByKeyboard);
            _actionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Children = { useAnyway, cancel } };
            Children.Add(_actionsPanel);
            Actions = (useAnyway, cancel);
        }
        Restyle();
    }

    public ShortcutRecorderMessageModel Model { get; private set; }

    public (ShortcutRecorderLink UseAnyway, ShortcutRecorderLink Cancel)? Actions { get; }

    /// <summary>Same key: the copy changes in place (no motion), as React re-renders the same element.</summary>
    public void Update(ShortcutRecorderMessageModel model)
    {
        Model = model;
        Restyle();
    }

    /// <summary>Applies the slot's typography and colours to the copy.</summary>
    public void Restyle()
    {
        var model = Model;
        _text.LineHeight = _slot.MessageLineHeight;
        _text.Foreground = model.IsWarning ? _slot.WarningToneBrush : _slot.HintBrush;
        if (_actionsPanel is not null)
        {
            _actionsPanel.Spacing = _slot.ColumnGap;
            foreach (var child in _actionsPanel.Children)
            {
                if (child is ShortcutRecorderLink link)
                {
                    link.UnderlineOffset = _slot.UnderlineOffset;
                }
            }
        }
        if (!model.IsWarning && model.Strong is null)
        {
            _text.Inlines = null;
            _text.Text = model.Text;
            _strongRun = null;
            _dot = null;
            InvalidateMeasure();
            return;
        }
        var inlines = new InlineCollection();
        if (model.IsWarning)
        {
            var d = _slot.WarningDotDiameter;
            _dot = new Ellipse { Width = d, Height = d, Fill = _slot.WarningDotBrush, Margin = _slot.WarningDotMargin };
            inlines.Add(new InlineUIContainer(_dot) { BaselineAlignment = BaselineAlignment.Center });
        }
        inlines.Add(new Run(model.Strong is null ? model.Text : model.Text + " "));
        if (model.Strong is not null)
        {
            _strongRun = new Run(model.Strong) { Foreground = _slot.StrongBrush, FontWeight = _slot.StrongWeight };
            inlines.Add(_strongRun);
        }
        _text.Text = null;
        _text.Inlines = inlines;
        InvalidateMeasure();
    }

    /// <summary>Rises 0.35em out of a soft blur (standard + enter ease); reduced motion fades in 0.1s.</summary>
    public void Enter(ArcMotionLevel level)
    {
        if (level == ArcMotionLevel.Off)
        {
            return;
        }
        _fadeOnly = level == ArcMotionLevel.Reduced;
        _enter = new MotionValue<double>(this, 0, MotionVectors.Double);
        _enter.Changed += ApplyEnter;
        ApplyEnter(0);
        _ = _enter.AnimateTo(1, _fadeOnly
            ? MotionSpec.Tween(TimeSpan.FromMilliseconds(100), ArcEasings.Enter)
            : MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
    }

    /// <summary>Fades out in 0.08s where it stands, then calls <paramref name="removed"/>.</summary>
    public void Leave(Action removed)
    {
        IsEnabled = false;
        IsHitTestVisible = false;
        _exitFrom = Opacity;
        _enter?.Stop();
        _exit = new MotionValue<double>(this, 0, MotionVectors.Double);
        _exit.Changed += q => Opacity = _exitFrom * (1 - Math.Clamp(q, 0, 1));
        _ = RemoveWhenDone(_exit.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(80), new SplineEasing(0.42, 0, 0.58, 1))), removed);
    }

    private async Task RemoveWhenDone(Task<bool> exit, Action removed)
    {
        await exit;
        Dispose();
        removed();
    }

    private void ApplyEnter(double p)
    {
        p = Math.Clamp(p, 0, 1);
        Opacity = p;
        if (_fadeOnly)
        {
            return;
        }
        var em = _text.FontSize;
        _rise.Y = 0.35 * em * (1 - p);
        Effect = ArcBlur.Get(4 * (1 - p));
    }

    public void Dispose()
    {
        _enter?.Dispose();
        _exit?.Dispose();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = _slot.MessagePadding;
        var width = Math.Max(0, availableSize.Width - padding.Left - padding.Right);
        double height;
        double natural;
        if (_actionsPanel is null)
        {
            _text.Measure(new Size(width, double.PositiveInfinity));
            height = _text.DesiredSize.Height;
            natural = _text.DesiredSize.Width;
            _singleRow = true;
        }
        else
        {
            _text.Measure(Size.Infinity);
            _actionsPanel.Measure(Size.Infinity);
            var textWidth = _text.DesiredSize.Width;
            var actionsWidth = _actionsPanel.DesiredSize.Width;
            natural = textWidth + _slot.ColumnGap + actionsWidth;
            _singleRow = natural <= width + 0.01;
            if (_singleRow)
            {
                height = Math.Max(_text.DesiredSize.Height, _actionsPanel.DesiredSize.Height);
            }
            else
            {
                _text.Measure(new Size(width, double.PositiveInfinity));
                height = _text.DesiredSize.Height + _slot.RowGap + _actionsPanel.DesiredSize.Height;
            }
        }
        var desiredWidth = double.IsInfinity(availableSize.Width) ? natural + padding.Left + padding.Right : availableSize.Width;
        return new Size(desiredWidth, padding.Top + height + padding.Bottom);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var padding = _slot.MessagePadding;
        var width = Math.Max(0, finalSize.Width - padding.Left - padding.Right);
        var top = padding.Top;
        if (_actionsPanel is null)
        {
            _text.Arrange(new Rect(padding.Left, top, width, _text.DesiredSize.Height));
            return finalSize;
        }
        if (_singleRow)
        {
            // justify-content: space-between; align-items: baseline (both lines share the 12px / 1.4 metrics).
            _text.Arrange(new Rect(padding.Left, top, _text.DesiredSize.Width, _text.DesiredSize.Height));
            var actions = _actionsPanel.DesiredSize;
            _actionsPanel.Arrange(new Rect(padding.Left + width - actions.Width, top, actions.Width, actions.Height));
        }
        else
        {
            _text.Arrange(new Rect(padding.Left, top, width, _text.DesiredSize.Height));
            var actions = _actionsPanel.DesiredSize;
            _actionsPanel.Arrange(new Rect(padding.Left, top + _text.DesiredSize.Height + _slot.RowGap, actions.Width, actions.Height));
        }
        return finalSize;
    }
}
