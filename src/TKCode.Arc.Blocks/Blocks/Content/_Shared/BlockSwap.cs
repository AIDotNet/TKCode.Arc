using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// How a <see cref="BlockSwap"/> changes its content: where the new content starts and how it travels, where the old content
/// goes, and whether the new one waits for the old one to leave (Arc's <c>mode="wait"</c>) or takes its place at once
/// (<c>mode="popLayout"</c>).
/// </summary>
internal sealed class BlockSwapMotion
{
    public double EnterX { get; init; }

    public double EnterY { get; init; }

    /// <summary>Added to <see cref="EnterY"/> as a fraction of the content's height (Arc's percentages).</summary>
    public double EnterYFraction { get; init; }

    public double EnterScale { get; init; } = 1;

    public double EnterBlur { get; init; }

    public double ExitX { get; init; }

    public double ExitY { get; init; }

    public double ExitYFraction { get; init; }

    public double ExitScale { get; init; } = 1;

    public double ExitBlur { get; init; }

    /// <summary>Drives the opacity and blur of the new content.</summary>
    public required MotionSpec EnterFade { get; init; }

    /// <summary>Drives the offset and scale of the new content.</summary>
    public required MotionSpec EnterMove { get; init; }

    /// <summary>Drives everything of the old content.</summary>
    public required MotionSpec Exit { get; init; }

    /// <summary>The old content leaves first; the new one starts after it.</summary>
    public bool Wait { get; init; }

    /// <summary>Plain fade in of the new content under reduced motion.</summary>
    public TimeSpan ReducedEnter { get; init; } = ArcMotionPresets.Instant;

    /// <summary>Plain fade out of the old content under reduced motion.</summary>
    public TimeSpan ReducedExit { get; init; } = ArcMotionPresets.Instant;
}

/// <summary>
/// Swaps one child for another with the exact enter and exit values of a block's source, on separate specs for fade and
/// travel. Only the current child sizes the control; a leaving child keeps its own size outside layout.
/// </summary>
/// <remarks>
/// Code-driven because a ControlTheme cannot keep a removed child alive, sequence an exit before an enter or run the fade and
/// the travel of one child on different specs. <c>ArcContentSwap</c> and <c>SiteSwap</c> have fixed travel per mode and tweens
/// only; these blocks need spring travel, horizontal travel, scale and percent offsets.
/// </remarks>
internal sealed class BlockSwap : Control
{
    private sealed class Part
    {
        public required Control Control { get; init; }
        public required MotionValue<double> Fade { get; init; }
        public required MotionValue<double> Move { get; init; }
        public required TranslateTransform Offset { get; init; }
        public required ScaleTransform Scale { get; init; }
        public double X { get; set; }
        public double Y { get; set; }
        public double YFraction { get; set; }
        public double ScaleFrom { get; set; } = 1;
        public double Blur { get; set; }
        public Size Size { get; set; }
    }

    public static readonly StyledProperty<HorizontalAlignment> HorizontalContentAlignmentProperty =
        ContentControl.HorizontalContentAlignmentProperty.AddOwner<BlockSwap>();

    public static readonly StyledProperty<VerticalAlignment> VerticalContentAlignmentProperty =
        ContentControl.VerticalContentAlignmentProperty.AddOwner<BlockSwap>();

    private readonly List<Part> _exiting = new();
    private Control? _current;
    private Part? _entering;
    private Control? _pending;
    private BlockSwapMotion? _pendingMotion;
    private bool _hasPending;
    private bool _waiting;

    static BlockSwap()
    {
        AffectsMeasure<BlockSwap>(HorizontalContentAlignmentProperty, VerticalContentAlignmentProperty);
        AffectsArrange<BlockSwap>(HorizontalContentAlignmentProperty, VerticalContentAlignmentProperty);
    }

    /// <inheritdoc cref="ContentControl.HorizontalContentAlignmentProperty"/>
    public HorizontalAlignment HorizontalContentAlignment { get => GetValue(HorizontalContentAlignmentProperty); set => SetValue(HorizontalContentAlignmentProperty, value); }

    /// <inheritdoc cref="ContentControl.VerticalContentAlignmentProperty"/>
    public VerticalAlignment VerticalContentAlignment { get => GetValue(VerticalContentAlignmentProperty); set => SetValue(VerticalContentAlignmentProperty, value); }

    /// <summary>The child that is in layout; while a waiting swap runs, the one that will come next.</summary>
    public Control? Current => _hasPending ? _pending : _current;

    /// <summary>How many old children are still leaving.</summary>
    public int ExitingCount => _exiting.Count;

    /// <summary>Puts <paramref name="next"/> in place at once, without motion.</summary>
    public void Set(Control? next)
    {
        Flush();
        if (ReferenceEquals(_current, next))
        {
            return;
        }
        Remove(_current);
        _current = next;
        Add(next);
        // The first content may be a re-used child.
        InvalidateMeasure();
    }

    /// <summary>Changes to <paramref name="next"/> with <paramref name="motion"/>; before attachment and with no old child it just sets it.</summary>
    public void Swap(Control? next, BlockSwapMotion motion)
    {
        if (_hasPending)
        {
            _pending = next;
            _pendingMotion = motion;
            return;
        }
        if (ReferenceEquals(_current, next))
        {
            return;
        }
        // Coming back while it still leaves: it just stays.
        if (next is not null && _exiting.Any(p => ReferenceEquals(p.Control, next)))
        {
            Set(next);
            return;
        }
        if (!this.IsAttachedToVisualTree() || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off || (_current is null && next is null))
        {
            Set(next);
            return;
        }
        var old = _current;
        _current = null;
        if (old is not null)
        {
            StartExit(old, motion);
        }
        if (motion.Wait && old is not null)
        {
            _pending = next;
            _pendingMotion = motion;
            _hasPending = true;
            _waiting = true;
        }
        else
        {
            StartEnter(next, motion);
        }
        InvalidateMeasure();
    }

    private void StartEnter(Control? next, BlockSwapMotion motion)
    {
        _current = next;
        if (next is null)
        {
            return;
        }
        Add(next);
        var part = CreatePart(next, 0, 1);
        var full = ArcMotion.IsFull(this);
        if (full)
        {
            part.X = motion.EnterX;
            part.Y = motion.EnterY;
            part.YFraction = motion.EnterYFraction;
            part.ScaleFrom = motion.EnterScale;
            part.Blur = motion.EnterBlur;
        }
        _entering = part;
        Apply(part);
        _ = RunEnter(part, full, motion);
    }

    private async Task RunEnter(Part part, bool full, BlockSwapMotion motion)
    {
        var fade = part.Fade.AnimateTo(1, full ? motion.EnterFade : MotionSpec.Tween(motion.ReducedEnter, ArcEasings.Standard));
        var move = part.Move.AnimateTo(0, full ? motion.EnterMove : MotionSpec.Instant);
        await Task.WhenAll(fade, move);
        if (ReferenceEquals(_entering, part))
        {
            _entering = null;
            Rest(part.Control);
        }
    }

    private void StartExit(Control old, BlockSwapMotion motion)
    {
        var part = CreatePart(old, 1, 0);
        // An enter that was still running hands over its current look: the exit starts where the child is.
        if (_entering is { } entering && ReferenceEquals(entering.Control, old))
        {
            part.Fade.Jump(entering.Fade.Value);
            _entering = null;
        }
        var full = ArcMotion.IsFull(this);
        if (full)
        {
            part.X = motion.ExitX;
            part.Y = motion.ExitY;
            part.YFraction = motion.ExitYFraction;
            part.ScaleFrom = motion.ExitScale;
            part.Blur = motion.ExitBlur;
        }
        part.Size = old.Bounds.Size;
        old.IsHitTestVisible = false;
        _exiting.Add(part);
        _ = RunExit(part, full, motion);
    }

    private async Task RunExit(Part part, bool full, BlockSwapMotion motion)
    {
        var spec = full ? motion.Exit : MotionSpec.Tween(motion.ReducedExit, ArcEasings.Standard);
        var fade = part.Fade.AnimateTo(0, spec);
        var move = part.Move.AnimateTo(1, spec);
        await Task.WhenAll(fade, move);
        Finish(part);
    }

    private void Finish(Part part)
    {
        if (!_exiting.Remove(part))
        {
            return;
        }
        Remove(part.Control);
        Rest(part.Control);
        part.Control.IsHitTestVisible = true;
        if (_waiting && _exiting.Count == 0)
        {
            _waiting = false;
            _hasPending = false;
            var pending = _pending;
            var motion = _pendingMotion;
            _pending = null;
            _pendingMotion = null;
            if (motion is not null)
            {
                StartEnter(pending, motion);
            }
        }
        InvalidateMeasure();
    }

    private Part CreatePart(Control control, double fade, double move)
    {
        var offset = new TranslateTransform();
        var scale = new ScaleTransform();
        var transforms = new TransformGroup();
        transforms.Children.Add(scale);
        transforms.Children.Add(offset);
        control.RenderTransform = transforms;
        var part = new Part
        {
            Control = control,
            Fade = new MotionValue<double>(this, fade, MotionVectors.Double),
            Move = new MotionValue<double>(this, move, MotionVectors.Double),
            Offset = offset,
            Scale = scale
        };
        part.Fade.Changed += _ => Apply(part);
        part.Move.Changed += _ => Apply(part);
        return part;
    }

    private static void Apply(Part part)
    {
        var fade = Math.Clamp(part.Fade.Value, 0, 1);
        var move = part.Move.Value;
        part.Control.Opacity = fade;
        part.Offset.X = part.X * move;
        part.Offset.Y = (part.Y + part.YFraction * part.Control.Bounds.Height) * move;
        var scale = 1 + (part.ScaleFrom - 1) * move;
        part.Scale.ScaleX = scale;
        part.Scale.ScaleY = scale;
        part.Control.Effect = ArcBlur.Get(part.Blur * (1 - fade));
    }

    private static void Rest(Control control)
    {
        control.Opacity = 1;
        control.RenderTransform = null;
        control.Effect = null;
    }

    private void Add(Control? control)
    {
        if (control is null)
        {
            return;
        }
        VisualChildren.Add(control);
        LogicalChildren.Add(control);
    }

    private void Remove(Control? control)
    {
        if (control is null)
        {
            return;
        }
        VisualChildren.Remove(control);
        LogicalChildren.Remove(control);
    }

    private void Flush()
    {
        foreach (var part in _exiting)
        {
            Remove(part.Control);
            Rest(part.Control);
            part.Control.IsHitTestVisible = true;
        }
        _exiting.Clear();
        if (_entering is { } entering)
        {
            Rest(entering.Control);
            _entering = null;
        }
        if (_hasPending)
        {
            _hasPending = false;
            _waiting = false;
            _current = _pending;
            Add(_pending);
            _pending = null;
            _pendingMotion = null;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Flush();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = default(Size);
        if (_current is { } current)
        {
            current.Measure(availableSize);
            size = current.DesiredSize;
        }
        foreach (var part in _exiting)
        {
            part.Control.Measure(part.Size);
            // While a waiting swap runs, the leaving child still holds the room.
            if (_current is null)
            {
                size = new Size(Math.Max(size.Width, part.Size.Width), Math.Max(size.Height, part.Size.Height));
            }
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_current is { } current)
        {
            current.Arrange(Place(current.DesiredSize, finalSize));
        }
        foreach (var part in _exiting)
        {
            part.Control.Arrange(Place(part.Size, finalSize));
        }
        return finalSize;
    }

    private Rect Place(Size size, Size room)
    {
        var horizontal = HorizontalContentAlignment;
        var vertical = VerticalContentAlignment;
        var width = horizontal == HorizontalAlignment.Stretch ? room.Width : size.Width;
        var height = vertical == VerticalAlignment.Stretch ? Math.Max(room.Height, size.Height) : size.Height;
        var x = horizontal switch
        {
            HorizontalAlignment.Center => (room.Width - width) / 2,
            HorizontalAlignment.Right => room.Width - width,
            _ => 0
        };
        var y = vertical switch
        {
            VerticalAlignment.Center => (room.Height - height) / 2,
            VerticalAlignment.Bottom => room.Height - height,
            _ => 0
        };
        return new Rect(x, y, width, height);
    }
}
