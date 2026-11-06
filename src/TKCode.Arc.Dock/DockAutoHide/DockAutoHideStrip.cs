using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Localization;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One edge's auto-hide strip (spec 5.4): a column (left, right) or row (top, bottom) of <see cref="DockAutoHideButton"/>s, one
/// per pane in <see cref="DockWorkspace.AutoHidden"/> for that edge. Hidden while the edge has no auto-hidden pane, so it
/// takes no room. The buttons sit on the strip's outer side; the rest of the strip is the gap to the cards.
/// </summary>
internal sealed class DockAutoHideStrip : Control
{
    /// <summary>The edge the strip belongs to.</summary>
    public static readonly StyledProperty<DockSide> SideProperty =
        AvaloniaProperty.Register<DockAutoHideStrip, DockSide>(nameof(Side));

    private readonly List<DockAutoHideButton> _buttons = [];
    private AvaloniaList<DockPane>? _panes;
    private DockLayout? _layout;
    private DockPane? _selected;

    public DockAutoHideStrip()
    {
        IsVisible = false;
    }

    /// <inheritdoc cref="SideProperty"/>
    public DockSide Side { get => GetValue(SideProperty); set => SetValue(SideProperty, value); }

    /// <summary>The buttons in order (tests).</summary>
    internal IReadOnlyList<DockAutoHideButton> Buttons => _buttons;

    /// <summary>The button of a pane, or null.</summary>
    internal DockAutoHideButton? ButtonOf(DockPane pane) => _buttons.FirstOrDefault(b => ReferenceEquals(b.Pane, pane));

    private bool IsVertical => Side is DockSide.Left or DockSide.Right;

    private double Gap => this.TryFindResource(DockAutoHideTokens.Key(DockAutoHideTokenKind.ButtonGap), out var value) && value is double gap ? gap : 4;

    private double Thickness => this.TryFindResource(DockLayoutTokens.Key(DockLayoutTokenKind.StripThickness), out var value) && value is double t ? t : 32;

    /// <summary>Follows <paramref name="panes"/> (an edge list of <paramref name="layout"/>'s workspace); null lets go.</summary>
    public void Attach(DockLayout? layout, AvaloniaList<DockPane>? panes)
    {
        if (_panes is not null)
        {
            _panes.CollectionChanged -= OnPanesChanged;
        }
        _layout = layout;
        _panes = panes;
        if (_panes is not null)
        {
            _panes.CollectionChanged += OnPanesChanged;
        }
        Sync();
    }

    /// <summary>Marks the button of the pane shown in the flyout (null: none).</summary>
    public void SetSelected(DockPane? pane)
    {
        _selected = pane;
        foreach (var button in _buttons)
        {
            button.IsSelected = ReferenceEquals(button.Pane, pane);
        }
    }

    /// <summary>Follows the layout's lock (the buttons' menus).</summary>
    public void Refresh()
    {
        foreach (var button in _buttons)
        {
            button.Refresh();
        }
    }

    private void OnPanesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Sync();

    private void Sync()
    {
        var panes = _layout is null || _panes is null ? [] : _panes.ToList();
        for (var i = _buttons.Count - 1; i >= 0; i--)
        {
            var button = _buttons[i];
            if (!panes.Contains(button.Pane))
            {
                button.Release();
                _buttons.RemoveAt(i);
                VisualChildren.Remove(button);
                LogicalChildren.Remove(button);
            }
        }
        var ordered = new List<DockAutoHideButton>(panes.Count);
        foreach (var pane in panes)
        {
            var button = ButtonOf(pane);
            if (button is null)
            {
                button = new DockAutoHideButton(_layout!, pane) { Side = Side };
                VisualChildren.Add(button);
                LogicalChildren.Add(button);
            }
            button.IsSelected = ReferenceEquals(pane, _selected);
            ordered.Add(button);
        }
        _buttons.Clear();
        _buttons.AddRange(ordered);
        IsVisible = _buttons.Count > 0;
        InvalidateMeasure();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SideProperty)
        {
            foreach (var button in _buttons)
            {
                button.Side = Side;
            }
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var gap = Gap;
        var along = 0.0;
        foreach (var button in _buttons)
        {
            button.Measure(IsVertical ? new Size(availableSize.Width, double.PositiveInfinity) : new Size(double.PositiveInfinity, availableSize.Height));
            along += IsVertical ? button.DesiredSize.Height : button.DesiredSize.Width;
        }
        if (_buttons.Count > 1)
        {
            along += gap * (_buttons.Count - 1);
        }
        var thickness = Thickness;
        return IsVertical ? new Size(thickness, along) : new Size(along, thickness);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var gap = Gap;
        var at = 0.0;
        foreach (var button in _buttons)
        {
            var size = button.DesiredSize;
            Rect rect = Side switch
            {
                DockSide.Left => new Rect(0, at, size.Width, size.Height),
                DockSide.Right => new Rect(finalSize.Width - size.Width, at, size.Width, size.Height),
                DockSide.Top => new Rect(at, 0, size.Width, size.Height),
                _ => new Rect(at, finalSize.Height - size.Height, size.Width, size.Height)
            };
            button.Arrange(rect);
            at += (IsVertical ? size.Height : size.Width) + gap;
        }
        return finalSize;
    }
}

/// <summary>
/// The button of one auto-hidden pane on a strip: its icon and title (turned 90° on the left and right strips), the accent
/// bar on the inner side while its flyout is open. Click, Enter or Space shows the pane; right-click or Shift+F10 opens the
/// pane's menu.
/// </summary>
[TemplatePart(RotatePart, typeof(DockTurnedPresenter))]
[PseudoClasses(SelectedPseudoClass, PressedPseudoClass, VerticalPseudoClass, LeftPseudoClass, RightPseudoClass, TopPseudoClass, BottomPseudoClass)]
internal sealed class DockAutoHideButton : TemplatedControl
{
    public const string RotatePart = "PART_Rotate";
    public const string SelectedPseudoClass = ":selected";
    public const string PressedPseudoClass = ":pressed";
    public const string VerticalPseudoClass = ":vertical";
    public const string LeftPseudoClass = ":left";
    public const string RightPseudoClass = ":right";
    public const string TopPseudoClass = ":top";
    public const string BottomPseudoClass = ":bottom";

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<DockAutoHideButton, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<DockAutoHideButton, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<DockAutoHideButton, bool>(nameof(IsSelected));

    public static readonly StyledProperty<DockSide> SideProperty =
        AvaloniaProperty.Register<DockAutoHideButton, DockSide>(nameof(Side));

    private readonly IDisposable[] _bindings;
    private DockTurnedPresenter? _rotate;
    private LazyFocusRing? _focusRing;

    static DockAutoHideButton()
    {
        FocusableProperty.OverrideDefaultValue<DockAutoHideButton>(true);
    }

    public DockAutoHideButton(DockLayout layout, DockPane pane)
    {
        Layout = layout;
        Pane = pane;
        _bindings =
        [
            Bind(TitleProperty, pane.GetObservable(DockPane.TitleProperty)),
            Bind(IconProperty, pane.GetObservable(DockPane.IconProperty)),
            Bind(Tooltip.TipProperty, pane.GetObservable(Tooltip.TipProperty))
        ];
        pane.PropertyChanged += OnPanePropertyChanged;
        AddHandler(ContextRequestedEvent, OnContextRequested);
        UpdateNames();
    }

    public DockLayout Layout { get; }

    public DockPane Pane { get; }

    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    public DockSide Side { get => GetValue(SideProperty); set => SetValue(SideProperty, value); }

    /// <summary>Stops following the pane (the button is being discarded).</summary>
    public void Release()
    {
        foreach (var binding in _bindings)
        {
            binding.Dispose();
        }
        Pane.PropertyChanged -= OnPanePropertyChanged;
    }

    public void Refresh() => UpdateNames();

    private void OnPanePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == DockPane.TitleProperty)
        {
            UpdateNames();
        }
    }

    private void UpdateNames()
    {
        AutomationProperties.SetName(this, Pane.ToString());
        AutomationProperties.SetHelpText(this, DockText.Get(DockLangResourceKind.AutoHiddenHelp));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(SelectedPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == SideProperty)
        {
            UpdateSide();
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _rotate = e.NameScope.Find<DockTurnedPresenter>(RotatePart);
        UpdateSide();
        _focusRing ??= new LazyFocusRing(this, DockAutoHideTokens.Key(DockAutoHideTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep8));
        _focusRing.Attach(AddRing, RemoveRing);
    }

    /// <summary>Turns the content on the left and right strips; the theme puts the accent bar on the inner side.</summary>
    private void UpdateSide()
    {
        var vertical = Side is DockSide.Left or DockSide.Right;
        PseudoClasses.Set(VerticalPseudoClass, vertical);
        PseudoClasses.Set(LeftPseudoClass, Side == DockSide.Left);
        PseudoClasses.Set(RightPseudoClass, Side == DockSide.Right);
        PseudoClasses.Set(TopPseudoClass, Side == DockSide.Top);
        PseudoClasses.Set(BottomPseudoClass, Side == DockSide.Bottom);
        if (_rotate is not null)
        {
            _rotate.IsTurned = vertical;
        }
    }

    private void AddRing(Control ring)
    {
        LogicalChildren.Add(ring);
        VisualChildren.Add(ring);
        InvalidateMeasure();
    }

    private void RemoveRing(Control ring)
    {
        VisualChildren.Remove(ring);
        LogicalChildren.Remove(ring);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        _focusRing?.Ring?.Measure(availableSize);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        _focusRing?.Ring?.Arrange(new Rect(size));
        return size;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        PseudoClasses.Set(PressedPseudoClass, true);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var wasPressed = PseudoClasses.Contains(PressedPseudoClass);
        PseudoClasses.Set(PressedPseudoClass, false);
        if (wasPressed && e.InitialPressMouseButton == MouseButton.Left && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            Layout.ToggleAutoHidden(Pane, NavigationMethod.Pointer);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        PseudoClasses.Set(PressedPseudoClass, false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Enter or Key.Space when e.KeyModifiers == KeyModifiers.None:
                e.Handled = true;
                Layout.ToggleAutoHidden(Pane, NavigationMethod.Tab);
                return;
            case Key.Apps:
            case Key.F10 when (e.KeyModifiers & KeyModifiers.Shift) != 0:
                e.Handled = true;
                Layout.ShowPaneMenu(Pane, this, null);
                return;
        }
    }

    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }
        e.Handled = true;
        Layout.ShowPaneMenu(Pane, this, e.TryGetPosition(this, out var point) ? point : null);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new DockAutoHideButtonAutomationPeer(this);

    private sealed class DockAutoHideButtonAutomationPeer : ControlAutomationPeer
    {
        public DockAutoHideButtonAutomationPeer(DockAutoHideButton owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// Holds a strip button's content and, while <see cref="IsTurned"/>, turns it 90° clockwise (reads top to bottom): it
/// measures as the child's height by its width and draws the child rotated into that box.
/// </summary>
internal sealed class DockTurnedPresenter : Decorator
{
    public static readonly StyledProperty<bool> IsTurnedProperty =
        AvaloniaProperty.Register<DockTurnedPresenter, bool>(nameof(IsTurned));

    static DockTurnedPresenter()
    {
        AffectsMeasure<DockTurnedPresenter>(IsTurnedProperty);
    }

    public bool IsTurned { get => GetValue(IsTurnedProperty); set => SetValue(IsTurnedProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        if (!IsTurned)
        {
            child.Measure(availableSize);
            return child.DesiredSize;
        }
        child.Measure(new Size(availableSize.Height, availableSize.Width));
        return new Size(child.DesiredSize.Height, child.DesiredSize.Width);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is not { } child)
        {
            return finalSize;
        }
        if (!IsTurned)
        {
            child.RenderTransform = null;
            child.Arrange(new Rect(finalSize));
            return finalSize;
        }
        // Lay the child out in its own (unturned) box, then turn the box: a box point (x, y) lands at (height - y, x). The
        // child is drawn at its offset in the box (its margin) after its render transform, so take that offset back out.
        var box = new Size(finalSize.Height, finalSize.Width);
        child.Arrange(new Rect(box));
        var at = child.Bounds.Position;
        child.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
        child.RenderTransform = new MatrixTransform(Matrix.CreateRotation(Math.PI / 2)
                                                    * Matrix.CreateTranslation(box.Height - at.Y - at.X, at.X - at.Y));
        return finalSize;
    }
}
