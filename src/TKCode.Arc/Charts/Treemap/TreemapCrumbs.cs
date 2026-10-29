using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Breadcrumb of a <see cref="Treemap"/>: the path from the root to the node in view. Crumbs slide in from the left
/// and settle on the smooth spring; the last one is the current level. One tab stop: ← / → move between crumbs,
/// Enter / Space zoom out to the focused one.
/// </summary>
internal sealed class TreemapCrumbs : Avalonia.Controls.Control, Avalonia.Rendering.ICustomHitTest
{
    /// <summary>The whole box takes the pointer, not only the painted marks.</summary>
    bool Avalonia.Rendering.ICustomHitTest.HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    private const double CrumbHeight = 32;
    private const double ButtonPadding = 6;
    private const double SeparatorPadding = 4;

    private readonly Treemap _owner;
    private readonly List<Crumb> _crumbs = new();
    private List<Crumb> _live = new();
    private int _focus;
    private int _hover = -1;
    private int _version = -1;

    private sealed class Crumb
    {
        public required string Id;
        public required MotionValue<double> X;
        public required MotionValue<double> Presence;
        public FormattedText? Text;
        public FormattedText? Separator;
        public bool IsCurrent;
        public bool Leaving;
        public double Width;
    }

    public TreemapCrumbs(Treemap owner)
    {
        _owner = owner;
        AutomationProperties.SetLandmarkType(this, AutomationLandmarkType.Navigation);
    }

    internal IReadOnlyList<string> Trail => _crumbs.Where(c => !c.Leaving).Select(c => c.Id).ToList();

    public void InvalidateText()
    {
        _version = -1;
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Rebuilds the trail: kept crumbs glide to their new place, new ones enter, removed ones fade out.</summary>
    public void OnTrailChanged()
    {
        var trail = new List<TreemapFlat>();
        for (var at = _owner.Center; ; at = _owner.Flat[at.Parent!])
        {
            trail.Insert(0, at);
            if (at.Parent is null || !_owner.Flat.ContainsKey(at.Parent))
            {
                break;
            }
        }
        var animate = IsLoaded && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
        var full = animate && ArcMotion.IsFull(this);
        var next = new List<Crumb>();
        foreach (var node in trail)
        {
            var crumb = _crumbs.FirstOrDefault(c => c.Id == node.Id && !c.Leaving);
            if (crumb is null)
            {
                crumb = new Crumb { Id = node.Id, X = Value(0), Presence = Value(animate ? 0 : 1) };
                crumb.X.Jump(double.NaN);
                if (animate)
                {
                    _ = crumb.Presence.AnimateTo(1, full ? MotionSpec.Spring(ArcSpring.Smooth) : MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
                }
            }
            next.Add(crumb);
        }
        foreach (var gone in _crumbs.Where(c => !next.Contains(c)))
        {
            gone.Leaving = true;
            if (animate)
            {
                var crumb = gone;
                _ = gone.Presence.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Exit)).ContinueWith(_ =>
                    Dispatcher.Post(() =>
                    {
                        _crumbs.Remove(crumb);
                        InvalidateVisual();
                    }), TaskScheduler.Default);
                next.Add(gone);
            }
        }
        _crumbs.Clear();
        _crumbs.AddRange(next);
        _live = _crumbs.Where(c => !c.Leaving).ToList();
        _focus = Math.Clamp(_focus, 0, Math.Max(0, ButtonCount - 1));
        Focusable = ButtonCount > 0;
        _version = -1;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private int ButtonCount => Math.Max(0, _crumbs.Count(c => !c.Leaving) - 1);

    private MotionValue<double> Value(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private void EnsureText()
    {
        var p = _owner.Palette;
        if (_version == p.Version)
        {
            return;
        }
        _version = p.Version;
        var live = _crumbs.Where(c => !c.Leaving).ToList();
        for (var i = 0; i < live.Count; i++)
        {
            var crumb = live[i];
            crumb.IsCurrent = i == live.Count - 1;
            var label = _owner.Flat.TryGetValue(crumb.Id, out var node) ? node.Label : crumb.Id;
            crumb.Text = ChartsBText.Make(label, p, p.TypeSM, crumb.IsCurrent ? p.Foreground : p.TextSecondary,
                crumb.IsCurrent ? FontWeight.Medium : FontWeight.Normal);
            crumb.Separator = i > 0 ? ChartsBText.Make("/", p, p.TypeSM, p.BorderStrong) : null;
        }
        // Lay crumbs out left to right; existing crumbs glide to their new place on the smooth spring.
        var x = 0.0;
        var full = IsLoaded && ArcMotion.IsFull(this);
        foreach (var crumb in live)
        {
            if (crumb.Separator is { } separator)
            {
                x += separator.WidthIncludingTrailingWhitespace + SeparatorPadding * 2;
            }
            crumb.Width = crumb.Text!.WidthIncludingTrailingWhitespace + (crumb.IsCurrent ? SeparatorPadding * 2 : ButtonPadding * 2 - 4);
            if (double.IsNaN(crumb.X.Value) || !full)
            {
                crumb.X.Jump(x);
            }
            else if (Math.Abs(crumb.X.Target - x) > 0.1)
            {
                _ = crumb.X.AnimateTo(x, MotionSpec.Spring(ArcSpring.Smooth));
            }
            x += crumb.Width;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureText();
        var width = _crumbs.Where(c => !c.Leaving).Sum(c => c.Width + (c.Separator?.WidthIncludingTrailingWhitespace ?? 0) + (c.Separator is null ? 0 : SeparatorPadding * 2));
        return new Size(double.IsInfinity(availableSize.Width) ? width : Math.Min(width, availableSize.Width), CrumbHeight);
    }

    private int ButtonAt(Point point)
    {
        var live = _crumbs.Where(c => !c.Leaving).ToList();
        for (var i = 0; i < live.Count - 1; i++)
        {
            var x = live[i].X.Value;
            if (point.X >= x && point.X <= x + live[i].Width && point.Y >= 0 && point.Y <= CrumbHeight)
            {
                return i;
            }
        }
        return -1;
    }

    private void Activate(int index)
    {
        var live = _crumbs.Where(c => !c.Leaving).ToList();
        if (index >= 0 && index < live.Count - 1)
        {
            _owner.Stage.ClearActive();
            _owner.ZoomTo(live[index].Id);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var hover = e.Pointer.Type == PointerType.Mouse ? ButtonAt(e.GetPosition(this)) : -1;
        if (hover != _hover)
        {
            _hover = hover;
            Cursor = hover >= 0 ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = -1;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var index = ButtonAt(e.GetPosition(this));
        if (index >= 0)
        {
            e.Handled = true;
            _focus = index;
            Activate(index);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || ButtonCount == 0)
        {
            return;
        }
        int? next = e.Key switch
        {
            Key.Left => _focus - 1,
            Key.Right => _focus + 1,
            Key.Home => 0,
            Key.End => ButtonCount - 1,
            _ => null
        };
        if (next is { } target)
        {
            e.Handled = true;
            _focus = Math.Clamp(target, 0, ButtonCount - 1);
            InvalidateVisual();
        }
        else if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            Activate(_focus);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CrumbsPeer(this);

    public override void Render(DrawingContext context)
    {
        EnsureText();
        var p = _owner.Palette;
        var live = _live;
        foreach (var crumb in _crumbs)
        {
            if (crumb.Text is null)
            {
                continue;
            }
            var presence = ChartsBMath.Clamp(crumb.Presence.Value, 0, 1);
            if (presence <= 0.001)
            {
                continue;
            }
            var index = live.IndexOf(crumb);
            var x = crumb.X.Value - (ArcMotion.IsFull(this) ? 6 * (1 - presence) : 0);
            using (context.PushOpacity(presence))
            {
                if (crumb.Separator is { } separator)
                {
                    var sx = x - separator.WidthIncludingTrailingWhitespace - SeparatorPadding;
                    context.DrawText(separator, new Point(sx, (CrumbHeight - separator.Height) / 2));
                }
                var box = new Rect(x, 0, crumb.Width, CrumbHeight);
                if (!crumb.IsCurrent && index >= 0)
                {
                    if (index == _hover)
                    {
                        context.DrawRectangle(p.Brush(ChartsBMath.Alpha(p.Foreground, 0.05)), null, new RoundedRect(box, 10));
                        crumb.Text.SetForegroundBrush(p.Brush(p.Foreground));
                    }
                    else
                    {
                        crumb.Text.SetForegroundBrush(p.Brush(p.TextSecondary));
                    }
                    if (index == _focus && IsFocused && Classes.Contains(":focus-visible"))
                    {
                        context.DrawRectangle(null, p.Pen(p.FocusRing, 2), new RoundedRect(box.Inflate(1), 11));
                    }
                }
                var pad = crumb.IsCurrent ? SeparatorPadding : ButtonPadding - 2;
                context.DrawText(crumb.Text, new Point(x + pad, (CrumbHeight - crumb.Text.Height) / 2));
            }
        }
    }

    private sealed class CrumbsPeer(TreemapCrumbs owner) : ControlAutomationPeer(owner)
    {
        private TreemapCrumbs Crumbs => (TreemapCrumbs)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => $"{Crumbs._owner.Label} path";

        protected override string? GetHelpTextCore()
            => string.Join(" / ", Crumbs.Trail.Select(id => Crumbs._owner.Flat.TryGetValue(id, out var n) ? n.Label : id)) +
               ". Current: " + Crumbs._owner.Center.Label;
    }
}
