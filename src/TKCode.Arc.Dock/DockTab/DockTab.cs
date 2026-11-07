using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Localization;
using TKCode.Arc.Primitives;

using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The tab of one <see cref="DockPane"/> in a group's strip (self-designed, after <c>TabItem</c>): optional icon, title and,
/// for documents, a close button that shows on hover, selection or keyboard focus (a modified document shows a dot until
/// hovered). The selected background is the strip's gliding pill, not the tab.
/// </summary>
[TemplatePart(ItemsPart, typeof(ArcShrinkRow))]
[PseudoClasses(SelectedPseudoClass, PressedPseudoClass, ModifiedPseudoClass, DocumentPseudoClass, DraggingPseudoClass, GroupActivePseudoClass)]
internal sealed class DockTab : TemplatedControl
{
    public const string ClosePart = "PART_Close";
    public const string ItemsPart = "PART_Items";
    public const string IconPart = "PART_Icon";
    public const string SelectedPseudoClass = ":selected";
    public const string PressedPseudoClass = ":pressed";
    public const string ModifiedPseudoClass = ":modified";
    public const string DocumentPseudoClass = ":document";
    public const string DraggingPseudoClass = ":dragging";
    public const string GroupActivePseudoClass = ":group-active";

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<DockTab, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<DockTab, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<bool> ShowsCloseProperty =
        AvaloniaProperty.Register<DockTab, bool>(nameof(ShowsClose));

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<DockTab, bool>(nameof(IsSelected));

    private readonly IDisposable[] _bindings;
    private ArcShrinkRow? _items;
    private ArcGlyph? _icon;
    private Panel? _closeSlot;
    private Ellipse? _dot;
    private DockTabCloseButton? _close;
    private LazyFocusRing? _focusRing;

    static DockTab()
    {
        FocusableProperty.OverrideDefaultValue<DockTab>(true);
    }

    public DockTab(DockGroupView owner, DockPane pane)
    {
        Owner = owner;
        Pane = pane;
        _bindings =
        [
            Bind(TitleProperty, pane.GetObservable(DockPane.TitleProperty)),
            Bind(IconProperty, pane.GetObservable(DockPane.IconProperty)),
            Bind(Tooltip.TipProperty, pane.GetObservable(Tooltip.TipProperty))
        ];
        pane.PropertyChanged += OnPanePropertyChanged;
        AddHandler(ContextRequestedEvent, OnContextRequested);
        UpdateState();
    }

    public DockGroupView Owner { get; }

    public DockPane Pane { get; }

    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>True for a document tab of a closable pane: it has the close slot.</summary>
    public bool ShowsClose { get => GetValue(ShowsCloseProperty); set => SetValue(ShowsCloseProperty, value); }

    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    /// <summary>The close button, once the tab needed one (tests, drags).</summary>
    internal AvaloniaButton? CloseButton => _close;

    /// <summary>Stops following the pane (the tab is being discarded).</summary>
    public void Release()
    {
        foreach (var binding in _bindings)
        {
            binding.Dispose();
        }
        Pane.PropertyChanged -= OnPanePropertyChanged;
    }

    public void SetDocument(bool document)
    {
        PseudoClasses.Set(DocumentPseudoClass, document);
        UpdateState();
    }

    public void SetGroupActive(bool active) => PseudoClasses.Set(GroupActivePseudoClass, active);

    /// <summary>Dims the tab while its pane is being docked elsewhere (<c>:dragging</c>).</summary>
    public void SetDragging(bool dragging) => PseudoClasses.Set(DraggingPseudoClass, dragging);

    private void OnPanePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == DockPane.IsModifiedProperty || e.Property == DockPane.CanCloseProperty || e.Property == DockPane.TitleProperty)
        {
            UpdateState();
        }
    }

    /// <summary>Follows the pane and the layout's lock: close slot, modified dot, accessible names.</summary>
    public void Refresh() => UpdateState();

    private void UpdateState()
    {
        var document = PseudoClasses.Contains(DocumentPseudoClass);
        ShowsClose = document && Pane.CanClose && !Owner.Layout.IsLocked;
        PseudoClasses.Set(ModifiedPseudoClass, document && Pane.IsModified);
        var title = Pane.ToString();
        AutomationProperties.SetName(this, Pane.IsModified ? DockText.Format(DockLangResourceKind.TabModified, title) : title);
        if (_close is not null)
        {
            AutomationProperties.SetName(_close, DockText.Format(DockLangResourceKind.CloseTab, title));
        }
        UpdateParts();
    }

    /// <summary>
    /// Builds the icon and the close slot the first time they are needed and shows them by state: the close button on
    /// hover, and on the selected or keyboard-focused tab unless it carries the modified dot.
    /// </summary>
    private void UpdateParts()
    {
        if (_items is null)
        {
            return;
        }
        if (Icon is not null && _icon is null)
        {
            _icon = new ArcGlyph { Name = IconPart, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            _icon.Bind(ArcGlyph.SizeProperty, _icon.GetResourceObservable(DockTabTokens.Key(DockTabTokenKind.IconSize)));
            _icon.Bind(ArcGlyph.DataProperty, this.GetObservable(IconProperty));
            _items.Children.Insert(0, _icon);
        }
        if (_icon is not null)
        {
            _icon.IsVisible = Icon is not null;
        }
        if (ShowsClose && _closeSlot is null)
        {
            BuildCloseSlot(_items);
        }
        if (_closeSlot is null || _dot is null || _close is null)
        {
            return;
        }
        _closeSlot.IsVisible = ShowsClose;
        var modified = PseudoClasses.Contains(ModifiedPseudoClass);
        var hovered = IsPointerOver;
        var emphasized = IsSelected || PseudoClasses.Contains(":focus-visible");
        // Hidden rather than transparent: a hidden button builds its template only once it shows.
        _close.IsVisible = hovered || (emphasized && !modified);
        _dot.IsVisible = modified && !hovered;
    }

    private void BuildCloseSlot(ArcShrinkRow items)
    {
        _closeSlot = new Panel { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        _closeSlot.Bind(WidthProperty, _closeSlot.GetResourceObservable(DockTabTokens.Key(DockTabTokenKind.CloseSize)));
        _closeSlot.Bind(HeightProperty, _closeSlot.GetResourceObservable(DockTabTokens.Key(DockTabTokenKind.CloseSize)));
        _dot = new Ellipse
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            IsVisible = false
        };
        _dot.Bind(WidthProperty, _dot.GetResourceObservable(DockTabTokens.Key(DockTabTokenKind.ModifiedDotSize)));
        _dot.Bind(HeightProperty, _dot.GetResourceObservable(DockTabTokens.Key(DockTabTokenKind.ModifiedDotSize)));
        ArcTokens.Bind(_dot, Shape.FillProperty, ArcDesignTokenKind.TextSecondary);
        _close = new DockTabCloseButton { Name = ClosePart, IsVisible = false };
        _close.Click += OnCloseClick;
        AutomationProperties.SetName(_close, DockText.Format(DockLangResourceKind.CloseTab, Pane.ToString()));
        _closeSlot.Children.Add(_dot);
        _closeSlot.Children.Add(_close);
        items.Children.Add(_closeSlot);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(SelectedPseudoClass, change.GetNewValue<bool>());
            UpdateParts();
        }
        else if (change.Property == IsPointerOverProperty || change.Property == IconProperty || change.Property == ShowsCloseProperty)
        {
            UpdateParts();
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_close is not null)
        {
            _close.Click -= OnCloseClick;
        }
        _icon = null;
        _closeSlot = null;
        _dot = null;
        _close = null;
        _items = e.NameScope.Find<ArcShrinkRow>(ItemsPart);
        UpdateState();
        _focusRing ??= new LazyFocusRing(this, DockTabTokens.Key(DockTabTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep8));
        _focusRing.Attach(AddRing, RemoveRing);
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

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Owner.Layout.Close(Pane);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled)
        {
            return;
        }
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed)
        {
            PseudoClasses.Set(PressedPseudoClass, true);
            Owner.OnTabPressed(this);
            e.Handled = true;
        }
        else if (point.Properties.IsMiddleButtonPressed)
        {
            // Middle click closes on release, like a browser tab.
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        PseudoClasses.Set(PressedPseudoClass, false);
        if (e.InitialPressMouseButton == MouseButton.Middle && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            Owner.Layout.Close(Pane);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        PseudoClasses.Set(PressedPseudoClass, false);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
        UpdateParts();
        Owner.OnTabFocused(this);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
        UpdateParts();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled)
        {
            Owner.OnTabKeyDown(this, e);
        }
    }

    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }
        e.Handled = true;
        Owner.Layout.ShowPaneMenu(Pane, this, e.TryGetPosition(this, out var point) ? point : null);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new DockTabAutomationPeer(this);

    private sealed class DockTabAutomationPeer : ControlAutomationPeer
    {
        public DockTabAutomationPeer(DockTab owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>The close button of a document tab (its own type, so the tab can make it only when it needs one).</summary>
internal sealed class DockTabCloseButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(DockTabCloseButton);
}
