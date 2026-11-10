using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Interactivity;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>Data for <see cref="Sticky.StuckChangedEvent"/>: whether the element is now stuck.</summary>
public sealed class StickyStuckChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data.</summary>
    public StickyStuckChangedEventArgs(RoutedEvent routedEvent, bool isStuck)
        : base(routedEvent)
    {
        IsStuck = isStuck;
    }

    /// <summary>True when the element has just become stuck, false when it has just been released.</summary>
    public bool IsStuck { get; }
}

/// <summary>
/// Keeps an element in scrolling content at the top of the viewport once it scrolls there, like CSS
/// <c>position: sticky; top: N</c>: <c>arc:Sticky.IsEnabled="True"</c> (and optionally <c>arc:Sticky.Offset</c>).
/// The element moves with a render translation only, so the content's layout and height never change, and it never
/// leaves its parent: when the parent's bottom edge reaches it, it scrolls away with the parent, so the next sticky
/// header in a grouped list pushes the previous one out. While stuck, <c>arc:Sticky.IsStuck</c> is true (select it with
/// <c>[(arc|Sticky.IsStuck)=True]</c> to add a shadow or background) and the element's ZIndex is raised to
/// <c>arc:Sticky.ZIndex</c>. Only vertical scrolling is followed. The element must not have a RenderTransform of its own.
/// </summary>
public static class Sticky
{
    /// <summary>Makes the element stick to the top of its scrolling viewport; off by default.</summary>
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsEnabled", typeof(Sticky));

    /// <summary>Distance in pixels between the viewport's top edge and the stuck element; default 0.</summary>
    public static readonly AttachedProperty<double> OffsetProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("Offset", typeof(Sticky));

    /// <summary>The element's ZIndex while it is stuck, so it draws over the content scrolling under it; default 1.</summary>
    public static readonly AttachedProperty<int> ZIndexProperty =
        AvaloniaProperty.RegisterAttached<Control, int>("ZIndex", typeof(Sticky), 1);

    /// <summary>True while the element is held at the top of the viewport (read-only; style it with <c>[(arc|Sticky.IsStuck)=True]</c>).</summary>
    public static readonly AttachedProperty<bool> IsStuckProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsStuck", typeof(Sticky));

    /// <summary>Raised on the element (bubbling) when <c>IsStuck</c> changes.</summary>
    public static readonly RoutedEvent<StickyStuckChangedEventArgs> StuckChangedEvent =
        RoutedEvent.Register<StickyStuckChangedEventArgs>("StuckChanged", RoutingStrategies.Bubble, typeof(Sticky));

    private static readonly AttachedProperty<Tracker?> TrackerProperty =
        AvaloniaProperty.RegisterAttached<Control, Tracker?>("Tracker", typeof(Sticky));

    static Sticky()
    {
        IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
        OffsetProperty.Changed.AddClassHandler<Control>((element, _) => element.GetValue(TrackerProperty)?.Update());
        ZIndexProperty.Changed.AddClassHandler<Control>((element, _) => element.GetValue(TrackerProperty)?.Update());
    }

    /// <summary>Whether <paramref name="element"/> sticks to the top of its viewport.</summary>
    public static bool GetIsEnabled(Control element) => element.GetValue(IsEnabledProperty);

    /// <summary>Turns sticking on or off for <paramref name="element"/>.</summary>
    public static void SetIsEnabled(Control element, bool value) => element.SetValue(IsEnabledProperty, value);

    /// <summary>The gap between the viewport's top edge and <paramref name="element"/> while it is stuck.</summary>
    public static double GetOffset(Control element) => element.GetValue(OffsetProperty);

    /// <summary>Sets the gap between the viewport's top edge and <paramref name="element"/> while it is stuck.</summary>
    public static void SetOffset(Control element, double value) => element.SetValue(OffsetProperty, value);

    /// <summary>The ZIndex <paramref name="element"/> takes while it is stuck.</summary>
    public static int GetZIndex(Control element) => element.GetValue(ZIndexProperty);

    /// <summary>Sets the ZIndex <paramref name="element"/> takes while it is stuck.</summary>
    public static void SetZIndex(Control element, int value) => element.SetValue(ZIndexProperty, value);

    /// <summary>Whether <paramref name="element"/> is currently held at the top of its viewport.</summary>
    public static bool GetIsStuck(Control element) => element.GetValue(IsStuckProperty);

    private static void SetIsStuck(Control element, bool value) => element.SetValue(IsStuckProperty, value);

    /// <summary>Adds a handler for <see cref="StuckChangedEvent"/> on <paramref name="element"/>.</summary>
    public static void AddStuckChangedHandler(Control element, EventHandler<StickyStuckChangedEventArgs> handler)
        => element.AddHandler(StuckChangedEvent, handler);

    /// <summary>Removes a handler for <see cref="StuckChangedEvent"/> from <paramref name="element"/>.</summary>
    public static void RemoveStuckChangedHandler(Control element, EventHandler<StickyStuckChangedEventArgs> handler)
        => element.RemoveHandler(StuckChangedEvent, handler);

    private static void OnIsEnabledChanged(Control element, AvaloniaPropertyChangedEventArgs e)
    {
        element.GetValue(TrackerProperty)?.Detach();
        element.ClearValue(TrackerProperty);
        if (e.NewValue is not true)
        {
            return;
        }
        if (element.RenderTransform is not null)
        {
            // Composing with the element's own transform is not supported (yet); leave it as it is rather than replace it.
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(element,
                "Sticky.IsEnabled has no effect on {Element}: the element already has a RenderTransform.", element);
            return;
        }
        var tracker = new Tracker(element);
        element.SetValue(TrackerProperty, tracker);
        tracker.Attach();
    }

    /// <summary>
    /// Per-element state: the viewport Avalonia last reported, the translation and the saved ZIndex. The subscriptions
    /// are on the element itself (and its visual parent's bounds), so they go away with it.
    /// </summary>
    private sealed class Tracker
    {
        private readonly Control _element;
        private readonly TranslateTransform _translate = new();
        private Visual? _parent;
        private Rect _viewport;
        private bool _hasViewport;
        private bool _raised;
        private int _savedZIndex;
        private bool _savedZIndexWasLocal;

        public Tracker(Control element)
        {
            _element = element;
        }

        public void Attach()
        {
            _element.RenderTransform = _translate;
            // Avalonia records the viewport when a listener subscribes and then reports only changes, so an element
            // that is already laid out reads its first viewport here (implementation guide section 13).
            _element.EffectiveViewportChanged += OnEffectiveViewportChanged;
            _element.PropertyChanged += OnElementPropertyChanged;
            _element.AttachedToVisualTree += OnAttachedToVisualTree;
            _element.DetachedFromVisualTree += OnDetachedFromVisualTree;
            WatchParent();
            Recalculate();
        }

        public void Detach()
        {
            _element.EffectiveViewportChanged -= OnEffectiveViewportChanged;
            _element.PropertyChanged -= OnElementPropertyChanged;
            _element.AttachedToVisualTree -= OnAttachedToVisualTree;
            _element.DetachedFromVisualTree -= OnDetachedFromVisualTree;
            UnwatchParent();
            if (ReferenceEquals(_element.RenderTransform, _translate))
            {
                _element.ClearValue(Visual.RenderTransformProperty);
            }
            _translate.Y = 0;
            SetStuck(false);
        }

        /// <summary>Recomputes the translation from the last reported viewport.</summary>
        public void Update()
        {
            if (!_hasViewport || !ReferenceEquals(_element.RenderTransform, _translate))
            {
                return;
            }
            var parentHeight = _parent?.Bounds.Height ?? 0;
            var limit = Math.Max(0, parentHeight - _element.Bounds.Bottom);
            var dy = Math.Clamp(_viewport.Top + GetOffset(_element), 0, limit);
            _translate.Y = dy;
            SetStuck(dy > 0);
        }

        private void Recalculate()
        {
            _hasViewport = _element.IsAttachedToVisualTree();
            _viewport = _hasViewport ? ArcViewport.Calculate(_element) : default;
            Update();
        }

        private void OnEffectiveViewportChanged(object? sender, Avalonia.Layout.EffectiveViewportChangedEventArgs e)
        {
            _viewport = e.EffectiveViewport;
            _hasViewport = true;
            Update();
        }

        private void OnElementPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            // The viewport is reported when the element moves; a change of its own height or of its parent's only
            // changes how far it may travel.
            if (e.Property == Visual.BoundsProperty)
            {
                Update();
            }
        }

        private void OnParentPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Visual.BoundsProperty)
            {
                Update();
            }
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            WatchParent();
            // A re-attached element may keep its old bounds, and then no viewport change is ever reported.
            Dispatcher.UIThread.Post(() =>
            {
                if (ReferenceEquals(_element.GetValue(TrackerProperty), this))
                {
                    Recalculate();
                }
            }, DispatcherPriority.Loaded);
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            UnwatchParent();
            _hasViewport = false;
        }

        private void WatchParent()
        {
            UnwatchParent();
            _parent = _element.GetVisualParent();
            if (_parent is not null)
            {
                _parent.PropertyChanged += OnParentPropertyChanged;
            }
        }

        private void UnwatchParent()
        {
            if (_parent is not null)
            {
                _parent.PropertyChanged -= OnParentPropertyChanged;
                _parent = null;
            }
        }

        private void SetStuck(bool stuck)
        {
            if (stuck)
            {
                RaiseZIndex();
            }
            else
            {
                RestoreZIndex();
            }
            if (GetIsStuck(_element) == stuck)
            {
                return;
            }
            SetIsStuck(_element, stuck);
            _element.RaiseEvent(new StickyStuckChangedEventArgs(StuckChangedEvent, stuck));
        }

        private void RaiseZIndex()
        {
            if (!_raised)
            {
                _savedZIndex = _element.ZIndex;
                _savedZIndexWasLocal = _element.GetDiagnostic(Visual.ZIndexProperty).Priority == BindingPriority.LocalValue;
                _raised = true;
            }
            var target = GetZIndex(_element);
            if (_element.ZIndex != target)
            {
                _element.SetCurrentValue(Visual.ZIndexProperty, target);
            }
        }

        private void RestoreZIndex()
        {
            if (!_raised)
            {
                return;
            }
            _raised = false;
            if (_savedZIndexWasLocal)
            {
                _element.SetValue(Visual.ZIndexProperty, _savedZIndex);
            }
            else
            {
                _element.ClearValue(Visual.ZIndexProperty);
            }
        }
    }
}
