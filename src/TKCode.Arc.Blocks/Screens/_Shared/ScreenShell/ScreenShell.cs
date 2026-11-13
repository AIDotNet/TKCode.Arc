using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The app frame shared by full-screen blocks (Arc screen shell, design reference: notes month-calendar §2.1, §5, §8). A
/// sidebar on the page background, a main card (header + body) and an optional side panel card, 8 apart:
/// <list type="bullet">
/// <item>From 900 wide the sidebar is docked (248, or a 56 rail when collapsed); below it is a 288 sheet from the left opened
/// by the header's menu button, over a scrim.</item>
/// <item>From 1080 wide the panel is docked beside the main card (<see cref="PanelWidth"/>, 300–460, resizable from its left
/// edge); below it is a sheet from the right (380, the full width on phones).</item>
/// <item>At 560 or less (phones) the main card fills the shell without margins, border or radius.</item>
/// </list>
/// The docked widths move on Arc's smooth spring (stiffness 300, damping 35) while the panel fades in 150 ms; sheets slide
/// in 200 ms on the standard curve with the scrim. Reduced and Off motion jump. The screen fills the parts and listens to
/// <see cref="LayoutChanged"/>; the shell owns the header, the toast and the dialog layer.
/// Internal for now: screens adopt it one by one.
/// </summary>
internal sealed class ScreenShell : Control
{
    public const double SidebarWidth = 248;
    public const double RailWidth = 56;
    public const double SheetSidebarWidth = 288;
    public const double SheetPanelWidth = 380;
    public const double Gap = 8;
    public const double MinPanelWidth = 300;
    public const double MaxPanelWidth = 460;
    public const double PanelDockWidth = 1080;
    public const double SidebarDockWidth = 900;
    public const double PhoneWidth = 560;

    private static readonly SpringSpec s_smooth = SpringSpec.FromPhysical(300, 35);
    private static readonly MotionSpec s_sheet = MotionSpec.Tween(TimeSpan.FromMilliseconds(200), ArcEasings.Standard);

    private readonly Border _sidebarHost = new() { ClipToBounds = true, Padding = new Thickness(8) };
    private readonly Border _mainCard = new() { ClipToBounds = true };
    private readonly DockPanel _mainStack = new();
    private readonly Border _bodyHost = new();
    private readonly Border _panelCard = new() { ClipToBounds = true };
    private readonly Border _panelInner = new() { HorizontalAlignment = HorizontalAlignment.Left };
    private readonly ScreenResizeHandle _handle;
    private readonly Border _scrim = new() { IsVisible = false, Opacity = 0 };
    private readonly Panel _toastLayer = new() { IsHitTestVisible = true };
    private readonly Panel _dialogLayer = new() { IsVisible = false };
    private readonly MotionValue<double> _collapse;
    private readonly MotionValue<double> _panelOpen;
    private readonly MotionValue<double> _panelFade;
    private readonly MotionValue<double> _sidebarSheet;
    private readonly MotionValue<double> _panelSheet;
    private ScreenPaint? _paint;
    private Control? _sidebar;
    private Control? _panel;
    private bool _isPanelOpen = true;
    private bool _isSidebarCollapsed;
    private bool _isSidebarSheetOpen;
    private bool _isPanelSheetOpen;
    private double _panelWidth = 340;
    private bool _panelDocked = true;
    private bool _sidebarDocked = true;
    private bool _phone;
    private bool _hasLaidOut;
    private ScreenToast? _toast;

    public ScreenShell()
    {
        Header = new ScreenHeader();
        _mainStack.LastChildFill = true;
        DockPanel.SetDock(Header, Dock.Top);
        _mainStack.Children.Add(Header);
        _mainStack.Children.Add(_bodyHost);
        _mainCard.Child = _mainStack;
        _panelCard.Child = _panelInner;
        _handle = new ScreenResizeHandle(this);
        _scrim.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            ScrimPressed?.Invoke();
        };
        _collapse = new MotionValue<double>(this, 0, MotionVectors.Double);
        _panelOpen = new MotionValue<double>(this, 1, MotionVectors.Double);
        _panelFade = new MotionValue<double>(this, 1, MotionVectors.Double);
        _sidebarSheet = new MotionValue<double>(this, 0, MotionVectors.Double);
        _panelSheet = new MotionValue<double>(this, 0, MotionVectors.Double);
        _collapse.Changed += v =>
        {
            SidebarCollapseChanged?.Invoke(v);
            InvalidateArrange();
        };
        _panelOpen.Changed += _ => InvalidateArrange();
        _panelFade.Changed += v => _panelCard.Opacity = _panelDocked ? Math.Clamp(v, 0, 1) : 1;
        _sidebarSheet.Changed += _ => { UpdateScrim(); InvalidateArrange(); };
        _panelSheet.Changed += _ => { UpdateScrim(); InvalidateArrange(); };

        foreach (var child in new Control[] { _sidebarHost, _mainCard, _panelCard, _handle, _scrim, _toastLayer, _dialogLayer })
        {
            VisualChildren.Add(child);
            LogicalChildren.Add(child);
        }
        AutomationProperties.SetAccessibilityView(_scrim, AccessibilityView.Raw);
        ClipToBounds = true;
        ActualThemeVariantChanged += (_, _) => ApplyPaint();
    }

    /// <summary>The header of the main card.</summary>
    public ScreenHeader Header { get; }

    /// <summary>Raised when the docked / sheet / phone state changes (after a width change).</summary>
    public event Action? LayoutChanged;

    /// <summary>Raised every frame of the sidebar collapse (0 expanded, 1 rail).</summary>
    public event Action<double>? SidebarCollapseChanged;

    /// <summary>Raised when the scrim is pressed (the screen closes its sheets).</summary>
    public event Action? ScrimPressed;

    /// <summary>Raised when the user resizes the panel.</summary>
    public event Action<double>? PanelWidthChanged;

    /// <summary>The sidebar content.</summary>
    public Control? Sidebar
    {
        get => _sidebar;
        set
        {
            _sidebar = value;
            _sidebarHost.Child = value;
        }
    }

    /// <summary>The main card's body.</summary>
    public Control? Body
    {
        get => _bodyHost.Child;
        set => _bodyHost.Child = value;
    }

    /// <summary>The panel content (laid out at <see cref="PanelWidth"/> even while the docked width moves).</summary>
    public Control? Panel
    {
        get => _panel;
        set
        {
            _panel = value;
            _panelInner.Child = value;
        }
    }

    /// <summary>The panel card (its automation name is set by the screen).</summary>
    public Border PanelCard => _panelCard;

    /// <summary>The sidebar column (its automation name is set by the screen).</summary>
    public Border SidebarHost => _sidebarHost;

    /// <summary>The resize handle (its automation name is set by the screen).</summary>
    public Control ResizeHandle => _handle;

    public bool IsPanelDocked => _panelDocked;

    public bool IsSidebarDocked => _sidebarDocked;

    public bool IsPhone => _phone;

    /// <summary>The main card's current width.</summary>
    public double MainWidth => _mainCard.Bounds.Width;

    /// <summary>Whether the docked panel shows (it is the user's wish; sheets use <see cref="IsPanelSheetOpen"/>).</summary>
    public bool IsPanelOpen
    {
        get => _isPanelOpen;
        set
        {
            if (_isPanelOpen == value)
            {
                return;
            }
            _isPanelOpen = value;
            Animate(_panelOpen, value ? 1 : 0, MotionSpec.Spring(s_smooth));
            Animate(_panelFade, value ? 1 : 0, MotionSpec.Tween(TimeSpan.FromMilliseconds(150), ArcEasings.Standard));
        }
    }

    public bool IsSidebarCollapsed
    {
        get => _isSidebarCollapsed;
        set
        {
            if (_isSidebarCollapsed == value)
            {
                return;
            }
            _isSidebarCollapsed = value;
            Animate(_collapse, value ? 1 : 0, MotionSpec.Spring(s_smooth));
        }
    }

    public bool IsSidebarSheetOpen
    {
        get => _isSidebarSheetOpen;
        set
        {
            if (_isSidebarSheetOpen == value)
            {
                return;
            }
            _isSidebarSheetOpen = value;
            Animate(_sidebarSheet, value ? 1 : 0, s_sheet);
        }
    }

    public bool IsPanelSheetOpen
    {
        get => _isPanelSheetOpen;
        set
        {
            if (_isPanelSheetOpen == value)
            {
                return;
            }
            _isPanelSheetOpen = value;
            Animate(_panelSheet, value ? 1 : 0, s_sheet);
        }
    }

    /// <summary>The docked panel's width (clamped to 300–460).</summary>
    public double PanelWidth
    {
        get => _panelWidth;
        set
        {
            var v = Math.Clamp(value, MinPanelWidth, MaxPanelWidth);
            if (Math.Abs(v - _panelWidth) < 0.01)
            {
                return;
            }
            _panelWidth = v;
            _handle.Refresh();
            InvalidateMeasure();
            InvalidateArrange();
        }
    }

    internal void ResizeTo(double width)
    {
        PanelWidth = width;
        PanelWidthChanged?.Invoke(_panelWidth);
    }

    private void Animate(MotionValue<double> value, double target, MotionSpec spec)
    {
        if (!_hasLaidOut || !ArcMotion.IsFull(this) && spec is MotionSpec.SpringMotion)
        {
            value.Jump(target);
            return;
        }
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            value.Jump(target);
            return;
        }
        _ = value.AnimateTo(target, spec);
    }

    private void UpdateScrim()
    {
        var t = Math.Max(_sidebarDocked ? 0 : _sidebarSheet.Value, _panelDocked ? 0 : _panelSheet.Value);
        _scrim.Opacity = Math.Clamp(t, 0, 1);
        _scrim.IsVisible = t > 0.001;
        _scrim.IsHitTestVisible = (_isSidebarSheetOpen && !_sidebarDocked) || (_isPanelSheetOpen && !_panelDocked);
    }

    // Toasts and dialogs -----------------------------------------------------------------------------------------------

    /// <summary>Shows a toast over the bottom of the main card (replacing the shown one).</summary>
    public ScreenToast ShowToast(string text, string? action, Action? onAction, string dismissName)
    {
        _toast?.Leave();
        var toast = new ScreenToast(Paint, text, action, onAction, dismissName);
        toast.Left += t =>
        {
            _toastLayer.Children.Remove(t);
            if (ReferenceEquals(_toast, t))
            {
                _toast = null;
            }
        };
        _toast = toast;
        _toastLayer.Children.Add(toast);
        return toast;
    }

    /// <summary>The toast showing, if any.</summary>
    public ScreenToast? Toast => _toast;

    /// <summary>Shows <paramref name="content"/> centred over a light scrim; Escape or the scrim closes it.</summary>
    public void ShowDialog(Control content)
    {
        _dialogLayer.Children.Clear();
        var scrim = new Border { Background = Paint.DialogScrim };
        scrim.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            CloseDialog();
        };
        _dialogLayer.Children.Add(scrim);
        content.HorizontalAlignment = HorizontalAlignment.Center;
        content.VerticalAlignment = VerticalAlignment.Center;
        _dialogLayer.Children.Add(content);
        _dialogLayer.IsVisible = true;
        DialogOpened?.Invoke();
    }

    public bool IsDialogOpen => _dialogLayer.IsVisible;

    public event Action? DialogOpened;

    public event Action? DialogClosed;

    public void CloseDialog()
    {
        if (!_dialogLayer.IsVisible)
        {
            return;
        }
        _dialogLayer.IsVisible = false;
        _dialogLayer.Children.Clear();
        DialogClosed?.Invoke();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Key != Key.Escape)
        {
            return;
        }
        if (IsDialogOpen)
        {
            CloseDialog();
            e.Handled = true;
        }
    }

    // Paint ------------------------------------------------------------------------------------------------------------

    public ScreenPaint Paint => _paint ??= ScreenPaint.For(this);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyPaint();
    }


    public void ApplyPaint()
    {
        _paint = ScreenPaint.For(this);
        var p = _paint;
        _sidebarHost.Background = p.PageBrush;
        _mainCard.Background = p.SurfaceBrush;
        _mainCard.BorderBrush = p.BorderBrush;
        _panelCard.Background = p.SurfaceBrush;
        _panelCard.BorderBrush = p.BorderBrush;
        _scrim.Background = p.Scrim;
        var shadow = new BoxShadows(new BoxShadow { OffsetY = 1, Blur = 2, Color = Color.FromArgb((byte)(p.IsDark ? 46 : 9), 0, 0, 0) });
        _mainCard.BoxShadow = shadow;
        _panelCard.BoxShadow = shadow;
        Header.ApplyPaint(p);
        _handle.Refresh();
        ApplyFrame();
        InvalidateVisual();
    }

    private void ApplyFrame()
    {
        var radius = _phone ? 0 : 12;
        _mainCard.CornerRadius = new CornerRadius(radius);
        _mainCard.BorderThickness = new Thickness(_phone ? 0 : 1);
        _panelCard.CornerRadius = new CornerRadius(_phone && !_panelDocked ? 0 : 12);
        _panelCard.BorderThickness = new Thickness(_phone && !_panelDocked ? 0 : 1);
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Paint.PageBrush, new Rect(Bounds.Size));
    }

    // Layout -----------------------------------------------------------------------------------------------------------

    private void UpdateMode(double width)
    {
        var panelDocked = width >= PanelDockWidth;
        var sidebarDocked = width >= SidebarDockWidth;
        var phone = width <= PhoneWidth;
        if (panelDocked == _panelDocked && sidebarDocked == _sidebarDocked && phone == _phone && _hasLaidOut)
        {
            return;
        }
        var first = !_hasLaidOut;
        _panelDocked = panelDocked;
        _sidebarDocked = sidebarDocked;
        _phone = phone;
        // Crossing a breakpoint never animates.
        _sidebarSheet.Jump(_isSidebarSheetOpen && !sidebarDocked ? 1 : 0);
        _panelSheet.Jump(_isPanelSheetOpen && !panelDocked ? 1 : 0);
        _panelFade.Jump(_isPanelOpen || !panelDocked ? 1 : 0);
        _panelCard.Opacity = 1;
        if (!panelDocked)
        {
            _panelCard.Opacity = 1;
        }
        UpdateScrim();
        ApplyFrame();
        Header.IsMenuVisible = !sidebarDocked;
        _handle.IsVisible = panelDocked;
        _sidebarHost.ZIndex = sidebarDocked ? 0 : 4;
        _panelCard.ZIndex = panelDocked ? 1 : 5;
        _scrim.ZIndex = 3;
        _toastLayer.ZIndex = 6;
        _dialogLayer.ZIndex = 7;
        _handle.ZIndex = 2;
        if (!first)
        {
            LayoutChanged?.Invoke();
        }
    }

    private (double SidebarW, double BodyX, Thickness Pad, double PanelW, double PanelGap, double MainW) Geometry(double w)
    {
        double bodyX;
        Thickness pad;
        if (_sidebarDocked)
        {
            var c = _collapse.Value;
            bodyX = SidebarWidth + (RailWidth - SidebarWidth) * c;
            pad = new Thickness(0, Gap, Gap, Gap);
        }
        else
        {
            bodyX = 0;
            pad = _phone ? new Thickness(0) : new Thickness(6);
        }
        double panelW = 0, panelGap = 0;
        if (_panelDocked)
        {
            var t = Math.Max(0, _panelOpen.Value);
            panelW = _panelWidth * t;
            panelGap = Gap * Math.Min(1, t);
        }
        var mainW = Math.Max(0, w - bodyX - pad.Left - pad.Right - panelW - panelGap);
        return (bodyX, bodyX, pad, panelW, panelGap, mainW);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 1440 : availableSize.Width;
        var h = double.IsInfinity(availableSize.Height) ? 900 : availableSize.Height;
        UpdateMode(w);
        var g = Geometry(w);
        var sidebarW = _sidebarDocked ? g.SidebarW : SheetSidebarWidth;
        _sidebarHost.Measure(new Size(sidebarW, h));
        var mainH = h - g.Pad.Top - g.Pad.Bottom;
        _mainCard.Measure(new Size(g.MainW, mainH));
        Header.Fit(g.MainW, _phone);
        var panelContentW = _panelDocked ? _panelWidth : PanelSheetWidth(w);
        _panelInner.Width = panelContentW;
        _panelCard.Measure(new Size(_panelDocked ? Math.Max(0, g.PanelW) : panelContentW, PanelHeight(h)));
        _handle.Measure(new Size(7, h));
        _scrim.Measure(new Size(w, h));
        _toastLayer.Measure(new Size(g.MainW, mainH));
        _dialogLayer.Measure(new Size(w, h));
        return new Size(w, h);
    }

    private double PanelSheetWidth(double w) => _phone ? w : Math.Min(SheetPanelWidth, w - 16);

    private double PanelHeight(double h) => _panelDocked ? h - 2 * Gap : _phone ? h : h - 2 * Gap;

    protected override Size ArrangeOverride(Size finalSize)
    {
        var w = finalSize.Width;
        var h = finalSize.Height;
        UpdateMode(w);
        var g = Geometry(w);
        if (_sidebarDocked)
        {
            _sidebarHost.Arrange(new Rect(0, 0, g.SidebarW, h));
        }
        else
        {
            var t = _sidebarSheet.Value;
            _sidebarHost.Arrange(new Rect(-SheetSidebarWidth * (1 - t), 0, SheetSidebarWidth, h));
            _sidebarHost.IsVisible = t > 0.001;
        }
        if (_sidebarDocked)
        {
            _sidebarHost.IsVisible = true;
        }
        var mainRect = new Rect(g.BodyX + g.Pad.Left, g.Pad.Top, g.MainW, Math.Max(0, h - g.Pad.Top - g.Pad.Bottom));
        _mainCard.Arrange(mainRect);
        Header.Fit(g.MainW, _phone);
        _toastLayer.Arrange(new Rect(mainRect.X, mainRect.Y, mainRect.Width, mainRect.Height - 12));
        if (_panelDocked)
        {
            var px = mainRect.Right + g.PanelGap;
            var panelRect = new Rect(px, Gap, Math.Max(0, g.PanelW), h - 2 * Gap);
            _panelCard.Arrange(panelRect);
            _panelCard.IsVisible = g.PanelW > 0.5;
            _handle.Arrange(new Rect(px, Gap, 7, h - 2 * Gap));
            _handle.IsVisible = _isPanelOpen && g.PanelW > 0.5;
        }
        else
        {
            var sw = PanelSheetWidth(w);
            var t = _panelSheet.Value;
            var margin = _phone ? 0 : Gap;
            var x = w - sw - margin + (1 - t) * (sw + margin + 16);
            _panelCard.Arrange(new Rect(x, _phone ? 0 : Gap, sw, PanelHeight(h)));
            _panelCard.IsVisible = t > 0.001;
            _handle.IsVisible = false;
            _handle.Arrange(new Rect(0, 0, 0, 0));
        }
        _scrim.Arrange(new Rect(0, 0, w, h));
        _dialogLayer.Arrange(new Rect(0, 0, w, h));
        _hasLaidOut = true;
        return finalSize;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ScreenShellPeer(this);

    private sealed class ScreenShellPeer(ScreenShell owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => false;
    }
}

/// <summary>
/// The 7 wide grip on the docked panel's left edge (role separator, 300–460): dragging resizes, a 2px BorderStrong line
/// shows while hovered or dragged, ArrowLeft widens by 16, ArrowRight narrows, Home goes to the widest and End to the
/// narrowest (as Arc).
/// </summary>
internal sealed class ScreenResizeHandle : Control
{
    private readonly ScreenShell _shell;
    private bool _hover;
    private bool _dragging;
    private double _startX;
    private double _startWidth;

    public ScreenResizeHandle(ScreenShell shell)
    {
        _shell = shell;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.SizeWestEast);
    }

    public void Refresh() => InvalidateVisual();

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _hover = true;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = false;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _dragging = true;
        _startX = e.GetPosition(_shell).X;
        _startWidth = _shell.PanelWidth;
        e.Pointer.Capture(this);
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging)
        {
            var dx = e.GetPosition(_shell).X - _startX;
            _shell.ResizeTo(_startWidth - dx);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragging)
        {
            _dragging = false;
            e.Pointer.Capture(null);
            InvalidateVisual();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var w = _shell.PanelWidth;
        switch (e.Key)
        {
            case Key.Left:
                _shell.ResizeTo(w + 16);
                break;
            case Key.Right:
                _shell.ResizeTo(w - 16);
                break;
            case Key.Home:
                _shell.ResizeTo(ScreenShell.MaxPanelWidth);
                break;
            case Key.End:
                _shell.ResizeTo(ScreenShell.MinPanelWidth);
                break;
            default:
                return;
        }
        e.Handled = true;
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

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (_hover || _dragging || Classes.Contains(":focus-visible"))
        {
            context.FillRectangle(_shell.Paint.StrongBrush, new Rect(0, 12, 2, Math.Max(0, Bounds.Height - 24)));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new HandlePeer(this);

    private sealed class HandlePeer(ScreenResizeHandle owner) : ControlAutomationPeer(owner), Avalonia.Automation.Provider.IRangeValueProvider
    {
        private ScreenResizeHandle Handle => (ScreenResizeHandle)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Separator;

        public bool IsReadOnly => false;

        public double Minimum => ScreenShell.MinPanelWidth;

        public double Maximum => ScreenShell.MaxPanelWidth;

        public double Value => Handle._shell.PanelWidth;

        public double LargeChange => 64;

        public double SmallChange => 16;

        public void SetValue(double value) => Handle._shell.ResizeTo(value);
    }
}
