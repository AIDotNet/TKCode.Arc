using System.Collections.Specialized;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>One pill of a <see cref="PillFilterBar"/>: the key it selects and its visible label. Key null is the "All" pill.</summary>
/// <param name="Key">The key <see cref="PillFilterBar.SelectedKey"/> takes when the pill is chosen; null for "All".</param>
/// <param name="Label">The visible (and accessible) label.</param>
internal sealed record PillFilterOption(string? Key, string Label);

/// <summary>
/// A row of text pills that filters a list (the memory groups of <see cref="AgentMemory"/>): one selected background
/// glides between the pills on the quick spring, only the selected pill is a tab stop, and Left / Right / Home / End move
/// and select at once (wrapping). When the pills are wider than the bar the row scrolls sideways without a scrollbar,
/// fades its edges only on the side with more to see, and keeps the selected pill in view with the least movement.
/// <see cref="IsConcealed"/> fades the row out so a search box can cover it.
/// </summary>
/// <remarks>
/// The pills are rebuilt (reusing their controls) when <see cref="Options"/> is replaced or, for a list that reports
/// its changes, when its content changes. A <see cref="SelectedKey"/> that matches no option falls back to the "All"
/// option (or the first one) once the current dispatcher job has finished, so a list that is cleared and refilled in
/// one go keeps its selection. <see cref="SelectionChanged"/> is raised only for the person's choices (pointer or keys),
/// never for code that sets <see cref="SelectedKey"/> or for that fallback.
/// </remarks>
[TemplatePart(ScrollViewerPart, typeof(ScrollViewer))]
[TemplatePart(TrackPart, typeof(PillFilterTrack))]
[TemplatePart(SelectionPart, typeof(GlidingIndicator))]
[PseudoClasses(ScrollableStartPseudoClass, ScrollableEndPseudoClass, ConcealedPseudoClass)]
internal class PillFilterBar : TemplatedControl
{
    public const string ScrollViewerPart = "PART_ScrollViewer";
    public const string TrackPart = "PART_Track";
    public const string SelectionPart = "PART_Selection";
    public const string ScrollableStartPseudoClass = ":scrollable-start";
    public const string ScrollableEndPseudoClass = ":scrollable-end";
    public const string ConcealedPseudoClass = ":concealed";

    /// <summary>The options, in order; the bar rebuilds its pills when the list or its content changes.</summary>
    public static readonly StyledProperty<IList<PillFilterOption>?> OptionsProperty =
        AvaloniaProperty.Register<PillFilterBar, IList<PillFilterOption>?>(nameof(Options));

    /// <summary>The key of the selected pill (two-way); null selects the option whose key is null ("All").</summary>
    public static readonly StyledProperty<string?> SelectedKeyProperty =
        AvaloniaProperty.Register<PillFilterBar, string?>(nameof(SelectedKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>True fades the pills out (no hit testing, no focus) so a search box can cover them.</summary>
    public static readonly StyledProperty<bool> IsConcealedProperty =
        AvaloniaProperty.Register<PillFilterBar, bool>(nameof(IsConcealed));

    /// <summary>The accessible name of the group ("Filter memories").</summary>
    public static readonly StyledProperty<string?> AccessibleNameProperty =
        AvaloniaProperty.Register<PillFilterBar, string?>(nameof(AccessibleName));

    /// <summary>Length of the fade at the start edge while the row is scrolled away from its start (20).</summary>
    internal static readonly StyledProperty<double> StartFadeProperty =
        AvaloniaProperty.Register<PillFilterBar, double>(nameof(StartFade), 20);

    /// <summary>Length of the fade at the end edge while more pills follow past it (28).</summary>
    internal static readonly StyledProperty<double> EndFadeProperty =
        AvaloniaProperty.Register<PillFilterBar, double>(nameof(EndFade), 28);

    /// <summary>Raised when the person (pointer or keys) selects a different pill, after <see cref="SelectedKey"/> changed.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<PillFilterBar, RoutedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    private readonly List<PillFilterButton> _pills = new();
    private INotifyCollectionChanged? _observed;
    private ScrollViewer? _scrollViewer;
    private PillFilterTrack? _track;
    private GlidingIndicator? _indicator;
    private PillFilterButton? _selected;
    private MotionValue<double>? _scroll;
    private Rect? _lastTarget;
    private bool _attached;
    private bool _fallbackPosted;
    private bool _layoutHooked;
    private bool _placePending;
    private bool _glidePending;
    private bool _scrollPending;
    private bool _scrollAnimated;
    private LinearGradientBrush? _fadeTemplate;
    private (bool Start, bool End, double Width) _fadeState;

    static PillFilterBar()
    {
        FocusableProperty.OverrideDefaultValue<PillFilterBar>(false);
    }

    /// <inheritdoc cref="OptionsProperty"/>
    public IList<PillFilterOption>? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    /// <inheritdoc cref="SelectedKeyProperty"/>
    public string? SelectedKey
    {
        get => GetValue(SelectedKeyProperty);
        set => SetValue(SelectedKeyProperty, value);
    }

    /// <inheritdoc cref="IsConcealedProperty"/>
    public bool IsConcealed
    {
        get => GetValue(IsConcealedProperty);
        set => SetValue(IsConcealedProperty, value);
    }

    /// <inheritdoc cref="AccessibleNameProperty"/>
    public string? AccessibleName
    {
        get => GetValue(AccessibleNameProperty);
        set => SetValue(AccessibleNameProperty, value);
    }

    internal double StartFade
    {
        get => GetValue(StartFadeProperty);
        set => SetValue(StartFadeProperty, value);
    }

    internal double EndFade
    {
        get => GetValue(EndFadeProperty);
        set => SetValue(EndFadeProperty, value);
    }

    /// <summary>Raised when the person (pointer or keys) selects a different pill.</summary>
    public event EventHandler<RoutedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <summary>The pill controls, in option order (tests and the owner's focus rules).</summary>
    internal IReadOnlyList<PillFilterButton> Pills => _pills;

    /// <summary>The selected pill, or null while no option matches <see cref="SelectedKey"/>.</summary>
    internal PillFilterButton? SelectedPill => _selected;

    /// <summary>The selected background (template part).</summary>
    internal GlidingIndicator? Indicator => _indicator;

    /// <summary>The scrolling row (template part).</summary>
    internal ScrollViewer? Scroller => _scrollViewer;

    /// <summary>
    /// Focuses the selected pill as the keyboard does (with its focus ring; <paramref name="method"/> Pointer leaves the
    /// ring off) and scrolls it into view. Returns false when there is no selected pill or it cannot take focus (concealed,
    /// disabled, not shown).
    /// </summary>
    public bool FocusSelected(NavigationMethod method = NavigationMethod.Tab)
    {
        if (_selected is not { } pill || IsConcealed || !pill.IsEffectivelyVisible || !pill.IsEffectivelyEnabled)
        {
            return false;
        }
        if (!pill.Focus(method))
        {
            return false;
        }
        RequestPlacement(glide: false, scroll: true, animateScroll: false);
        return true;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_scrollViewer is not null)
        {
            _scrollViewer.ScrollChanged -= OnScrollChanged;
            _scrollViewer.OpacityMask = null;
        }
        if (_track is not null)
        {
            _track.Arranged -= OnTrackArranged;
            foreach (var pill in _pills)
            {
                _track.Children.Remove(pill);
            }
        }
        _scroll?.Stop();
        _scroll = null;
        _fadeState = default;
        _lastTarget = null;
        _scrollViewer = e.NameScope.Find<ScrollViewer>(ScrollViewerPart);
        _track = e.NameScope.Find<PillFilterTrack>(TrackPart);
        _indicator = e.NameScope.Find<GlidingIndicator>(SelectionPart);
        if (_scrollViewer is not null)
        {
            _scrollViewer.ScrollChanged += OnScrollChanged;
        }
        if (_track is not null)
        {
            _track.Arranged += OnTrackArranged;
            foreach (var pill in _pills)
            {
                _track.Children.Add(pill);
            }
        }
        RebuildPills();
        RequestPlacement(glide: false, scroll: true, animateScroll: false);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Observe(Options);
        // The list may have changed while the bar was away from the tree.
        RebuildPills();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        Observe(null);
        UnhookLayout();
        _scroll?.Stop();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OptionsProperty)
        {
            if (_attached)
            {
                Observe(change.GetNewValue<IList<PillFilterOption>?>());
            }
            RebuildPills();
        }
        else if (change.Property == SelectedKeyProperty)
        {
            ApplySelection(glide: true);
        }
        else if (change.Property == IsConcealedProperty)
        {
            var concealed = change.GetNewValue<bool>();
            PseudoClasses.Set(ConcealedPseudoClass, concealed);
            foreach (var pill in _pills)
            {
                pill.Focusable = !concealed;
            }
            AutomationProperties.SetIsOffscreenBehavior(this, concealed ? IsOffscreenBehavior.Offscreen : IsOffscreenBehavior.Default);
            if (ControlAutomationPeer.FromElement(this) is BarPeer peer)
            {
                peer.ConcealedChanged();
            }
        }
        else if (change.Property == AccessibleNameProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>());
        }
        else if (change.Property == StartFadeProperty || change.Property == EndFadeProperty)
        {
            _fadeState = default;
            UpdateScrollState();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new BarPeer(this);

    #region Options and pills

    private void Observe(IList<PillFilterOption>? options)
    {
        var observed = options as INotifyCollectionChanged;
        if (ReferenceEquals(observed, _observed))
        {
            return;
        }
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnOptionsCollectionChanged;
        }
        _observed = observed;
        if (_observed is not null)
        {
            _observed.CollectionChanged += OnOptionsCollectionChanged;
        }
    }

    private void OnOptionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildPills();

    /// <summary>Brings the pills in line with <see cref="Options"/>, reusing the pill controls by position.</summary>
    private void RebuildPills()
    {
        if (_track is null)
        {
            return;
        }
        var options = Options;
        var count = options?.Count ?? 0;
        for (var i = 0; i < count; i++)
        {
            var option = options![i];
            PillFilterButton pill;
            if (i < _pills.Count)
            {
                pill = _pills[i];
            }
            else
            {
                pill = new PillFilterButton { Focusable = !IsConcealed };
                pill.Click += OnPillClick;
                _pills.Add(pill);
                _track.Children.Add(pill);
            }
            pill.Key = option.Key;
            pill.Label = option.Label;
            AutomationProperties.SetPositionInSet(pill, i + 1);
            AutomationProperties.SetSizeOfSet(pill, count);
        }
        for (var i = _pills.Count - 1; i >= count; i--)
        {
            var pill = _pills[i];
            _pills.RemoveAt(i);
            _track.Children.Remove(pill);
            pill.Click -= OnPillClick;
        }
        ApplySelection(glide: false);
    }

    private PillFilterButton? FindPill(string? key)
    {
        foreach (var pill in _pills)
        {
            if (string.Equals(pill.Key, key, StringComparison.Ordinal))
            {
                return pill;
            }
        }
        return null;
    }

    #endregion

    #region Selection

    /// <summary>Marks the pill that matches <see cref="SelectedKey"/>, moves the tab stop and the background to it.</summary>
    private void ApplySelection(bool glide)
    {
        var key = SelectedKey;
        var selected = FindPill(key);
        var changed = !ReferenceEquals(selected, _selected);
        _selected = selected;
        foreach (var pill in _pills)
        {
            pill.IsSelected = ReferenceEquals(pill, selected);
            // Roving tab stop: only the selected pill (the first while none is selected) is reached with Tab.
            KeyboardNavigation.SetIsTabStop(pill, selected is null ? ReferenceEquals(pill, _pills[0]) : ReferenceEquals(pill, selected));
        }
        if (selected is null)
        {
            ScheduleFallback();
            if (_pills.Count == 0)
            {
                _indicator?.Hide(animate: false);
                _lastTarget = null;
            }
            return;
        }
        RequestPlacement(glide: glide && changed, scroll: changed, animateScroll: glide);
    }

    /// <summary>A key that matches no option falls back to "All" (or the first option) after the current dispatcher job.</summary>
    private void ScheduleFallback()
    {
        if (_fallbackPosted || Options is not { Count: > 0 })
        {
            return;
        }
        _fallbackPosted = true;
        Dispatcher.UIThread.Post(ApplyFallback, DispatcherPriority.Normal);
    }

    private void ApplyFallback()
    {
        _fallbackPosted = false;
        if (Options is not { Count: > 0 } options)
        {
            return;
        }
        var key = SelectedKey;
        PillFilterOption? fallback = null;
        foreach (var option in options)
        {
            if (string.Equals(option.Key, key, StringComparison.Ordinal))
            {
                return;
            }
            if (option.Key is null && fallback is null)
            {
                fallback = option;
            }
        }
        SetCurrentValue(SelectedKeyProperty, (fallback ?? options[0]).Key);
    }

    private void OnPillClick(object? sender, RoutedEventArgs e)
    {
        if (sender is PillFilterButton pill)
        {
            Choose(pill);
        }
    }

    /// <summary>The person chose <paramref name="pill"/>: select it and say so when that changed the selection.</summary>
    private void Choose(PillFilterButton pill)
    {
        if (ReferenceEquals(pill, _selected) && string.Equals(SelectedKey, pill.Key, StringComparison.Ordinal))
        {
            RequestPlacement(glide: false, scroll: true, animateScroll: true);
            return;
        }
        SetCurrentValue(SelectedKeyProperty, pill.Key);
        RaiseEvent(new RoutedEventArgs(SelectionChangedEvent, this));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || IsConcealed || _pills.Count == 0 || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var from = e.Source is Visual source ? IndexOfPill(source) : -1;
        if (from < 0)
        {
            return;
        }
        var count = _pills.Count;
        var to = e.Key switch
        {
            Key.Left => (from - 1 + count) % count,
            Key.Right => (from + 1) % count,
            Key.Home => 0,
            Key.End => count - 1,
            _ => -1
        };
        if (to < 0)
        {
            return;
        }
        e.Handled = true;
        var pill = _pills[to];
        // Auto-activation: the pill that takes focus is selected at once.
        Choose(pill);
        pill.Focus(NavigationMethod.Directional);
    }

    private int IndexOfPill(Visual source)
    {
        for (var i = 0; i < _pills.Count; i++)
        {
            if (ReferenceEquals(_pills[i], source))
            {
                return i;
            }
        }
        return -1;
    }

    #endregion

    #region Placement (the background and the scroll position)

    /// <summary>
    /// Moves the selected background to the selected pill (gliding when <paramref name="glide"/>, otherwise at once)
    /// and scrolls the pill into view, now when the row is laid out or after the next layout pass.
    /// </summary>
    private void RequestPlacement(bool glide, bool scroll, bool animateScroll)
    {
        _placePending = true;
        _glidePending |= glide;
        if (scroll)
        {
            _scrollPending = true;
            _scrollAnimated = animateScroll;
        }
        if (IsLaidOut())
        {
            Place();
        }
        else
        {
            HookLayout();
        }
    }

    private bool IsLaidOut() =>
        _track is { IsArrangeValid: true } && _scrollViewer is { IsArrangeValid: true } &&
        (_selected is null || _selected.IsArrangeValid);

    private void Place()
    {
        _placePending = false;
        var glide = _glidePending;
        var scroll = _scrollPending;
        _glidePending = false;
        _scrollPending = false;
        if (_selected is not { } pill || _indicator is null || pill.Bounds.Width <= 0)
        {
            return;
        }
        // The background shares the track's coordinates (it is arranged over the whole track).
        var rect = pill.Bounds;
        if (glide || _lastTarget != rect || !_indicator.HasTarget)
        {
            // A layout change during a glide retargets the glide; otherwise the background follows the pill at once.
            var animate = glide || (_lastTarget is { } last && _indicator.CurrentRect != last);
            _indicator.MoveTo(rect, animate, ArcSpring.Quick);
            _lastTarget = rect;
        }
        if (scroll)
        {
            ScrollIntoView(pill, _scrollAnimated);
        }
    }

    /// <summary>The track arranged its pills: the background follows the selected pill when it moved or resized.</summary>
    private void OnTrackArranged()
    {
        if (_selected is { } pill && (_lastTarget != pill.Bounds || _placePending))
        {
            _placePending = true;
            HookLayout();
        }
    }

    private void HookLayout()
    {
        if (_layoutHooked)
        {
            return;
        }
        _layoutHooked = true;
        LayoutUpdated += OnLayoutUpdated;
    }

    private void UnhookLayout()
    {
        if (!_layoutHooked)
        {
            return;
        }
        _layoutHooked = false;
        LayoutUpdated -= OnLayoutUpdated;
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (!IsLaidOut())
        {
            return;
        }
        UnhookLayout();
        if (_placePending)
        {
            Place();
        }
    }

    /// <summary>
    /// Scrolls the least distance that shows <paramref name="pill"/> whole and clear of the edge fades (a fade only
    /// shows while there is more to scroll on its side, so at either end the pill may touch the edge).
    /// </summary>
    private void ScrollIntoView(PillFilterButton pill, bool animate)
    {
        if (_scrollViewer is not { } viewer)
        {
            return;
        }
        var viewport = viewer.Viewport.Width;
        var max = viewer.Extent.Width - viewport;
        if (viewport <= 0 || max <= 0.5)
        {
            return;
        }
        var offset = _scroll is { IsAnimating: true } running ? running.Target : viewer.Offset.X;
        var left = pill.Bounds.Left;
        var right = pill.Bounds.Right;
        var target = offset;
        if (left - StartFade < offset)
        {
            target = left - StartFade;
        }
        else if (right + EndFade > offset + viewport)
        {
            target = right + EndFade - viewport;
        }
        target = Math.Clamp(target, 0, max);
        if (Math.Abs(target - viewer.Offset.X) < 0.5 && _scroll is not { IsAnimating: true })
        {
            return;
        }
        _scroll ??= CreateScroll(viewer);
        if (!_scroll.IsAnimating)
        {
            _scroll.Jump(viewer.Offset.X);
        }
        if (animate && IsLoaded)
        {
            // The scroll rides the same quick spring as the background (Reduced and Off jump).
            _ = _scroll.AnimateTo(target, MotionSpec.Spring(ArcMotionPresets.Quick));
        }
        else
        {
            _scroll.Jump(target);
        }
    }

    private MotionValue<double> CreateScroll(ScrollViewer viewer)
    {
        var value = new MotionValue<double>(this, viewer.Offset.X, MotionVectors.Double);
        value.Changed += x => viewer.Offset = new Vector(x, viewer.Offset.Y);
        return value;
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        // The person scrolled (wheel, touch, a clamp): stop chasing the selected pill.
        if (_scroll is { IsAnimating: true } scroll && _scrollViewer is { } viewer && Math.Abs(viewer.Offset.X - scroll.Value) > 0.5)
        {
            scroll.Stop();
        }
        UpdateScrollState();
        if (e.ExtentDelta.X != 0 || e.ViewportDelta.X != 0)
        {
            // The row or the bar changed size: keep the selected pill in view.
            RequestPlacement(glide: false, scroll: true, animateScroll: false);
        }
    }

    /// <summary>
    /// Sets <c>:scrollable-start</c> / <c>:scrollable-end</c> and the edge mask: each fade shows only while the row can
    /// scroll that way. A new immutable brush is published only when that state or the bar's width changes.
    /// </summary>
    private void UpdateScrollState()
    {
        if (_scrollViewer is not { } viewer)
        {
            return;
        }
        var width = viewer.Viewport.Width;
        var offset = viewer.Offset.X;
        var start = width > 0 && offset > 0.5;
        var end = width > 0 && viewer.Extent.Width - width - offset > 0.5;
        PseudoClasses.Set(ScrollableStartPseudoClass, start);
        PseudoClasses.Set(ScrollableEndPseudoClass, end);
        var state = (start, end, width);
        if (state == _fadeState)
        {
            return;
        }
        _fadeState = state;
        if (!start && !end)
        {
            viewer.OpacityMask = null;
            return;
        }
        if (_fadeTemplate is null)
        {
            _fadeTemplate = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative)
            };
            for (var i = 0; i < 4; i++)
            {
                _fadeTemplate.GradientStops.Add(new GradientStop(Colors.Black, 0));
            }
        }
        var stops = _fadeTemplate.GradientStops;
        stops[0].Color = start ? Colors.Transparent : Colors.Black;
        stops[0].Offset = 0;
        stops[1].Offset = start ? Math.Clamp(StartFade / width, 0, 0.5) : 0;
        stops[2].Offset = end ? Math.Clamp(1 - EndFade / width, 0.5, 1) : 1;
        stops[3].Color = end ? Colors.Transparent : Colors.Black;
        stops[3].Offset = 1;
        viewer.OpacityMask = new ImmutableLinearGradientBrush(_fadeTemplate);
    }

    #endregion

    /// <summary>
    /// The bar reads as a named group (Arc's <c>role="group"</c> with <c>aria-label</c>); while concealed it is offscreen
    /// and its pills are left out, as the search box covers them.
    /// </summary>
    private sealed class BarPeer(PillFilterBar owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() =>
            ((PillFilterBar)Owner).IsConcealed ? null : base.GetChildrenCore();

        public void ConcealedChanged() => InvalidateChildren();
    }
}
