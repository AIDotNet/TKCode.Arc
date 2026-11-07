using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>One mounted level: the sheet and its motion state.</summary>
internal sealed class SheetStackEntry
{
    public SheetStackEntry(SheetStackSheet sheet)
    {
        Sheet = sheet;
    }

    public readonly SheetStackSheet Sheet;
    public readonly MatrixTransform Transform = new();
    public bool Exiting;
    public int Index;
    public double Natural;

    /// <summary>Own vertical offset: the rise of a new level, the drag of the top level, the fall of a closing one.</summary>
    public SheetStackSpring Y = new(0, 0.1, 5);

    /// <summary>Displayed height (min of natural and available).</summary>
    public SheetStackSpring Height = new(0, 0.1, 5);

    /// <summary>Continuous depth: 0 for the top level, 1 for the one under it, …</summary>
    public SheetStackSpring Depth = new(0, 0.0005, 0.005);

    /// <summary>Opacity (dialogs and reduced motion fade).</summary>
    public SheetStackTween Fade = new(1);

    /// <summary>The element focused when this level opened; focus returns there when it closes.</summary>
    public IInputElement? Opener;

    public bool OpenerKeyboard;
}

/// <summary>
/// The overlay of a <see cref="SheetStack"/>: the scrim and the mounted sheets, laid out and animated here.
/// <para>
/// Motion is code-driven (one <see cref="LiveLoop"/> stepping closed-form springs) rather than ControlTheme transitions
/// because every level's pose is a function of the top sheet's live height and drag offset, recomputed each frame;
/// the levels must keep their velocity when a push is interrupted by a pop (and the other way round); a drag hands its
/// release velocity to the spring; and a closing sheet must finish its fall before it is unmounted.
/// </para>
/// </summary>
internal sealed class SheetStackLayer : Control
{
    private readonly SheetStack _owner;
    private readonly Border _scrim;
    private readonly List<SheetStackEntry> _entries = new();
    private readonly LiveLoop _loop;
    private readonly SheetStackDragGesture _drag;
    private SheetStackSpring _reference = new(0, 0.05, 1);
    private SheetStackTween _scrimFade = new(0);
    private SheetStackMetrics _metrics = SheetStackMetrics.Default;
    private bool _dialog;
    private bool _referenceValid;
    private bool _linked;
    private bool _pressInBody;
    private double _dragBase;
    private SheetStackEntry? _dragEntry;
    private Size _hostSize;

    public SheetStackLayer(SheetStack owner)
    {
        _owner = owner;
        _scrim = new Border { Opacity = 0 };
        _scrim.PointerPressed += OnScrimPressed;
        VisualChildren.Add(_scrim);
        ClipToBounds = true;
        // The loop belongs to the stack (it is in the page tree; this layer may sit in the window's overlay layer).
        _loop = new LiveLoop(owner, Step) { MinimumLevel = ArcMotionLevel.Reduced, TracksViewport = false };
        _drag = new SheetStackDragGesture(this)
        {
            CanBegin = CanBeginDrag,
            CanStart = CanStartDrag
        };
        _drag.Started += OnDragStarted;
        _drag.Moved += OnDragMoved;
        _drag.Released += OnDragReleased;
        _drag.Cancelled += OnDragCancelled;
    }

    internal IReadOnlyList<SheetStackEntry> Entries => _entries;

    internal Border Scrim => _scrim;

    internal double ScrimOpacity => _scrim.Opacity;

    internal bool IsIdle => _entries.Count == 0 && !_scrimFade.IsMoving;

    internal bool IsDragging => _drag.IsDragging;

    internal SheetStackMetrics Metrics
    {
        get => _metrics;
        set
        {
            _metrics = value;
            InvalidateMeasure();
        }
    }

    internal bool IsDialog => _dialog;

    /// <summary>The size the layer will have (known before its first layout).</summary>
    internal Size HostSize
    {
        get => Bounds.Width > 0 && Bounds.Height > 0 ? Bounds.Size : _hostSize;
        set => _hostSize = value;
    }

    internal IBrush? ScrimBrush
    {
        get => _scrim.Background;
        set => _scrim.Background = value;
    }

    private SheetStackEntry? Top
    {
        get
        {
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                if (!_entries[i].Exiting)
                {
                    return _entries[i];
                }
            }
            return null;
        }
    }

    internal int LiveCount
    {
        get
        {
            var n = 0;
            foreach (var entry in _entries)
            {
                if (!entry.Exiting)
                {
                    n++;
                }
            }
            return n;
        }
    }

    internal SheetStackEntry? Find(SheetStackSheet sheet)
    {
        foreach (var entry in _entries)
        {
            if (ReferenceEquals(entry.Sheet, sheet))
            {
                return entry;
            }
        }
        return null;
    }

    private ArcMotionLevel Level => ArcMotion.GetEffectiveLevel(_owner);

    #region Stack changes

    /// <summary>
    /// Brings the mounted levels in line with <paramref name="newLive"/>: levels past the common prefix of the old and new
    /// stacks leave (the old top falls on its own, the others drop as a group), new ones enter (a level still leaving is
    /// caught and returns from where it is).
    /// </summary>
    internal void Sync(IReadOnlyList<SheetStackSheet> oldLive, IReadOnlyList<SheetStackSheet> newLive, SheetStackChangeReason reason,
                       IInputElement? opener, bool openerKeyboard)
    {
        if (_drag.IsDragging && reason != SheetStackChangeReason.Drag)
        {
            _drag.Cancel();
        }
        var level = Level;
        var hadLive = LiveCount > 0;
        var oldTop = Top;
        FoldDrag(oldTop);

        var prefix = 0;
        while (prefix < oldLive.Count && prefix < newLive.Count && ReferenceEquals(oldLive[prefix], newLive[prefix]))
        {
            prefix++;
        }
        SheetStackEntry? lowestRemoved = null;
        for (var i = oldLive.Count - 1; i >= prefix; i--)
        {
            if (Find(oldLive[i]) is not { Exiting: false } entry)
            {
                continue;
            }
            entry.Exiting = true;
            BeginExit(entry, ReferenceEquals(entry, oldTop), reason, level);
            if (entry.Opener is not null || lowestRemoved is null)
            {
                lowestRemoved = entry;
            }
        }
        List<SheetStackEntry>? entered = null;
        for (var i = prefix; i < newLive.Count; i++)
        {
            var sheet = newLive[i];
            if (Find(sheet) is { } existing)
            {
                _entries.Remove(existing);
                _entries.Add(existing);
                existing.Exiting = false;
                if (i == prefix)
                {
                    existing.Opener = opener;
                    existing.OpenerKeyboard = openerKeyboard;
                }
                Revive(existing, level);
                continue;
            }
            var entry = new SheetStackEntry(sheet);
            if (i == prefix)
            {
                entry.Opener = opener;
                entry.OpenerKeyboard = openerKeyboard;
            }
            _entries.Add(entry);
            Mount(entry);
            (entered ??= new List<SheetStackEntry>()).Add(entry);
        }
        Relevel(level);
        if (entered is not null)
        {
            foreach (var entry in entered)
            {
                Enter(entry, level);
            }
        }
        var live = LiveCount;
        if (!hadLive && live > 0)
        {
            _referenceValid = false;
        }
        if (live > 0)
        {
            if (level == ArcMotionLevel.Off)
            {
                _scrimFade.Jump(1);
            }
            else
            {
                _scrimFade.To(1, SheetStackMotion.ScrimIn, ArcEasings.Enter);
            }
        }
        else
        {
            _scrimFade.Jump(_scrim.Opacity);
            if (level == ArcMotionLevel.Off)
            {
                _scrimFade.Jump(0);
            }
            else
            {
                _scrimFade.To(0, SheetStackMotion.ScrimOut, ArcEasings.Standard);
            }
        }
        IsHitTestVisible = live > 0;
        Kick();
        Apply();
        MoveFocus(newLive.Count > prefix, lowestRemoved, reason);
    }

    /// <summary>Lower levels continue from where the drag left them: fold the drag progress into their depth.</summary>
    private void FoldDrag(SheetStackEntry? top)
    {
        if (!_linked)
        {
            return;
        }
        _linked = false;
        if (top is null)
        {
            return;
        }
        var p = SheetStackLayout.DragProgress(top.Y.Value, top.Height.Value);
        foreach (var entry in _entries)
        {
            if (!entry.Exiting && !ReferenceEquals(entry, top))
            {
                entry.Depth.Value -= p;
            }
        }
    }

    private void Mount(SheetStackEntry entry)
    {
        var sheet = entry.Sheet;
        sheet.RenderTransform = entry.Transform;
        sheet.RenderTransformOrigin = new RelativePoint(0.5, 0, RelativeUnit.Relative);
        sheet.SetDialog(_dialog);
        sheet.BackRequested += OnBackRequested;
        sheet.CloseRequested += OnCloseRequested;
        sheet.HeaderChanged += OnHeaderChanged;
        VisualChildren.Add(sheet);
    }

    private void Unmount(SheetStackEntry entry)
    {
        var sheet = entry.Sheet;
        sheet.BackRequested -= OnBackRequested;
        sheet.CloseRequested -= OnCloseRequested;
        sheet.HeaderChanged -= OnHeaderChanged;
        sheet.SetLevel(false, 0, false);
        sheet.SetDragging(false);
        sheet.ClearValue(RenderTransformProperty);
        sheet.ClearValue(OpacityProperty);
        sheet.DimOpacity = 0;
        VisualChildren.Remove(sheet);
    }

    private void Enter(SheetStackEntry entry, ArcMotionLevel level)
    {
        var size = HostSize;
        var width = SheetStackLayout.PanelWidth(_metrics, size.Width, _dialog);
        var available = SheetStackLayout.AvailableHeight(_metrics, size.Height, _dialog, entry.Index);
        entry.Sheet.Measure(new Size(width, available));
        entry.Natural = entry.Sheet.DesiredSize.Height;
        var height = Math.Min(entry.Natural, available);
        entry.Height.Jump(height);
        entry.Depth.Jump(0);
        switch (level)
        {
            case ArcMotionLevel.Full when _dialog:
                entry.Y.Jump(SheetStackMotion.DialogEnterOffset);
                entry.Y.To(0, SheetStackMotion.Rise);
                entry.Fade.Jump(0);
                entry.Fade.To(1, SheetStackMotion.DialogFadeIn, ArcEasings.Standard);
                break;
            case ArcMotionLevel.Full:
                // Hidden just under the layer's bottom edge, then it rises.
                entry.Y.Jump(height + _metrics.ExitOvershoot);
                entry.Y.To(0, SheetStackMotion.Rise);
                entry.Fade.Jump(1);
                break;
            case ArcMotionLevel.Reduced:
                entry.Y.Jump(0);
                entry.Fade.Jump(0);
                entry.Fade.To(1, SheetStackMotion.ReducedFadeIn, SheetStackMotion.CssEase);
                break;
            default:
                entry.Y.Jump(0);
                entry.Fade.Jump(1);
                break;
        }
    }

    private void Revive(SheetStackEntry entry, ArcMotionLevel level)
    {
        switch (level)
        {
            case ArcMotionLevel.Full:
                entry.Y.To(0, SheetStackMotion.Rise);
                entry.Fade.To(1, _dialog ? SheetStackMotion.DialogFadeIn : ArcMotionPresets.Fast, ArcEasings.Standard);
                break;
            case ArcMotionLevel.Reduced:
                entry.Y.Jump(0);
                entry.Fade.To(1, SheetStackMotion.ReducedFadeIn, SheetStackMotion.CssEase);
                break;
            default:
                entry.Y.Jump(0);
                entry.Fade.Jump(1);
                break;
        }
    }

    private void BeginExit(SheetStackEntry entry, bool wasTop, SheetStackChangeReason reason, ArcMotionLevel level)
    {
        var sheet = entry.Sheet;
        sheet.IsHitTestVisible = false;
        sheet.SetDragging(false);
        KeyboardNavigation.SetTabNavigation(sheet, KeyboardNavigationMode.None);
        sheet.SetLevel(false, 0, false);
        var layerHeight = HostSize.Height;
        switch (level)
        {
            case ArcMotionLevel.Full when _dialog:
                // Fade out (110 ms, or 120 ms after a drag) while it scales to 0.97 and sinks; a dragged dialog keeps going.
                entry.Y.To(wasTop && reason == SheetStackChangeReason.Drag
                    ? Math.Max(entry.Y.Value, 0) + entry.Height.Value
                    : SheetStackMotion.DialogEnterOffset, SheetStackMotion.Exit);
                entry.Fade.To(0, reason == SheetStackChangeReason.Drag ? SheetStackMotion.DialogDragFadeOut : SheetStackMotion.DialogFadeOut,
                    SheetStackMotion.CssEase);
                break;
            case ArcMotionLevel.Full:
                // The top level falls its own height (plus 24); the levels below drop with it as a group, a layer height.
                entry.Y.To((wasTop ? entry.Height.Value : layerHeight) + _metrics.ExitOvershoot, SheetStackMotion.Exit);
                break;
            case ArcMotionLevel.Reduced:
                entry.Y.Jump(entry.Y.Value);
                entry.Fade.To(0, SheetStackMotion.ReducedFadeOut, SheetStackMotion.CssEase);
                break;
            default:
                entry.Y.Jump(entry.Y.Value);
                entry.Fade.Jump(0);
                break;
        }
    }

    /// <summary>Assigns indices, depths, heights, input and header texts to the open levels.</summary>
    internal void Relevel()
    {
        Relevel(Level);
        Kick();
        Apply();
    }

    private void Relevel(ArcMotionLevel level)
    {
        var live = LiveCount;
        var labels = _owner.Labels ?? new SheetStackLabels();
        var index = 0;
        SheetStackSheet? below = null;
        var size = HostSize;
        for (var k = 0; k < _entries.Count; k++)
        {
            var entry = _entries[k];
            var sheet = entry.Sheet;
            sheet.ZIndex = k + 1;
            if (entry.Exiting)
            {
                continue;
            }
            var depth = live - 1 - index;
            var isTop = depth == 0;
            entry.Index = index;
            if (level == ArcMotionLevel.Full)
            {
                entry.Depth.To(depth, SheetStackMotion.Depth);
            }
            else
            {
                entry.Depth.Jump(depth);
            }
            if (entry.Natural > 0)
            {
                RetargetHeight(entry, Math.Min(entry.Natural, SheetStackLayout.AvailableHeight(_metrics, size.Height, _dialog, index)), level);
            }
            sheet.SetLevel(isTop, depth, true);
            sheet.IsHitTestVisible = isTop;
            KeyboardNavigation.SetTabNavigation(sheet, isTop ? KeyboardNavigationMode.Cycle : KeyboardNavigationMode.None);
            var back = below is null ? null : string.IsNullOrEmpty(below.BackLabel) ? below.Title : below.BackLabel;
            sheet.BackText = back;
            sheet.BackName = back is null ? null : labels.BackName(back);
            sheet.CloseName = live > 1 ? labels.CloseAll : labels.Close;
            below = sheet;
            index++;
        }
    }

    private void RetargetHeight(SheetStackEntry entry, double target, ArcMotionLevel level)
    {
        if (Math.Abs(entry.Height.Target - target) < 0.01)
        {
            return;
        }
        if (level == ArcMotionLevel.Full && entry.Height.Value > 0)
        {
            entry.Height.To(target, SheetStackMotion.Height);
            Kick();
        }
        else
        {
            entry.Height.Jump(target);
        }
        InvalidateArrange();
    }

    internal void SetDialog(bool dialog)
    {
        if (_dialog == dialog)
        {
            return;
        }
        _dialog = dialog;
        foreach (var entry in _entries)
        {
            entry.Sheet.SetDialog(dialog);
        }
        InvalidateMeasure();
        Kick();
    }

    #endregion

    #region Frame loop

    /// <summary>Starts the frame loop, or settles everything at once when motion is off or the loop cannot run.</summary>
    private void Kick()
    {
        if (Level == ArcMotionLevel.Off || TopLevel.GetTopLevel(_owner) is null)
        {
            Snap();
            return;
        }
        _loop.Start();
        if (!_loop.CanRun)
        {
            Snap();
        }
    }

    private void Snap()
    {
        foreach (var entry in _entries)
        {
            entry.Y.Jump(entry.Y.Target);
            entry.Height.Jump(entry.Height.Target);
            entry.Depth.Jump(entry.Depth.Target);
            entry.Fade.Jump(entry.Fade.Target);
        }
        UpdateReference();
        _reference.Jump(_reference.Target);
        _scrimFade.Jump(_scrimFade.Target);
        RemoveFinished();
        InvalidateArrange();
        Apply();
    }

    /// <summary>Advances every spring and tween by <paramref name="dt"/> seconds (the loop's step; tests call it directly).</summary>
    internal bool Step(double dt)
    {
        var moving = false;
        var heightMoving = false;
        foreach (var entry in _entries)
        {
            if (!(_drag.IsDragging && ReferenceEquals(entry, _dragEntry)))
            {
                moving |= entry.Y.Step(dt);
            }
            if (entry.Height.Step(dt))
            {
                moving = true;
                heightMoving = true;
            }
            moving |= entry.Depth.Step(dt);
            moving |= entry.Fade.Step(dt);
        }
        UpdateReference();
        moving |= _reference.Step(dt);
        moving |= _scrimFade.Step(dt);
        if (heightMoving)
        {
            InvalidateArrange();
        }
        RemoveFinished();
        Apply();
        return moving;
    }

    /// <summary>The reference edge follows the top level's top edge at rest (its live height) on the depth spring.</summary>
    private void UpdateReference()
    {
        if (Top is not { } top)
        {
            return;
        }
        var target = SheetStackLayout.LayoutTop(HostSize.Height, top.Height.Value, _dialog);
        if (!_referenceValid)
        {
            _referenceValid = true;
            _reference.Jump(target);
            return;
        }
        _reference.To(target, SheetStackMotion.Depth);
    }

    private void RemoveFinished()
    {
        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            if (!entry.Exiting)
            {
                continue;
            }
            var gone = entry.Fade is { IsMoving: false, Value: <= 0.001 } || (!entry.Y.IsMoving && !entry.Fade.IsMoving);
            if (gone)
            {
                _entries.RemoveAt(i);
                Unmount(entry);
            }
        }
        if (IsIdle && _scrimFade.Value <= 0.001)
        {
            _owner.OnLayerIdle();
        }
    }

    /// <summary>Writes every level's transform, opacity and dim, and the scrim's opacity.</summary>
    internal void Apply()
    {
        // Motion off still shows the full static layout (scale, peek); reduced motion keeps only the dim.
        var level = Level;
        var full = level != ArcMotionLevel.Reduced;
        var layerHeight = HostSize.Height;
        var top = Top;
        var p = _linked && top is not null ? SheetStackLayout.DragProgress(top.Y.Value, top.Height.Value) : 0;
        foreach (var entry in _entries)
        {
            var ownTop = SheetStackLayout.LayoutTop(layerHeight, entry.Height.Value, _dialog);
            var depth = entry.Depth.Value;
            if (_linked && !entry.Exiting && !ReferenceEquals(entry, top))
            {
                depth -= p;
            }
            var pose = SheetStackLayout.Pose(_metrics, _dialog, depth, _reference.Value, ownTop, full);
            var fade = Math.Clamp(entry.Fade.Value, 0, 1);
            var scale = pose.Scale;
            if (level == ArcMotionLevel.Full && _dialog)
            {
                scale *= SheetStackMotion.DialogEnterScale + (1 - SheetStackMotion.DialogEnterScale) * fade;
            }
            var ty = pose.TranslateY + entry.Y.Value;
            entry.Transform.Matrix = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(0, ty);
            entry.Sheet.Opacity = fade;
            entry.Sheet.DimOpacity = pose.Dim;
        }
        var scrim = Math.Clamp(_scrimFade.Value, 0, 1);
        if (_linked && top is not null && LiveCount == 1)
        {
            scrim *= 1 - p;
        }
        _scrim.Opacity = scrim;
    }

    #endregion

    #region Layout

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = Resolve(availableSize);
        _scrim.Measure(size);
        var width = SheetStackLayout.PanelWidth(_metrics, size.Width, _dialog);
        var level = Level;
        foreach (var entry in _entries)
        {
            // Measured within its available height, a level's desired height is its natural height capped there (its body
            // scrolls past it), which is the height spring's target.
            var available = entry.Exiting
                ? Math.Max(entry.Height.Value, 0)
                : SheetStackLayout.AvailableHeight(_metrics, size.Height, _dialog, entry.Index);
            entry.Sheet.Measure(new Size(width, available));
            entry.Natural = entry.Sheet.DesiredSize.Height;
            if (!entry.Exiting)
            {
                RetargetHeight(entry, Math.Min(entry.Natural, available), level);
            }
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _hostSize = finalSize;
        _scrim.Arrange(new Rect(finalSize));
        var width = SheetStackLayout.PanelWidth(_metrics, finalSize.Width, _dialog);
        var left = (finalSize.Width - width) / 2;
        foreach (var entry in _entries)
        {
            var height = Math.Max(0, entry.Height.Value);
            var top = SheetStackLayout.LayoutTop(finalSize.Height, height, _dialog);
            if (Math.Abs(entry.Sheet.DesiredSize.Height - height) > 0.5)
            {
                // Mid-spring (or leaving) the level is shorter or taller than measured: lay its parts out for the
                // height it is drawn at, so the body scrolls and the footer stays pinned.
                entry.Sheet.Measure(new Size(width, height));
            }
            entry.Sheet.Arrange(new Rect(left, top, width, height));
        }
        UpdateReference();
        Apply();
        return finalSize;
    }

    private Size Resolve(Size available)
    {
        var width = double.IsInfinity(available.Width) ? _hostSize.Width : available.Width;
        var height = double.IsInfinity(available.Height) ? _hostSize.Height : available.Height;
        return new Size(double.IsFinite(width) ? width : 0, double.IsFinite(height) ? height : 0);
    }

    #endregion

    #region Buttons, scrim, keys

    private void OnBackRequested(object? sender, EventArgs e)
    {
        if (sender is SheetStackSheet sheet && Find(sheet) is { Exiting: false } entry && ReferenceEquals(entry, Top))
        {
            _owner.PopTop(SheetStackChangeReason.Back);
        }
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        if (sender is SheetStackSheet sheet && Find(sheet) is { Exiting: false } entry && ReferenceEquals(entry, Top))
        {
            if (LiveCount > 1)
            {
                _owner.CloseAll(SheetStackChangeReason.CloseAll);
            }
            else
            {
                _owner.PopTop(SheetStackChangeReason.Close);
            }
        }
    }

    private void OnHeaderChanged(object? sender, EventArgs e) => Relevel(Level);

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Top is null)
        {
            return;
        }
        if (e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(_scrim).Properties.IsLeftButtonPressed)
        {
            return;
        }
        e.Handled = true;
        _owner.PopTop(SheetStackChangeReason.ScrimPress);
    }

    #endregion

    #region Drag

    private bool CanBeginDrag(PointerPressedEventArgs e)
    {
        if (Top is not { } top || e.Source is not Visual source)
        {
            return false;
        }
        var sheet = top.Sheet;
        if (!ReferenceEquals(source, sheet) && !sheet.IsVisualAncestorOf(source))
        {
            return false;
        }
        for (Visual? v = source; v is not null && !ReferenceEquals(v, sheet); v = v.GetVisualParent())
        {
            // Text boxes keep their own drag (selection); content can opt out (sliders, maps).
            if (v is TextBox || (v is AvaloniaObject o && SheetStack.GetIsDragExcluded(o)))
            {
                return false;
            }
        }
        _pressInBody = sheet.Body is { } body && body.IsVisualAncestorOf(source);
        if (_pressInBody && sheet.Body!.Offset.Y > 0.5)
        {
            // A scrolled body scrolls; only a body at its top hands the gesture to the sheet.
            return false;
        }
        return true;
    }

    private bool CanStartDrag(PointerType pointerType, double travel)
    {
        if (pointerType != PointerType.Touch || !_pressInBody || travel >= 0 || Top?.Sheet.Body is not { } body)
        {
            return true;
        }
        // A finger swiping up in a body that can scroll scrolls it.
        return body.Extent.Height <= body.Viewport.Height + 0.5;
    }

    private void OnDragStarted()
    {
        if (Top is not { } top)
        {
            _drag.Cancel();
            return;
        }
        _dragEntry = top;
        _linked = true;
        _dragBase = Math.Max(0, top.Y.Value);
        top.Y.Jump(top.Y.Value);
        top.Sheet.SetDragging(true);
    }

    private void OnDragMoved(double raw)
    {
        if (_dragEntry is not { Exiting: false } top || !ReferenceEquals(top, Top))
        {
            return;
        }
        top.Y.Jump(SheetStackDragModel.Display(_dragBase + raw, top.Height.Value, top.Sheet.IsDismissible));
        Apply();
    }

    private void OnDragReleased(double velocity)
    {
        var top = _dragEntry;
        _dragEntry = null;
        if (top is not { Exiting: false } || !ReferenceEquals(top, Top))
        {
            return;
        }
        top.Sheet.SetDragging(false);
        var decision = SheetStackDragModel.Decide(top.Y.Value, velocity, top.Height.Value, top.Sheet.IsDismissible);
        top.Y.Velocity = velocity;
        if (decision == SheetStackRelease.Dismiss)
        {
            _owner.PopTop(SheetStackChangeReason.Drag);
            return;
        }
        SpringBack(top);
    }

    private void OnDragCancelled()
    {
        var top = _dragEntry;
        _dragEntry = null;
        if (top is null || top.Exiting)
        {
            return;
        }
        top.Sheet.SetDragging(false);
        top.Y.Velocity = 0;
        SpringBack(top);
    }

    private void SpringBack(SheetStackEntry top)
    {
        if (Level == ArcMotionLevel.Full)
        {
            top.Y.To(0, SheetStackMotion.Rise);
        }
        else
        {
            top.Y.Jump(0);
            _linked = false;
        }
        Kick();
        Apply();
    }

    #endregion

    #region Focus

    private void MoveFocus(bool pushed, SheetStackEntry? lowestRemoved, SheetStackChangeReason reason)
    {
        if (TopLevel.GetTopLevel(_owner) is not { } topLevel)
        {
            return;
        }
        var top = Top;
        if (pushed)
        {
            // The new level takes focus itself (no ring), like a dialog element with tabindex -1.
            top?.Sheet.Focus(NavigationMethod.Unspecified);
            return;
        }
        if (lowestRemoved is null)
        {
            return;
        }
        var focused = topLevel.FocusManager?.GetFocusedElement();
        var keyboard = reason == SheetStackChangeReason.Escape || lowestRemoved.OpenerKeyboard ||
                       (focused is StyledElement styled && styled.Classes.Contains(":focus-visible"));
        if (lowestRemoved.Opener is InputElement opener && TopLevel.GetTopLevel(opener) is not null &&
            opener.IsEffectivelyVisible && opener.IsEffectivelyEnabled && IsReachable(opener))
        {
            opener.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
            return;
        }
        top?.Sheet.Focus(NavigationMethod.Unspecified);
    }

    /// <summary>False for elements inside a level that is leaving or below the top.</summary>
    private bool IsReachable(Visual element)
    {
        foreach (var entry in _entries)
        {
            if (entry.Sheet.IsVisualAncestorOf(element) || ReferenceEquals(entry.Sheet, element))
            {
                return !entry.Exiting && ReferenceEquals(entry, Top);
            }
        }
        return true;
    }

    #endregion

    internal void Dispose()
    {
        _loop.Dispose();
        _drag.Dispose();
    }
}
