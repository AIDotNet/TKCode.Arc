using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Metadata;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Mobile;

/// <summary>What a <see cref="NavigationStack"/> did.</summary>
public enum NavigationKind
{
    /// <summary>A page was pushed on the stack.</summary>
    Push,

    /// <summary>The top page was popped off the stack.</summary>
    Pop,
}

/// <summary>Raised when a <see cref="NavigationStack"/> pushes or pops a page.</summary>
public sealed class NavigationEventArgs : EventArgs
{
    internal NavigationEventArgs(NavigationKind kind, Control page)
    {
        Kind = kind;
        Page = page;
    }

    /// <summary>Whether a page was pushed or popped.</summary>
    public NavigationKind Kind { get; }

    /// <summary>The page that was pushed, or the page that is on top after a pop.</summary>
    public Control Page { get; }
}

/// <summary>
/// A stack of pages, one on top. <see cref="Push"/> slides a page in from the right over the one below, which shifts
/// left and dims; <see cref="Pop"/> and the back swipe from the left edge slide it away again. The swipe follows the
/// finger, and on release the finger's speed decides whether the page leaves or springs back. Pages are usually
/// <see cref="Page"/>s; only the top one is in the visual tree while the stack is at rest, and a page that is not on top
/// keeps its state. Bind a <see cref="Navbar"/>'s <c>BackCommand</c> to <see cref="BackCommand"/>.
/// <para>
/// The slide is a <see cref="MotionValue{T}"/>, not a ControlTheme transition: the swipe writes it on every move and a
/// release hands the finger's velocity to the spring. Under reduced motion pages change at once.
/// </para>
/// </summary>
[TemplatePart("PART_StaticLayer", typeof(Panel))]
[TemplatePart("PART_Dim", typeof(Border))]
[TemplatePart("PART_TopLayer", typeof(Panel))]
public class NavigationStack : TemplatedControl
{
    /// <summary>How far the page underneath shifts left while the top page covers it, as a fraction of the width: 0.3.</summary>
    public const double ParallaxFraction = 0.3;

    /// <summary>The back swipe starts only from this many px at the left edge: 24.</summary>
    public const double EdgeWidth = 24;

    /// <summary>A release coasts this long at its speed before it is decided whether the page leaves.</summary>
    private static readonly TimeSpan Projection = TimeSpan.FromSeconds(0.2);

    /// <summary>A rightward release faster than this (px/s) pops the page even if it was not dragged far.</summary>
    private const double FlickVelocity = 500;

    /// <summary>The first page of the stack. Setting it resets the stack to that one page.</summary>
    public static readonly StyledProperty<Control?> RootProperty =
        AvaloniaProperty.Register<NavigationStack, Control?>(nameof(Root));

    /// <summary>Whether a swipe from the left edge pops the top page: true.</summary>
    public static readonly StyledProperty<bool> IsSwipeBackEnabledProperty =
        AvaloniaProperty.Register<NavigationStack, bool>(nameof(IsSwipeBackEnabled), true);

    /// <summary>Whether there is a page to go back to.</summary>
    public static readonly DirectProperty<NavigationStack, bool> CanGoBackProperty =
        AvaloniaProperty.RegisterDirect<NavigationStack, bool>(nameof(CanGoBack), s => s.CanGoBack);

    private enum Transition
    {
        None,

        /// <summary>A pushed page is sliding in.</summary>
        Push,

        /// <summary>The top page, already removed from the stack, is sliding out.</summary>
        Pop,

        /// <summary>A swipe is in progress or springing back; the stack has not changed.</summary>
        Peek,
    }

    private readonly List<Control> _stack = [];
    private readonly TranslateTransform _topShift = new();
    private readonly TranslateTransform _staticShift = new();
    private readonly BackCommandImpl _backCommand;
    private Panel? _staticLayer;
    private Border? _dim;
    private Panel? _topLayer;
    private MotionValue<double>? _x;
    private DragTracker? _drag;
    private Transition _transition;
    private Control? _under;
    private bool _canGoBack;

    public NavigationStack()
    {
        _backCommand = new BackCommandImpl(this);
    }

    /// <inheritdoc cref="RootProperty"/>
    [Content]
    public Control? Root
    {
        get => GetValue(RootProperty);
        set => SetValue(RootProperty, value);
    }

    /// <inheritdoc cref="IsSwipeBackEnabledProperty"/>
    public bool IsSwipeBackEnabled
    {
        get => GetValue(IsSwipeBackEnabledProperty);
        set => SetValue(IsSwipeBackEnabledProperty, value);
    }

    /// <inheritdoc cref="CanGoBackProperty"/>
    public bool CanGoBack => _canGoBack;

    /// <summary>How many pages the stack holds.</summary>
    public int Depth => _stack.Count;

    /// <summary>The page on top, or null when the stack is empty.</summary>
    public Control? Top => _stack.Count == 0 ? null : _stack[^1];

    /// <summary>A command that pops the top page; it can run only while <see cref="CanGoBack"/>.</summary>
    public ICommand BackCommand => _backCommand;

    /// <summary>Raised when a page is pushed or popped, as soon as the stack changes (the slide may still be running).</summary>
    public event EventHandler<NavigationEventArgs>? Navigated;

    protected override Type StyleKeyOverride => typeof(NavigationStack);

    /// <summary>Slides <paramref name="page"/> in from the right on top of the stack.</summary>
    public void Push(Control page)
    {
        ArgumentNullException.ThrowIfNull(page);
        Settle();
        var previous = Top;
        _stack.Add(page);
        UpdateCanGoBack();
        Navigated?.Invoke(this, new NavigationEventArgs(NavigationKind.Push, page));
        if (_topLayer is null || _staticLayer is null || _x is null)
        {
            return;
        }
        if (previous is not null)
        {
            _topLayer.Children.Remove(previous);
            _staticLayer.Children.Add(previous);
        }
        _topLayer.Children.Add(page);
        _transition = Transition.Push;
        _x.Jump(Width());
        Run(0, null, Transition.Push);
    }

    /// <summary>Slides the top page out to the right. Returns false when there is no page to go back to.</summary>
    public bool Pop()
    {
        Settle();
        if (_stack.Count < 2)
        {
            return false;
        }
        if (_topLayer is null || _staticLayer is null || _x is null)
        {
            _stack.RemoveAt(_stack.Count - 1);
            UpdateCanGoBack();
            Navigated?.Invoke(this, new NavigationEventArgs(NavigationKind.Pop, Top!));
            return true;
        }
        var under = _stack[^2];
        _staticLayer.Children.Add(under);
        _under = under;
        _x.Jump(0);
        CommitPop(null);
        return true;
    }

    /// <summary>Pops every page above the first. Returns false when only the first is left.</summary>
    public bool PopToRoot()
    {
        Settle();
        if (_stack.Count < 2)
        {
            return false;
        }
        // The pages in between leave without a slide; only the top one is seen going.
        for (var i = _stack.Count - 2; i > 0; i--)
        {
            _stack.RemoveAt(i);
        }
        return Pop();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _x?.Dispose();
        _x = null;
        _staticLayer = e.NameScope.Find<Panel>("PART_StaticLayer");
        _dim = e.NameScope.Find<Border>("PART_Dim");
        _topLayer = e.NameScope.Find<Panel>("PART_TopLayer");
        if (_topLayer is null || _staticLayer is null)
        {
            return;
        }
        _topLayer.RenderTransform = _topShift;
        _staticLayer.RenderTransform = _staticShift;
        _x = new MotionValue<double>(_topLayer, 0, MotionVectors.Double);
        _x.Changed += _ => Apply();
        _transition = Transition.None;
        _under = null;
        _staticLayer.Children.Clear();
        _topLayer.Children.Clear();
        if (Top is { } top)
        {
            _topLayer.Children.Add(top);
        }
        Apply();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _drag = new DragTracker(this, new DragOptions { Axis = DragAxis.Horizontal, Threshold = 8, HandledEventsToo = true })
        {
            CanBegin = args => IsSwipeBackEnabled && _stack.Count > 1 && _transition == Transition.None
                               && args.GetPosition(this).X <= EdgeWidth,
        };
        _drag.Started += (_, _) => BeginPeek();
        _drag.Moved += (_, args) => MovePeek(args.Offset.X);
        _drag.Completed += (_, args) => EndPeek(args.Velocity.X);
        _drag.Cancelled += (_, _) => EndPeek(0);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _drag?.Dispose();
        _drag = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RootProperty)
        {
            ResetTo(change.GetNewValue<Control?>());
        }
    }

    private bool Reduced => ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full;

    private double Width() => Bounds.Width;

    private void ResetTo(Control? root)
    {
        Settle();
        _topLayer?.Children.Clear();
        _staticLayer?.Children.Clear();
        _stack.Clear();
        if (root is not null)
        {
            _stack.Add(root);
            _topLayer?.Children.Add(root);
        }
        UpdateCanGoBack();
    }

    private void UpdateCanGoBack()
    {
        var value = _stack.Count > 1;
        SetAndRaise(CanGoBackProperty, ref _canGoBack, value);
        _backCommand.Raise();
    }

    /// <summary>Puts the layers where the slide is: the top page at x, the one underneath shifted left and dimmed.</summary>
    private void Apply()
    {
        if (_x is null)
        {
            return;
        }
        var width = Width();
        if (_transition == Transition.None || width <= 0)
        {
            _topShift.X = 0;
            _staticShift.X = 0;
            if (_dim is not null)
            {
                _dim.Opacity = 0;
            }
            return;
        }
        var x = Math.Clamp(_x.Value, 0, width);
        _topShift.X = x;
        _staticShift.X = -ParallaxFraction * (width - x);
        if (_dim is not null)
        {
            _dim.Opacity = 1 - x / width;
        }
    }

    /// <summary>Starts the slide to <paramref name="target"/> (0 or the width) and finishes the transition when it lands.</summary>
    private async void Run(double target, double? velocity, Transition kind)
    {
        if (_x is null)
        {
            return;
        }
        if (Reduced || Width() <= 0)
        {
            _x.Jump(target);
            Complete();
            return;
        }
        var landed = await _x.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth), velocity);
        // An interrupted slide was finished by whatever interrupted it.
        if (landed && _transition == kind)
        {
            Complete();
        }
    }

    /// <summary>Finishes a running slide at once, for a new push or pop that arrives before it lands.</summary>
    private void Settle()
    {
        if (_transition == Transition.None)
        {
            return;
        }
        _x?.Stop();
        Complete();
    }

    /// <summary>The slide landed: the layers go back to a single page on top.</summary>
    private void Complete()
    {
        var kind = _transition;
        _transition = Transition.None;
        if (kind == Transition.Pop && _under is { } under && _topLayer is not null && _staticLayer is not null)
        {
            _topLayer.Children.Clear();
            _staticLayer.Children.Remove(under);
            _topLayer.Children.Add(under);
        }
        _staticLayer?.Children.Clear();
        _under = null;
        Apply();
    }

    /// <summary>The stack drops its top page and that page slides out from where it is.</summary>
    private void CommitPop(double? velocity)
    {
        _stack.RemoveAt(_stack.Count - 1);
        _transition = Transition.Pop;
        UpdateCanGoBack();
        Navigated?.Invoke(this, new NavigationEventArgs(NavigationKind.Pop, Top!));
        Run(Width(), velocity, Transition.Pop);
    }

    private void BeginPeek()
    {
        if (_x is null || _staticLayer is null || _stack.Count < 2)
        {
            return;
        }
        Settle();
        var under = _stack[^2];
        _staticLayer.Children.Add(under);
        _under = under;
        _transition = Transition.Peek;
        _x.Jump(0);
    }

    private void MovePeek(double offset)
    {
        if (_transition == Transition.Peek && _x is not null)
        {
            _x.Jump(Math.Clamp(offset, 0, Width()));
        }
    }

    private void EndPeek(double velocity)
    {
        if (_transition != Transition.Peek || _x is null)
        {
            return;
        }
        var width = Width();
        Span<double> points = [0, width];
        var target = MomentumProjector.SelectSnapPoint(points, _x.Value, velocity,
            SnapProjection.Linear(Projection, 0, FlickVelocity));
        if (target == 1)
        {
            CommitPop(velocity);
        }
        else
        {
            Run(0, velocity, Transition.Peek);
        }
    }

    private sealed class BackCommandImpl(NavigationStack owner) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => owner.CanGoBack;

        public void Execute(object? parameter) => owner.Pop();

        public void Raise() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
