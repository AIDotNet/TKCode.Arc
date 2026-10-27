using TKCode.Arc.DesignTokens;
using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>A snapshot of one item's motion in a <see cref="DeveloperListPanel"/> (tests and blocks that coordinate with it).</summary>
/// <param name="Visual">The item's control.</param>
/// <param name="Offset">FLIP offset from its layout position (px, 0 at rest).</param>
/// <param name="OffsetVelocity">Velocity of that offset (px/s).</param>
/// <param name="Opacity">Drawn opacity (0–1).</param>
/// <param name="HeightFactor">Share of its height it takes in the layout (0–1; Grow / Collapse).</param>
/// <param name="Flash">Opacity of its flash (0–1).</param>
/// <param name="IsLeaving">True while it plays its exit.</param>
internal readonly record struct DeveloperListItemState(
    Control Visual,
    double Offset,
    double OffsetVelocity,
    double Opacity,
    double HeightFactor,
    double Flash,
    bool IsLeaving);

/// <summary>Arguments of <see cref="DeveloperListPanel.ItemPrepared"/> and <see cref="DeveloperListPanel.ItemExited"/>.</summary>
internal sealed class DeveloperListItemEventArgs(object item, Control visual) : EventArgs
{
    /// <summary>The data item.</summary>
    public object Item { get; } = item;

    /// <summary>Its control (built from <see cref="DeveloperListPanel.ItemTemplate"/>, or the item itself when it is a control).</summary>
    public Control Visual { get; } = visual;
}

/// <summary>
/// The Developer group's single-column list: the items of <see cref="Items"/> stacked top to bottom in order, one control
/// each (built by <see cref="ItemTemplate"/>, or the item itself when it is a control). When the items change (the
/// collection raises <see cref="INotifyCollectionChanged"/>, <see cref="Items"/> is replaced, or <see cref="Refresh"/> is
/// called) it diffs by item identity and plays <see cref="Motion"/>:
/// <list type="bullet">
/// <item>items that stay and whose place changed jump back to where they were drawn and spring to their new place (FLIP,
/// render transform only; a glide that is retargeted keeps its velocity);</item>
/// <item>items that leave either pop out of the layout and fade where they were drawn (still drawn, not hit-testable,
/// until the fade ends), or collapse their height to 0 in place;</item>
/// <item>items that arrive fade in at their place or grow from height 0, optionally with a flash behind them;</item>
/// <item>the panel's own height can follow its content on a spring (the list frame of Integrations).</item>
/// </list>
/// Code-driven (<see cref="MotionValue{T}"/>) because the motion depends on where each row was drawn before the change and
/// must keep its speed when a new keystroke retargets it, which a ControlTheme transition cannot know. Reduced motion
/// drops travel, height and blur (entering items fade in over 120ms, leaving items are removed at once, so they never
/// overlap the ones that already jumped); Off jumps. The panel owns each item control's <c>Opacity</c>,
/// <c>RenderTransform</c>, <c>Effect</c>, <c>Clip</c>, <c>ZIndex</c> and <c>IsHitTestVisible</c>: do not set them on the
/// item's root. Flashes are drawn by the panel below the rows, so a row's root should be transparent where the flash
/// must show. Items must be unique (the first of equal items wins).
/// </summary>
internal sealed class DeveloperListPanel : Control
{
    /// <summary>The items in display order (observed when it raises collection changes).</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsProperty =
        AvaloniaProperty.Register<DeveloperListPanel, IEnumerable?>(nameof(Items));

    /// <summary>Builds the control of an item that is not a control itself (its DataContext is the item).</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<DeveloperListPanel, IDataTemplate?>(nameof(ItemTemplate));

    /// <summary>The motion of item changes (default <see cref="DeveloperListMotion.Insert"/>).</summary>
    public static readonly StyledProperty<DeveloperListMotion> MotionProperty =
        AvaloniaProperty.Register<DeveloperListPanel, DeveloperListMotion>(nameof(Motion), DeveloperListMotion.Insert);

    /// <summary>Fill of row flashes (default the AccentSubtle token).</summary>
    public static readonly StyledProperty<IBrush?> FlashBrushProperty =
        AvaloniaProperty.Register<DeveloperListPanel, IBrush?>(nameof(FlashBrush));

    /// <summary>Corner radius of row flashes (Webhooks 10; API keys and Env variables 0).</summary>
    public static readonly StyledProperty<CornerRadius> FlashCornerRadiusProperty =
        AvaloniaProperty.Register<DeveloperListPanel, CornerRadius>(nameof(FlashCornerRadius));

    /// <summary>Inset of row flashes from the row bounds (Webhooks 8,2; API keys and Env variables 0).</summary>
    public static readonly StyledProperty<Thickness> FlashInsetProperty =
        AvaloniaProperty.Register<DeveloperListPanel, Thickness>(nameof(FlashInset));

    /// <summary>Horizontal overflow kept visible while the panel or a row clips its height (focus rings; default 8).</summary>
    public static readonly StyledProperty<double> ClipOverflowProperty =
        AvaloniaProperty.Register<DeveloperListPanel, double>(nameof(ClipOverflow), 8);

    private readonly List<Entry> _layout = new();
    private readonly List<Entry> _popped = new();
    private readonly Dictionary<object, Entry> _live = new();
    private readonly RectangleGeometry _clip = new();
    private INotifyCollectionChanged? _observed;
    private DeveloperListMotion? _override;
    private DeveloperListMotion _lastMotion = DeveloperListMotion.Insert;
    private MotionValue<double>? _container;
    private double _containerTarget = double.NaN;
    private double _contentHeight;
    private double _lastWidth = double.NaN;
    private bool _armed;
    private bool _disarmPosted;
    private bool _syncedOnce;

    static DeveloperListPanel()
    {
        AffectsRender<DeveloperListPanel>(FlashBrushProperty, FlashCornerRadiusProperty, FlashInsetProperty);
        AffectsArrange<DeveloperListPanel>(ClipOverflowProperty);
    }

    public DeveloperListPanel()
    {
        ClipToBounds = false;
        ArcTokens.Bind(this, FlashBrushProperty, ArcDesignTokenKind.AccentSubtle);
    }

    /// <inheritdoc cref="ItemsProperty"/>
    public IEnumerable? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <inheritdoc cref="ItemTemplateProperty"/>
    public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <inheritdoc cref="MotionProperty"/>
    public DeveloperListMotion Motion { get => GetValue(MotionProperty); set => SetValue(MotionProperty, value); }

    /// <inheritdoc cref="FlashBrushProperty"/>
    public IBrush? FlashBrush { get => GetValue(FlashBrushProperty); set => SetValue(FlashBrushProperty, value); }

    /// <inheritdoc cref="FlashCornerRadiusProperty"/>
    public CornerRadius FlashCornerRadius { get => GetValue(FlashCornerRadiusProperty); set => SetValue(FlashCornerRadiusProperty, value); }

    /// <inheritdoc cref="FlashInsetProperty"/>
    public Thickness FlashInset { get => GetValue(FlashInsetProperty); set => SetValue(FlashInsetProperty, value); }

    /// <inheritdoc cref="ClipOverflowProperty"/>
    public double ClipOverflow { get => GetValue(ClipOverflowProperty); set => SetValue(ClipOverflowProperty, value); }

    /// <summary>Raised when an item's control is created and added (wire per-row handlers here).</summary>
    public event EventHandler<DeveloperListItemEventArgs>? ItemPrepared;

    /// <summary>Raised when a leaving item finished its exit and its control was removed.</summary>
    public event EventHandler<DeveloperListItemEventArgs>? ItemExited;

    /// <summary>The live items (not leaving) in display order.</summary>
    public IReadOnlyList<object> LiveItems => _layout.Where(e => !e.IsLeaving).Select(e => e.Item).ToList();

    /// <summary>Items still playing their exit (popped or collapsing).</summary>
    public int ExitingCount => _popped.Count + _layout.Count(e => e.IsLeaving);

    /// <summary>The panel's current (possibly springing) height.</summary>
    public double ContainerHeight => _container?.Value ?? _contentHeight;

    /// <summary>The height of the content: every laid out item's height times its height factor.</summary>
    public double ContentHeight => _contentHeight;

    /// <summary>True while any item or the panel height moves.</summary>
    public bool IsMoving => _container?.IsAnimating == true || _layout.Concat(_popped).Any(e => e.IsMoving);

    /// <summary>
    /// Plays <paramref name="motion"/> for the changes <paramref name="mutate"/> makes (instead of <see cref="Motion"/>):
    /// <c>list.Change(DeveloperListMotion.Instant, () => keys.Clear())</c>. Changes are applied as they happen, so the
    /// collection must be observed (or call <see cref="Refresh"/> inside).
    /// </summary>
    public void Change(DeveloperListMotion motion, Action mutate)
    {
        var previous = _override;
        _override = motion;
        try
        {
            mutate();
        }
        finally
        {
            _override = previous;
        }
    }

    /// <summary>Re-reads <see cref="Items"/> (a list that does not raise collection changes, or items whose order was changed in place).</summary>
    public void Refresh() => Sync(animate: true);

    /// <summary>The control of a live or leaving item, or null.</summary>
    public Control? ContainerFor(object item) => Find(item)?.Visual;

    /// <summary>The motion state of a live or leaving item, or null.</summary>
    public DeveloperListItemState? StateOf(object item)
    {
        if (Find(item) is not { } e)
        {
            return null;
        }
        return new DeveloperListItemState(e.Visual, e.Offset.Value, e.Offset.Velocity, e.Visual.Opacity,
            Math.Clamp(e.Height.Value, 0, 1), e.Flash.Value, e.IsLeaving);
    }

    /// <summary>
    /// Flashes a live item (default <see cref="DeveloperFlash.Held"/>): Env variables glows a saved row with
    /// <see cref="DeveloperFlash.Glow"/>. Returns false when the item is not live.
    /// </summary>
    public bool Flash(object item, DeveloperFlash? flash = null)
    {
        if (!_live.TryGetValue(item, out var e))
        {
            return false;
        }
        StartFlash(e, flash ?? DeveloperFlash.Held);
        return true;
    }

    /// <summary>Releases a held flash: it fades after its <see cref="DeveloperFlash.FadeDelay"/> (a reveal closed, Done).</summary>
    public void EndFlash(object item)
    {
        if (Find(item) is not { } e || e.Flash.Value <= 0)
        {
            return;
        }
        var flash = e.FlashSpec ?? DeveloperFlash.Held;
        if (ArcMotion.IsFull(this))
        {
            _ = e.Flash.AnimateTo(0, MotionSpec.Tween(flash.FadeDuration, flash.Easing, flash.FadeDelay));
        }
        else
        {
            e.Flash.Jump(0);
        }
    }

    /// <summary>Releases every held flash.</summary>
    public void EndAllFlashes()
    {
        foreach (var e in _layout.ToList())
        {
            EndFlash(e.Item);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsProperty)
        {
            Observe(change.GetNewValue<IEnumerable?>());
            Sync(animate: true);
        }
        else if (change.Property == ItemTemplateProperty)
        {
            // A new template rebuilds every control without motion.
            foreach (var e in _layout.Concat(_popped).ToList())
            {
                Remove(e, raise: false);
            }
            Sync(animate: false);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Observe(Items);
        Sync(animate: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Observe(null);
        // Exits cannot finish off screen: drop them, and park the others at rest.
        foreach (var entry in _layout.Concat(_popped).ToList())
        {
            if (entry.IsLeaving)
            {
                Remove(entry, raise: true);
            }
            else
            {
                entry.Rest();
            }
        }
        _container?.Jump(_contentHeight);
    }

    private void Observe(IEnumerable? items)
    {
        var next = VisualRoot is null ? null : items as INotifyCollectionChanged;
        if (ReferenceEquals(next, _observed))
        {
            return;
        }
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnItemsChanged;
        }
        _observed = next;
        if (_observed is not null)
        {
            _observed.CollectionChanged += OnItemsChanged;
        }
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Sync(animate: true);

    private Entry? Find(object item)
    {
        if (_live.TryGetValue(item, out var live))
        {
            return live;
        }
        foreach (var e in _layout)
        {
            if (e.IsLeaving && Equals(e.Item, item))
            {
                return e;
            }
        }
        foreach (var e in _popped)
        {
            if (Equals(e.Item, item))
            {
                return e;
            }
        }
        return null;
    }

    private void Sync(bool animate)
    {
        if (VisualRoot is null)
        {
            return;
        }
        var motion = _override ?? Motion;
        _lastMotion = motion;
        animate &= _syncedOnce && IsLoaded;
        _syncedOnce = true;
        var level = ArcMotion.GetEffectiveLevel(this);

        var desired = new List<object>();
        var desiredSet = new HashSet<object>();
        if (Items is { } items)
        {
            foreach (var item in items)
            {
                if (item is not null && desiredSet.Add(item))
                {
                    desired.Add(item);
                }
            }
        }

        var oldLayout = _layout.ToList();
        foreach (var e in oldLayout)
        {
            if (!e.IsLeaving && !desiredSet.Contains(e.Item))
            {
                Leave(e, motion, animate, level);
            }
        }

        var next = new List<Entry>(desired.Count);
        foreach (var item in desired)
        {
            if (_live.TryGetValue(item, out var staying))
            {
                next.Add(staying);
                continue;
            }
            var leaving = Find(item);
            if (leaving is not null && leaving.IsLeaving && !leaving.Removed)
            {
                Revive(leaving, motion, animate, level);
                next.Add(leaving);
                continue;
            }
            var created = Create(item);
            Enter(created, motion, animate, level);
            next.Add(created);
        }

        // Collapsing items keep their place: after the nearest item that preceded them and is still laid out.
        foreach (var e in oldLayout)
        {
            if (!e.IsLeaving || e.Removed || e.LeaveMode != DeveloperListExit.Collapse || next.Contains(e))
            {
                continue;
            }
            var index = 0;
            for (var i = oldLayout.IndexOf(e) - 1; i >= 0; i--)
            {
                var at = next.IndexOf(oldLayout[i]);
                if (at >= 0)
                {
                    index = at + 1;
                    break;
                }
            }
            next.Insert(index, e);
        }

        _layout.Clear();
        _layout.AddRange(next);
        if (animate)
        {
            Arm();
        }
        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
    }

    private void Arm()
    {
        _armed = true;
        if (_disarmPosted)
        {
            return;
        }
        _disarmPosted = true;
        // Layout runs before background jobs; an arm that found nothing to move must not leak into a later resize.
        Dispatcher.UIThread.Post(() =>
        {
            _disarmPosted = false;
            _armed = false;
        }, DispatcherPriority.Background);
    }

    private Entry Create(object item)
    {
        Control visual;
        var owns = false;
        if (item is Control control)
        {
            visual = control;
        }
        else
        {
            var template = this.FindDataTemplate(item, ItemTemplate);
            visual = template?.Build(item) ?? new TextBlock { Text = item.ToString() };
            visual.DataContext = item;
            owns = true;
        }
        var entry = new Entry(this, item, visual, owns);
        _live[item] = entry;
        LogicalChildren.Add(visual);
        VisualChildren.Add(visual);
        ItemPrepared?.Invoke(this, new DeveloperListItemEventArgs(item, visual));
        return entry;
    }

    private void Enter(Entry e, DeveloperListMotion motion, bool animate, ArcMotionLevel level)
    {
        if (!animate || level == ArcMotionLevel.Off || motion.Enter == DeveloperListEnter.None)
        {
            e.Rest();
        }
        else if (level == ArcMotionLevel.Reduced)
        {
            e.Blur = 0;
            e.FollowsHeight = false;
            e.Height.Jump(1);
            e.Opacity.Jump(0);
            _ = e.Opacity.AnimateTo(1, DeveloperSprings.ReducedFade());
        }
        else if (motion.Enter == DeveloperListEnter.Fade)
        {
            e.Blur = motion.EnterBlur;
            e.Height.Jump(1);
            e.Opacity.Jump(0);
            _ = e.Opacity.AnimateTo(1, motion.EnterFade);
        }
        else
        {
            e.Blur = 0;
            e.Height.Jump(0);
            _ = e.Height.AnimateTo(1, motion.Grow);
            if (motion.FadeFollowsHeight)
            {
                e.FollowsHeight = true;
                e.Opacity.Jump(1);
            }
            else
            {
                e.Opacity.Jump(0);
                _ = e.Opacity.AnimateTo(1, motion.EnterFade);
            }
        }
        if (animate && motion.Flash is { } flash)
        {
            StartFlash(e, flash);
        }
    }

    private void Revive(Entry e, DeveloperListMotion motion, bool animate, ArcMotionLevel level)
    {
        e.IsLeaving = false;
        e.Visual.IsHitTestVisible = true;
        e.Visual.ZIndex = 0;
        if (_popped.Remove(e))
        {
            // Fly from where it fades to wherever it now belongs.
            e.Layout = e.PoppedRect;
        }
        _live[e.Item] = e;
        if (!animate || level != ArcMotionLevel.Full)
        {
            e.Rest();
            return;
        }
        _ = e.Height.AnimateTo(1, motion.Grow);
        _ = e.Opacity.AnimateTo(1, motion.Enter == DeveloperListEnter.Grow && motion.FadeFollowsHeight
            ? motion.Grow
            : motion.EnterFade);
    }

    private void Leave(Entry e, DeveloperListMotion motion, bool animate, ArcMotionLevel level)
    {
        _live.Remove(e.Item);
        // Reduced motion removes leaving rows at once (they would overlap the rows that already jumped into place).
        if (!animate || level != ArcMotionLevel.Full || motion.Exit == DeveloperListExit.None
            || (motion.Exit == DeveloperListExit.Fade && e.Layout is null))
        {
            Remove(e, raise: true);
            return;
        }
        e.IsLeaving = true;
        e.LeaveMode = motion.Exit;
        e.FollowsHeight = false;
        e.Visual.IsHitTestVisible = false;
        e.Visual.ZIndex = -1;
        if (motion.Exit == DeveloperListExit.Fade)
        {
            // Pop out of the layout where it is drawn right now (layout place plus any glide in progress).
            var layout = e.Layout!.Value;
            e.PoppedRect = new Rect(layout.X, layout.Y + e.Offset.Value, layout.Width,
                layout.Height * Math.Clamp(e.Height.Value, 0, 1));
            e.Offset.Jump(0);
            e.Blur = motion.ExitBlur;
            _layout.Remove(e);
            _popped.Add(e);
            _ = e.Opacity.AnimateTo(0, motion.ExitFade);
        }
        else
        {
            e.Blur = 0;
            _ = e.Height.AnimateTo(0, motion.Collapse);
            _ = e.Opacity.AnimateTo(0, motion.ExitFade);
        }
        e.CheckExit();
    }

    private void StartFlash(Entry e, DeveloperFlash flash)
    {
        e.FlashSpec = flash;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level != ArcMotionLevel.Full && !flash.ShowsWhenReduced)
        {
            e.Flash.Jump(0);
            return;
        }
        e.Flash.Jump(1);
        if (flash.Hold is not { } hold)
        {
            return;
        }
        var delay = hold + flash.FadeDelay;
        // Reduced motion keeps the flash but drops its fade (Arc: transition none); Off jumps (MotionValue).
        _ = e.Flash.AnimateTo(0, level == ArcMotionLevel.Full
            ? MotionSpec.Tween(flash.FadeDuration, flash.Easing, delay)
            : MotionSpec.Tween(TimeSpan.FromMilliseconds(1), new LinearEasing(), delay));
    }

    private void Remove(Entry e, bool raise)
    {
        if (e.Removed)
        {
            return;
        }
        e.Removed = true;
        _layout.Remove(e);
        _popped.Remove(e);
        if (_live.TryGetValue(e.Item, out var live) && ReferenceEquals(live, e))
        {
            _live.Remove(e.Item);
        }
        e.Dispose();
        VisualChildren.Remove(e.Visual);
        LogicalChildren.Remove(e.Visual);
        InvalidateMeasure();
        InvalidateVisual();
        if (raise)
        {
            ItemExited?.Invoke(this, new DeveloperListItemEventArgs(e.Item, e.Visual));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        var child = new Size(width, double.PositiveInfinity);
        double height = 0;
        double maxWidth = 0;
        foreach (var e in _layout)
        {
            e.Visual.Measure(child);
            height += e.Visual.DesiredSize.Height * Math.Clamp(e.Height.Value, 0, 1);
            maxWidth = Math.Max(maxWidth, e.Visual.DesiredSize.Width);
        }
        foreach (var e in _popped)
        {
            e.Visual.Measure(child);
        }
        _contentHeight = height;

        var passive = !double.IsNaN(_lastWidth) && !Same(_lastWidth, width);
        _lastWidth = width;
        _container ??= CreateContainer(height);
        if (!Same(height, _containerTarget))
        {
            _containerTarget = height;
            var spec = _lastMotion.Container;
            if (spec is not null && IsLoaded && !passive && ArcMotion.IsFull(this) && (_armed || _container.IsAnimating))
            {
                _ = _container.AnimateTo(height, spec);
            }
            else
            {
                _container.Jump(height);
            }
        }
        var resultWidth = double.IsInfinity(width) ? maxWidth : Math.Max(maxWidth, 0);
        return new Size(resultWidth, _container.Value);
    }

    private MotionValue<double> CreateContainer(double initial)
    {
        _containerTarget = initial;
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateMeasure();
        return value;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        var overflow = ClipOverflow;
        double y = 0;
        var anyFlash = false;
        foreach (var e in _layout)
        {
            var height = e.Visual.DesiredSize.Height;
            var factor = Math.Clamp(e.Height.Value, 0, 1);
            var rect = new Rect(0, y, width, height);
            e.Visual.Arrange(rect);
            if (_armed && !e.IsLeaving && e.Layout is { } old && !Same(old.Y, rect.Y))
            {
                // FLIP: jump back to where it was drawn, then glide home; a glide in progress keeps its speed.
                var velocity = e.Offset.Velocity;
                e.Offset.Jump(e.Offset.Value + old.Y - rect.Y);
                if (ArcMotion.IsFull(this))
                {
                    _ = e.Offset.AnimateTo(0, _lastMotion.Move, velocity);
                }
                else
                {
                    e.Offset.Jump(0);
                }
            }
            e.Layout = rect;
            e.ApplyClip(factor, height, width, overflow);
            anyFlash |= e.Flash.Value > 0;
            y += height * factor;
        }
        foreach (var e in _popped)
        {
            var rect = e.PoppedRect;
            e.Visual.Arrange(new Rect(rect.X, rect.Y, width, e.Visual.DesiredSize.Height));
            e.ApplyClip(rect.Height / Math.Max(1, e.Visual.DesiredSize.Height), e.Visual.DesiredSize.Height, width, overflow);
            anyFlash |= e.Flash.Value > 0;
        }

        var container = _container?.Value ?? y;
        if (_container?.IsAnimating == true || container < y - 0.5 || _popped.Count > 0 && container < _popped.Max(p => p.PoppedRect.Bottom))
        {
            _clip.Rect = new Rect(-overflow, 0, width + 2 * overflow, Math.Max(0, container));
            Clip = _clip;
            InvalidateVisual();
        }
        else
        {
            Clip = null;
        }
        if (anyFlash)
        {
            InvalidateVisual();
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (FlashBrush is not { } brush)
        {
            return;
        }
        var inset = FlashInset;
        var radius = FlashCornerRadius.TopLeft;
        foreach (var e in _layout)
        {
            DrawFlash(context, e, brush, inset, radius, e.Layout, e.Offset.Value);
        }
        foreach (var e in _popped)
        {
            DrawFlash(context, e, brush, inset, radius, e.PoppedRect, 0);
        }
    }

    private static void DrawFlash(DrawingContext context, Entry e, IBrush brush, Thickness inset, double radius, Rect? layout, double offset)
    {
        var flash = e.Flash.Value;
        if (!(flash > 0.001) || layout is not { } r)
        {
            return;
        }
        var visible = Math.Min(r.Height, e.Visual.DesiredSize.Height * Math.Clamp(e.Height.Value, 0, 1));
        var rect = new Rect(r.X, r.Y + offset, r.Width, visible).Deflate(inset);
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }
        using (context.PushOpacity(Math.Clamp(flash * e.Visual.Opacity, 0, 1)))
        {
            context.DrawRectangle(brush, null, rect, radius, radius);
        }
    }

    private static bool Same(double a, double b) => a.Equals(b) || Math.Abs(a - b) < 0.5;

    /// <summary>One item: its control and the motion values that animate it.</summary>
    private sealed class Entry : IDisposable
    {
        private readonly DeveloperListPanel _owner;
        private readonly TranslateTransform _translate = new();
        private RectangleGeometry? _clip;

        public Entry(DeveloperListPanel owner, object item, Control visual, bool owns)
        {
            _owner = owner;
            Item = item;
            Visual = visual;
            OwnsVisual = owns;
            Offset = new MotionValue<double>(owner, 0, MotionVectors.Double);
            Opacity = new MotionValue<double>(owner, 1, MotionVectors.Double);
            Height = new MotionValue<double>(owner, 1, MotionVectors.Double);
            Flash = new MotionValue<double>(owner, 0, MotionVectors.Double);
            Offset.Changed += v =>
            {
                _translate.Y = v;
                if (Flash.Value > 0)
                {
                    _owner.InvalidateVisual();
                }
            };
            Opacity.Changed += _ =>
            {
                ApplyLook();
                CheckExit();
            };
            Height.Changed += _ =>
            {
                if (FollowsHeight)
                {
                    ApplyLook();
                }
                _owner.InvalidateMeasure();
                CheckExit();
            };
            Flash.Changed += _ => _owner.InvalidateVisual();
            visual.RenderTransform = _translate;
        }

        public object Item { get; }

        public Control Visual { get; }

        public bool OwnsVisual { get; }

        public MotionValue<double> Offset { get; }

        public MotionValue<double> Opacity { get; }

        /// <summary>Share of the item's height in the layout (Grow / Collapse).</summary>
        public MotionValue<double> Height { get; }

        public MotionValue<double> Flash { get; }

        public DeveloperFlash? FlashSpec { get; set; }

        /// <summary>Blur (px) at opacity 0; the drawn blur is (1 − opacity) × Blur.</summary>
        public double Blur { get; set; }

        /// <summary>The drawn opacity also follows the height factor (Env variables' grow).</summary>
        public bool FollowsHeight { get; set; }

        public Rect? Layout { get; set; }

        public Rect PoppedRect { get; set; }

        public bool IsLeaving { get; set; }

        public DeveloperListExit LeaveMode { get; set; }

        public bool Removed { get; set; }

        public bool IsMoving => Offset.IsAnimating || Opacity.IsAnimating || Height.IsAnimating || Flash.IsAnimating;

        public void Rest()
        {
            Blur = 0;
            FollowsHeight = false;
            Offset.Jump(0);
            Height.Jump(1);
            Opacity.Jump(1);
        }

        public void ApplyLook()
        {
            var opacity = Math.Clamp(Opacity.Value, 0, 1);
            if (FollowsHeight)
            {
                opacity *= Math.Clamp(Height.Value, 0, 1);
            }
            Visual.Opacity = opacity;
            var radius = Blur * (1 - Math.Clamp(Opacity.Value, 0, 1));
            Visual.Effect = radius < 0.05 ? null : ArcBlur.Get(radius);
            if (Flash.Value > 0)
            {
                _owner.InvalidateVisual();
            }
        }

        public void ApplyClip(double factor, double height, double width, double overflow)
        {
            if (factor >= 0.999)
            {
                if (Visual.Clip is not null)
                {
                    Visual.Clip = null;
                }
                return;
            }
            _clip ??= new RectangleGeometry();
            _clip.Rect = new Rect(-overflow, 0, width + 2 * overflow, Math.Max(0, height * factor));
            Visual.Clip = _clip;
            Visual.InvalidateVisual();
        }

        /// <summary>Removes the item once its exit reached the end.</summary>
        public void CheckExit()
        {
            if (!IsLeaving || Removed)
            {
                return;
            }
            var faded = Opacity.Value <= 0 && Opacity.Target <= 0;
            var done = LeaveMode == DeveloperListExit.Fade
                ? faded
                : faded && Height.Value <= 0 && Height.Target <= 0;
            if (done)
            {
                _owner.Remove(this, raise: true);
            }
        }

        public void Dispose()
        {
            Offset.Dispose();
            Opacity.Dispose();
            Height.Dispose();
            Flash.Dispose();
            if (ReferenceEquals(Visual.RenderTransform, _translate))
            {
                Visual.RenderTransform = null;
            }
            if (ReferenceEquals(Visual.Clip, _clip))
            {
                Visual.Clip = null;
            }
            Visual.Opacity = 1;
            Visual.Effect = null;
            Visual.ZIndex = 0;
            Visual.IsHitTestVisible = true;
        }
    }
}
