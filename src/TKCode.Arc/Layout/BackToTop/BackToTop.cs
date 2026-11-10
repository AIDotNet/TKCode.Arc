using TKCode.Arc.Localization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// A round button that appears once a long list, log or feed has scrolled past <see cref="Threshold"/> and smoothly
/// scrolls it back to the top when pressed. Place it yourself over the scroll container (usually in the same Grid cell)
/// and point <see cref="Target"/> at it, or set <c>BackToTop.IsAttached="True"</c> on the container to have the button
/// placed in a corner of its viewport for you.
/// </summary>
[TemplatePart(ButtonPart, typeof(Control))]
[TemplatePart(PresenterPart, typeof(Control))]
[PseudoClasses(ShownPseudoClass, SmallPseudoClass, LargePseudoClass)]
public class BackToTop : TemplatedControl
{
    public const string ButtonPart = "PART_Button";
    public const string PresenterPart = "PART_Presenter";

    /// <summary>Set while <see cref="IsShown"/> is true.</summary>
    public const string ShownPseudoClass = ":shown";

    /// <summary><see cref="Size"/> Small or Large as a pseudo-class (Medium has none).</summary>
    public const string SmallPseudoClass = ":small";

    /// <inheritdoc cref="SmallPseudoClass"/>
    public const string LargePseudoClass = ":large";

    #region 公共属性定义

    /// <summary>The scroll container to bring back to the top: a ScrollViewer (ScrollArea is one), or a control whose template holds one (Timeline, Table, TreeView, a templated ListBox).</summary>
    public static readonly StyledProperty<Control?> TargetProperty =
        AvaloniaProperty.Register<BackToTop, Control?>(nameof(Target));

    /// <summary>
    /// How far, in pixels, the content must be scrolled down before the button appears (default 200; negative counts as
    /// 0). Also an attached property for a container with <c>BackToTop.IsAttached</c>.
    /// </summary>
    public static readonly AttachedProperty<double> ThresholdProperty =
        AvaloniaProperty.RegisterAttached<BackToTop, Control, double>("Threshold", 200);

    /// <summary>Whether the button is showing: the target can scroll and is scrolled further down than <see cref="Threshold"/>.</summary>
    public static readonly DirectProperty<BackToTop, bool> IsShownProperty =
        AvaloniaProperty.RegisterDirect<BackToTop, bool>(nameof(IsShown), o => o.IsShown);

    /// <summary>The button size: Small 36, Medium 44 (default) or Large 50 pixels across.</summary>
    public static readonly StyledProperty<ControlSize> SizeProperty =
        AvaloniaProperty.Register<BackToTop, ControlSize>(nameof(Size), ControlSize.Medium);

    /// <summary>The accessible name and tooltip; null uses the localized "Back to top".</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<BackToTop, string?>(nameof(Label));

    /// <summary>
    /// Attached to a scroll container: adds a back-to-top button in a corner of its viewport (in the container's adorner
    /// layer, so it does not scroll with the content).
    /// </summary>
    public static readonly AttachedProperty<bool> IsAttachedProperty =
        AvaloniaProperty.RegisterAttached<BackToTop, Control, bool>("IsAttached");

    /// <summary>Attached to a scroll container with <c>BackToTop.IsAttached</c>: the viewport corner the button sits in.</summary>
    public static readonly AttachedProperty<BackToTopPlacement> PlacementProperty =
        AvaloniaProperty.RegisterAttached<BackToTop, Control, BackToTopPlacement>("Placement");

    /// <summary>Attached to a scroll container with <c>BackToTop.IsAttached</c>: the distance from the viewport's edges (default 16).</summary>
    public static readonly AttachedProperty<Thickness> InsetProperty =
        AvaloniaProperty.RegisterAttached<BackToTop, Control, Thickness>("Inset", new Thickness(16));

    /// <summary>Raised when a smooth scroll started by the button or <see cref="ScrollToTop"/> has reached the top; not raised when the user interrupts it.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ScrolledToTopEvent =
        RoutedEvent.Register<BackToTop, RoutedEventArgs>(nameof(ScrolledToTop), RoutingStrategies.Bubble);

    /// <inheritdoc cref="TargetProperty"/>
    public Control? Target { get => GetValue(TargetProperty); set => SetValue(TargetProperty, value); }

    /// <inheritdoc cref="ThresholdProperty"/>
    public double Threshold { get => GetValue(ThresholdProperty); set => SetValue(ThresholdProperty, value); }

    /// <inheritdoc cref="IsShownProperty"/>
    public bool IsShown { get => _isShown; private set => SetAndRaise(IsShownProperty, ref _isShown, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public ControlSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="ScrolledToTopEvent"/>
    public event EventHandler<RoutedEventArgs>? ScrolledToTop
    {
        add => AddHandler(ScrolledToTopEvent, value);
        remove => RemoveHandler(ScrolledToTopEvent, value);
    }

    /// <summary>Whether <paramref name="element"/> has an automatically placed back-to-top button.</summary>
    public static bool GetIsAttached(Control element) => element.GetValue(IsAttachedProperty);

    /// <summary>Adds (true) or removes (false) the automatically placed back-to-top button of <paramref name="element"/>.</summary>
    public static void SetIsAttached(Control element, bool value) => element.SetValue(IsAttachedProperty, value);

    /// <summary>The threshold of <paramref name="element"/>'s attached button.</summary>
    public static double GetThreshold(Control element) => element.GetValue(ThresholdProperty);

    /// <summary>Sets the threshold of <paramref name="element"/>'s attached button.</summary>
    public static void SetThreshold(Control element, double value) => element.SetValue(ThresholdProperty, value);

    /// <summary>The viewport corner of <paramref name="element"/>'s attached button.</summary>
    public static BackToTopPlacement GetPlacement(Control element) => element.GetValue(PlacementProperty);

    /// <summary>Sets the viewport corner of <paramref name="element"/>'s attached button.</summary>
    public static void SetPlacement(Control element, BackToTopPlacement value) => element.SetValue(PlacementProperty, value);

    /// <summary>The distance of <paramref name="element"/>'s attached button from the viewport's edges.</summary>
    public static Thickness GetInset(Control element) => element.GetValue(InsetProperty);

    /// <summary>Sets the distance of <paramref name="element"/>'s attached button from the viewport's edges.</summary>
    public static void SetInset(Control element, Thickness value) => element.SetValue(InsetProperty, value);

    #endregion

    /// <summary>The attached button of a host (<see cref="IsAttachedProperty"/>).</summary>
    private static readonly AttachedProperty<BackToTopAdorner?> AdornerProperty =
        AvaloniaProperty.RegisterAttached<BackToTop, Control, BackToTopAdorner?>("Adorner");

    private bool _isShown;
    private bool _inTree;
    private BackToTopButton? _button;
    private Control? _hookedTarget;
    private ScrollViewer? _scrollViewer;
    private MotionValue<double>? _scroll;
    private double _written;
    private IDisposable? _nameBinding;
    private IDisposable? _tipBinding;

    static BackToTop()
    {
        IsAttachedProperty.Changed.AddClassHandler<Control>((host, e) => OnIsAttachedChanged(host, e.GetNewValue<bool>()));
        PlacementProperty.Changed.AddClassHandler<Control>((host, _) => host.GetValue(AdornerProperty)?.InvalidateArrange());
        InsetProperty.Changed.AddClassHandler<Control>((host, _) => host.GetValue(AdornerProperty)?.InvalidateArrange());
    }

    public BackToTop()
    {
        UpdateSizePseudoClasses();
    }

    /// <summary>The ScrollViewer found in <see cref="Target"/>, while the button is in a visual tree.</summary>
    internal ScrollViewer? Viewer => _scrollViewer;

    /// <summary>Raised when the watched ScrollViewer changes or its viewport moves (the adorner places the button by it).</summary>
    internal event Action? ViewportChanged;

    /// <summary>Smoothly scrolls the target to the top, as pressing the button does (immediately under reduced motion or Off).</summary>
    public void ScrollToTop() => _ = ScrollToTopAsync();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_button is not null)
        {
            _button.Click -= OnButtonClick;
        }
        _nameBinding?.Dispose();
        _tipBinding?.Dispose();
        _button = e.NameScope.Find<BackToTopButton>(ButtonPart);
        if (_button is not null)
        {
            _button.Click += OnButtonClick;
        }
        UpdateLabel();
        UpdateButtonState();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _inTree = true;
        HookTarget(Target);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _inTree = false;
        _scroll?.Stop();
        HookTarget(null);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TargetProperty)
        {
            if (_inTree)
            {
                HookTarget(Target);
            }
        }
        else if (change.Property == ThresholdProperty)
        {
            UpdateShown();
        }
        else if (change.Property == SizeProperty)
        {
            UpdateSizePseudoClasses();
        }
        else if (change.Property == LabelProperty)
        {
            UpdateLabel();
        }
        else if (change.Property == IsShownProperty)
        {
            PseudoClasses.Set(ShownPseudoClass, IsShown);
            UpdateButtonState();
        }
    }

    private void UpdateSizePseudoClasses()
    {
        var size = Size;
        PseudoClasses.Set(SmallPseudoClass, size == ControlSize.Small);
        PseudoClasses.Set(LargePseudoClass, size == ControlSize.Large);
    }

    /// <summary>Names the button: <see cref="Label"/>, or the localized "Back to top" (which follows the language).</summary>
    private void UpdateLabel()
    {
        _nameBinding?.Dispose();
        _tipBinding?.Dispose();
        _nameBinding = null;
        _tipBinding = null;
        if (_button is null)
        {
            return;
        }
        if (Label is { } label)
        {
            AutomationProperties.SetName(_button, label);
            Tooltip.SetTip(_button, label);
            return;
        }
        var text = _button.GetResourceObservable(BackToTopLangResources.Key(BackToTopLangResourceKind.BackToTop));
        _nameBinding = _button.Bind(AutomationProperties.NameProperty, text);
        _tipBinding = _button.Bind(Tooltip.TipProperty, text);
    }

    /// <summary>A hidden button takes no pointer input and is not in the Tab order.</summary>
    private void UpdateButtonState()
    {
        if (_button is null)
        {
            return;
        }
        _button.IsHitTestVisible = IsShown;
        _button.Focusable = IsShown;
        _button.IsTabStop = IsShown;
    }

    private void HookTarget(Control? target)
    {
        if (_hookedTarget is TemplatedControl previous)
        {
            previous.TemplateApplied -= OnTargetTemplateApplied;
            previous.Loaded -= OnTargetLoaded;
        }
        _hookedTarget = target;
        if (target is TemplatedControl templated and not ScrollViewer)
        {
            // The ScrollViewer of a ListBox or DataGrid exists only once its template is applied, which can be later.
            templated.TemplateApplied += OnTargetTemplateApplied;
            templated.Loaded += OnTargetLoaded;
        }
        FindScrollViewer();
    }

    private void OnTargetTemplateApplied(object? sender, TemplateAppliedEventArgs e) => FindScrollViewer();

    private void OnTargetLoaded(object? sender, RoutedEventArgs e) => FindScrollViewer();

    private void FindScrollViewer()
    {
        var target = _hookedTarget;
        var found = target switch
        {
            null => null,
            ScrollViewer viewer => viewer,
            _ => target.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault()
        };
        if (found == _scrollViewer)
        {
            UpdateShown();
            return;
        }
        if (_scrollViewer is not null)
        {
            _scrollViewer.ScrollChanged -= OnScrollChanged;
        }
        _scroll?.Stop();
        _scrollViewer = found;
        if (found is not null)
        {
            found.ScrollChanged += OnScrollChanged;
        }
        UpdateShown();
        ViewportChanged?.Invoke();
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        // The user scrolled (wheel, scroll bar, keys) while the button was scrolling: stop and leave the offset to them.
        if (_scroll is { IsAnimating: true } && sender is ScrollViewer viewer && Math.Abs(viewer.Offset.Y - _written) > 0.5)
        {
            _scroll.Stop();
        }
        UpdateShown();
        if (e.ViewportDelta != default)
        {
            ViewportChanged?.Invoke();
        }
    }

    private void UpdateShown()
    {
        var viewer = _scrollViewer;
        IsShown = viewer is not null
                  && viewer.Extent.Height > viewer.Viewport.Height + 0.5
                  && viewer.Offset.Y > Math.Max(0, Threshold);
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e) => ScrollToTop();

    private async Task ScrollToTopAsync()
    {
        var viewer = _scrollViewer;
        if (viewer is null)
        {
            return;
        }
        var from = viewer.Offset.Y;
        if (from <= 0.5 || !ArcMotion.IsFull(this))
        {
            _scroll?.Stop();
            SetOffsetY(viewer, 0);
            RaiseEvent(new RoutedEventArgs(ScrolledToTopEvent, this));
            return;
        }
        // Code-driven: a ScrollViewer's Offset is not a property a ControlTheme transition can animate.
        if (_scroll is null)
        {
            _scroll = new MotionValue<double>(this, from, MotionVectors.Double);
            _scroll.Changed += y =>
            {
                if (_scrollViewer is { } current)
                {
                    SetOffsetY(current, y);
                }
            };
        }
        if (!_scroll.IsAnimating)
        {
            _written = from;
            _scroll.Jump(from);
        }
        var reached = await _scroll.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Considered, ArcEasings.InOut));
        if (reached && _scrollViewer == viewer && viewer.Offset.Y <= 0.5)
        {
            RaiseEvent(new RoutedEventArgs(ScrolledToTopEvent, this));
        }
    }

    private void SetOffsetY(ScrollViewer viewer, double y)
    {
        _written = y;
        viewer.Offset = new Vector(viewer.Offset.X, y);
    }

    private static void OnIsAttachedChanged(Control host, bool attached)
    {
        if (host.GetValue(AdornerProperty) is { } existing)
        {
            AdornerLayer.SetAdorner(host, null);
            existing.Release();
            host.ClearValue(AdornerProperty);
        }
        if (!attached)
        {
            return;
        }
        var adorner = new BackToTopAdorner(host);
        host.SetValue(AdornerProperty, adorner);
        AdornerLayer.SetAdorner(host, adorner);
    }

    /// <summary>The automatically placed button of <paramref name="host"/>, or null (tests).</summary>
    internal static BackToTop? AttachedButtonOf(Control host) => host.GetValue(AdornerProperty)?.Button;
}
