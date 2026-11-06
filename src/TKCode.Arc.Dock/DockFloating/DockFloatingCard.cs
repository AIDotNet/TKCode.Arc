using Avalonia;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Localization;
using TKCode.Arc.Motion;

using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A floating pane where there are no real windows (browser, mobile, <see cref="DockFloatingMode.Overlay"/>; spec 5.5): a
/// raised card with the floating shadow inside the layout, showing one <see cref="DockFloat"/>'s tree under a title bar with
/// Dock and Close buttons. The title bar drags the card (and the layout's compass can take it back), a double click on it
/// docks the card, and the grip in the bottom-right corner resizes it. The card stays inside the layout. It enters like a
/// popup: fading in from 97% scale (reduced motion: the fade only).
/// </summary>
[TemplatePart(TitleBarPart, typeof(Control))]
[TemplatePart(DockButtonPart, typeof(AvaloniaButton))]
[TemplatePart(CloseButtonPart, typeof(AvaloniaButton))]
[TemplatePart(GripPart, typeof(Control))]
internal sealed class DockFloatingCard : ContentControl, IDockFloatHost
{
    public const string TitleBarPart = "PART_TitleBar";
    public const string DockButtonPart = "PART_Dock";
    public const string CloseButtonPart = "PART_Close";
    public const string GripPart = "PART_Grip";

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<DockFloatingCard, string>(nameof(Title), string.Empty);

    private static readonly TimeSpan s_enter = TimeSpan.FromMilliseconds(160);

    private readonly DockLayout _layout;
    private readonly DockFloatTitle _title;
    private readonly ScaleTransform _scale = new();
    private MotionValue<double>? _enter;
    private Control? _titleBar;
    private AvaloniaButton? _dock;
    private AvaloniaButton? _close;
    private Control? _grip;
    private DockDragGesture? _titleGesture;
    private IPointer? _resizing;
    private Point _resizeStart;
    private Size _sizeStart;
    private bool _dismissed;
    private bool _placing;

    public DockFloatingCard(DockLayout layout, DockFloat dock)
    {
        _layout = layout;
        Float = dock;
        _title = new DockFloatTitle(text => Title = text);
        ActivationStamp = DockActivation.Next();
        RenderTransform = _scale;
        RenderTransformOrigin = RelativePoint.Center;
        AddHandler(PointerPressedEvent, (_, _) => BringToFront(), RoutingStrategies.Tunnel, handledEventsToo: true);
        dock.PropertyChanged += OnFloatPropertyChanged;
        SyncContent();
    }

    /// <summary>Bounds set on the model in code move the card.</summary>
    private void OnFloatPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == DockFloat.BoundsProperty && !_placing && !_dismissed && Parent is Control panel)
        {
            Place(panel.Bounds.Size);
        }
    }

    public DockFloat Float { get; }

    DockFloat? IDockHost.Float => Float;

    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public Visual Region => this;

    /// <summary>Cards draw their indicators on the layout's layer, which lies above them.</summary>
    public DockIndicatorLayer? Layer => _layout.IndicatorLayer;

    public IEnumerable<DockGroup> Groups => Float.Root?.SelfAndDescendants().OfType<DockGroup>() ?? [];

    public long ActivationStamp { get; private set; }

    public bool IsWindow => false;

    /// <summary>The title bar (tests).</summary>
    internal Control? TitleBarPartControl => _titleBar;

    internal AvaloniaButton? DockButton => _dock;

    internal AvaloniaButton? CloseButton => _close;

    internal Control? Grip => _grip;

    /// <summary>The card's top-left corner in the floats panel.</summary>
    public Point Position => new(Canvas.GetLeft(this) is var x && double.IsFinite(x) ? x : 0,
        Canvas.GetTop(this) is var y && double.IsFinite(y) ? y : 0);

    public void SyncContent()
    {
        if (Float.Root is { } root)
        {
            var view = _layout.ViewFor(root);
            if (!ReferenceEquals(Content, view))
            {
                Content = null;
                DockLayout.Adopt(view);
                Content = view;
            }
        }
        _title.Follow(Float);
    }

    public void BringToFront()
    {
        ActivationStamp = DockActivation.Next();
        ZIndex = (int)Math.Min(int.MaxValue, ActivationStamp);
    }

    public void Dismiss()
    {
        _dismissed = true;
        Float.PropertyChanged -= OnFloatPropertyChanged;
        _title.Dispose();
        _titleGesture?.Dispose();
        _titleGesture = null;
        Content = null;
        if (Parent is Panel panel)
        {
            panel.Children.Remove(this);
        }
    }

    /// <summary>Places the card from its float's bounds, kept inside <paramref name="area"/> (the floats panel's size).</summary>
    public void Place(Size area)
    {
        var bounds = Float.Bounds;
        var minWidth = Token(DockFloatingWindowTokenKind.MinWidth, 320);
        var minHeight = Token(DockFloatingWindowTokenKind.MinHeight, 240);
        var width = Math.Max(bounds.Width > 0 ? bounds.Width : minWidth, Math.Min(minWidth, area.Width));
        var height = Math.Max(bounds.Height > 0 ? bounds.Height : minHeight, Math.Min(minHeight, area.Height));
        if (area.Width > 0)
        {
            width = Math.Min(width, area.Width);
        }
        if (area.Height > 0)
        {
            height = Math.Min(height, area.Height);
        }
        Width = width;
        Height = height;
        MoveTo(bounds.Position, area);
    }

    /// <summary>Moves the card's top-left corner to <paramref name="position"/>, kept inside <paramref name="area"/>, and records it.</summary>
    public void MoveTo(Point position, Size area)
    {
        var width = double.IsFinite(Width) ? Width : Bounds.Width;
        var height = double.IsFinite(Height) ? Height : Bounds.Height;
        var x = area.Width > 0 ? Math.Clamp(position.X, 0, Math.Max(0, area.Width - width)) : position.X;
        var y = area.Height > 0 ? Math.Clamp(position.Y, 0, Math.Max(0, area.Height - height)) : position.Y;
        Canvas.SetLeft(this, x);
        Canvas.SetTop(this, y);
        _placing = true;
        try
        {
            Float.Bounds = new Rect(x, y, width, height);
        }
        finally
        {
            _placing = false;
        }
    }

    private double Token(DockFloatingWindowTokenKind kind, double fallback)
        => this.TryFindResource(DockFloatingWindowTokens.Key(kind), out var value) && value is double d ? d : fallback;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _titleGesture?.Dispose();
        _titleGesture = null;
        if (_titleBar is not null)
        {
            _titleBar.RemoveHandler(PointerPressedEvent, OnTitlePressed);
        }
        if (_dock is not null)
        {
            _dock.Click -= OnDockClick;
        }
        if (_close is not null)
        {
            _close.Click -= OnCloseClick;
        }
        if (_grip is not null)
        {
            _grip.PointerPressed -= OnGripPressed;
            _grip.PointerMoved -= OnGripMoved;
            _grip.PointerReleased -= OnGripReleased;
        }
        _titleBar = e.NameScope.Find<Control>(TitleBarPart);
        _dock = e.NameScope.Find<AvaloniaButton>(DockButtonPart);
        _close = e.NameScope.Find<AvaloniaButton>(CloseButtonPart);
        _grip = e.NameScope.Find<Control>(GripPart);
        if (_titleBar is not null)
        {
            _titleBar.AddHandler(PointerPressedEvent, OnTitlePressed, RoutingStrategies.Bubble);
            _titleGesture = new DockDragGesture(_titleBar, _layout, OnResolveTitle);
        }
        if (_dock is not null)
        {
            _dock.Click += OnDockClick;
            Avalonia.Automation.AutomationProperties.SetName(_dock, DockText.Get(DockLangResourceKind.Dock));
            Tooltip.SetTip(_dock, DockText.Get(DockLangResourceKind.Dock));
        }
        if (_close is not null)
        {
            _close.Click += OnCloseClick;
            Avalonia.Automation.AutomationProperties.SetName(_close, DockText.Get(DockLangResourceKind.Close));
            Tooltip.SetTip(_close, DockText.Get(DockLangResourceKind.Close));
        }
        if (_grip is not null)
        {
            _grip.PointerPressed += OnGripPressed;
            _grip.PointerMoved += OnGripMoved;
            _grip.PointerReleased += OnGripReleased;
        }
    }

    private DockDragRequest? OnResolveTitle(PointerPressedEventArgs e)
    {
        if (e.Source is Visual source && (IsWithin(source, _dock) || IsWithin(source, _close)))
        {
            return null;
        }
        return _layout.ResolveHostPress(this);
    }

    private static bool IsWithin(Visual source, Visual? part)
        => part is not null && (ReferenceEquals(source, part) || part.IsVisualAncestorOf(source));

    private void OnTitlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount == 2 && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            && e.Source is Visual source && !IsWithin(source, _dock) && !IsWithin(source, _close))
        {
            e.Handled = true;
            _layout.DockFloat(Float);
        }
    }

    private void OnDockClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _layout.DockFloat(Float);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _layout.CloseFloat(Float);
    }

    private void OnGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_grip is null || !e.GetCurrentPoint(_grip).Properties.IsLeftButtonPressed || _layout.IsLocked)
        {
            return;
        }
        _resizing = e.Pointer;
        _resizeStart = e.GetPosition(Parent as Visual ?? this);
        _sizeStart = Bounds.Size;
        e.Pointer.Capture(_grip);
        e.Handled = true;
    }

    private void OnGripMoved(object? sender, PointerEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _resizing) || Parent is not Control panel)
        {
            return;
        }
        var delta = e.GetPosition(panel) - _resizeStart;
        var position = Position;
        var minWidth = Math.Min(Token(DockFloatingWindowTokenKind.MinWidth, 320), panel.Bounds.Width);
        var minHeight = Math.Min(Token(DockFloatingWindowTokenKind.MinHeight, 240), panel.Bounds.Height);
        Width = Math.Clamp(_sizeStart.Width + delta.X, minWidth, Math.Max(minWidth, panel.Bounds.Width - position.X));
        Height = Math.Clamp(_sizeStart.Height + delta.Y, minHeight, Math.Max(minHeight, panel.Bounds.Height - position.Y));
        _placing = true;
        try
        {
            Float.Bounds = new Rect(position, new Size(Width, Height));
        }
        finally
        {
            _placing = false;
        }
        e.Handled = true;
    }

    private void OnGripReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _resizing))
        {
            return;
        }
        _resizing = null;
        e.Pointer.Capture(null);
        e.Handled = true;
        _layout.OnFloatResized();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_dismissed)
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        _enter ??= CreateEnter();
        if (level == ArcMotionLevel.Off)
        {
            _enter.Jump(1);
            return;
        }
        _enter.Jump(0);
        _ = _enter.AnimateTo(1, MotionSpec.Tween(s_enter, ArcEasings.Enter));
    }

    private MotionValue<double> CreateEnter()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += progress =>
        {
            Opacity = progress;
            var scale = ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full ? 0.97 + 0.03 * progress : 1;
            _scale.ScaleX = scale;
            _scale.ScaleY = scale;
        };
        return value;
    }
}
