using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A compact navigation pill that opens into a panel of sections and links, and closes back into the pill. The pill
/// shows your mark, a title and the page you are on; pressing it grows the same surface into the panel on a spring,
/// its corners and shadow following, and choosing a link shrinks it back.
/// </summary>
/// <remarks>
/// The nav takes only the pill's room in the layout. The open panel grows over the page from the pill: to the right
/// when the nav is aligned left, to the left when it is aligned right, and both ways when it is centered, kept
/// <c>PanelGutter</c> from the window's sides. It sits above its siblings (<c>ZIndex</c> 10); give its parent room or
/// no clipping if the panel should be seen in full.
/// </remarks>
[TemplatePart(PillPart, typeof(AvaloniaButton))]
[TemplatePart(PillHostPart, typeof(PresenceHost))]
[TemplatePart(PanelHostPart, typeof(PresenceHost))]
[TemplatePart(SectionsPart, typeof(Panel))]
[TemplatePart(ClosePart, typeof(AvaloniaButton))]
[TemplatePart(SurfacePart, typeof(Border))]
[PseudoClasses(OpenPseudoClass)]
public class MorphNav : TemplatedControl
{
    internal const string PillPart = "PART_Pill";
    internal const string PillHostPart = "PART_PillHost";
    internal const string PanelHostPart = "PART_PanelHost";
    internal const string SectionsPart = "PART_Sections";
    internal const string ClosePart = "PART_Close";
    internal const string SurfacePart = "PART_Surface";
    internal const string OpenPseudoClass = ":open";

    /// <summary>Your product's or site's name, shown in the pill and at the top of the panel.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MorphNav, string?>(nameof(Title));

    /// <summary>A 24 × 24 stroked mark before the title; null shows the title alone.</summary>
    public static readonly StyledProperty<Geometry?> MarkProperty =
        AvaloniaProperty.Register<MorphNav, Geometry?>(nameof(Mark));

    /// <summary>Whether the panel is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<MorphNav, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The page you are on (two-way): its link is marked in the panel and its text shows in the pill.</summary>
    public static readonly StyledProperty<MorphNavLink?> CurrentLinkProperty =
        AvaloniaProperty.Register<MorphNav, MorphNavLink?>(nameof(CurrentLink), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Content at the bottom of the open panel, such as a sign-in button; null leaves it out.</summary>
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<MorphNav, object?>(nameof(Footer));

    /// <summary>Width of the open panel in px; NaN (default) uses the theme's 600. It never exceeds the window less its gutters.</summary>
    public static readonly StyledProperty<double> PanelWidthProperty =
        AvaloniaProperty.Register<MorphNav, double>(nameof(PanelWidth), double.NaN);

    /// <summary>Accessible name of the pill, which opens the panel.</summary>
    public static readonly StyledProperty<string> MenuLabelProperty =
        AvaloniaProperty.Register<MorphNav, string>(nameof(MenuLabel), "Open menu");

    /// <summary>Accessible name and tooltip of the panel's close button.</summary>
    public static readonly StyledProperty<string> CloseLabelProperty =
        AvaloniaProperty.Register<MorphNav, string>(nameof(CloseLabel), "Close menu");

    /// <summary>Raised when a link is chosen, after it becomes <see cref="CurrentLink"/>. Bubbles.</summary>
    public static readonly RoutedEvent<MorphNavLinkEventArgs> LinkInvokedEvent =
        RoutedEvent.Register<MorphNav, MorphNavLinkEventArgs>(nameof(LinkInvoked), RoutingStrategies.Bubble);

    private readonly List<MorphNavLinkRow> _rows = new();
    private readonly List<int> _rowSection = new();
    private AvaloniaButton? _pill;
    private PresenceHost? _pillHost;
    private PresenceHost? _panelHost;
    private Panel? _sections;
    private AvaloniaButton? _close;
    private Border? _surface;
    private MorphNavSection[] _watched = [];
    private TopLevel? _pressRoot;
    private IDisposable? _viewport;
    private Size _footprint;
    private Size _pillSize;
    private int _focusRequest;
    private bool _rowsDirty = true;

    static MorphNav()
    {
        FocusableProperty.OverrideDefaultValue<MorphNav>(false);
        ClipToBoundsProperty.OverrideDefaultValue<MorphNav>(false);
        ZIndexProperty.OverrideDefaultValue<MorphNav>(10);
        AffectsMeasure<MorphNav>(PanelWidthProperty);
        SpecialFocusModality.Watch();
    }

    public MorphNav()
    {
        Sections.CollectionChanged += OnSectionsChanged;
    }

    /// <summary>The panel's sections, in order.</summary>
    [Content]
    public AvaloniaList<MorphNavSection> Sections { get; } = new();

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="MarkProperty"/>
    public Geometry? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <inheritdoc cref="CurrentLinkProperty"/>
    public MorphNavLink? CurrentLink { get => GetValue(CurrentLinkProperty); set => SetValue(CurrentLinkProperty, value); }

    /// <inheritdoc cref="FooterProperty"/>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

    /// <inheritdoc cref="PanelWidthProperty"/>
    public double PanelWidth { get => GetValue(PanelWidthProperty); set => SetValue(PanelWidthProperty, value); }

    /// <inheritdoc cref="MenuLabelProperty"/>
    public string MenuLabel { get => GetValue(MenuLabelProperty); set => SetValue(MenuLabelProperty, value); }

    /// <inheritdoc cref="CloseLabelProperty"/>
    public string CloseLabel { get => GetValue(CloseLabelProperty); set => SetValue(CloseLabelProperty, value); }

    /// <inheritdoc cref="LinkInvokedEvent"/>
    public event EventHandler<MorphNavLinkEventArgs>? LinkInvoked
    {
        add => AddHandler(LinkInvokedEvent, value);
        remove => RemoveHandler(LinkInvokedEvent, value);
    }

    /// <summary>The pill (null before the template is applied).</summary>
    internal AvaloniaButton? Pill => _pill;

    /// <summary>The panel's link rows, in reading order.</summary>
    internal IReadOnlyList<MorphNavLinkRow> Rows => _rows;

    /// <summary>The room the nav takes in the layout: the closed pill's.</summary>
    internal Size Footprint => _footprint;

    /// <summary>Opens the panel; with the keyboard, focus moves to the first link.</summary>
    public void Open() => SetCurrentValue(IsOpenProperty, true);

    /// <summary>Closes the panel back into the pill.</summary>
    public void Close() => SetCurrentValue(IsOpenProperty, false);

    /// <summary>Opens the panel when it is closed and closes it when it is open.</summary>
    public void Toggle() => SetCurrentValue(IsOpenProperty, !IsOpen);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_pill is not null)
        {
            _pill.Click -= OnPillClick;
        }
        if (_close is not null)
        {
            _close.Click -= OnCloseClick;
        }
        _sections?.Children.Clear();
        _pill = e.NameScope.Find<AvaloniaButton>(PillPart);
        _pillHost = e.NameScope.Find<PresenceHost>(PillHostPart);
        _panelHost = e.NameScope.Find<PresenceHost>(PanelHostPart);
        _sections = e.NameScope.Find<Panel>(SectionsPart);
        _close = e.NameScope.Find<AvaloniaButton>(ClosePart);
        _surface = e.NameScope.Find<Border>(SurfacePart);
        if (_pill is not null)
        {
            _pill.Click += OnPillClick;
        }
        if (_close is not null)
        {
            _close.Click += OnCloseClick;
        }
        _rowsDirty = true;
        RebuildRows();
        UpdateState();
        UpdateNames();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            OnOpenChanged(change.GetNewValue<bool>());
        }
        else if (change.Property == CurrentLinkProperty)
        {
            UpdateCurrent();
        }
        else if (change.Property == MenuLabelProperty || change.Property == CloseLabelProperty || change.Property == TitleProperty)
        {
            UpdateNames();
        }
        else if (change.Property == HorizontalAlignmentProperty)
        {
            InvalidateArrange();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _viewport = ArcViewport.Observe(this, _ => InvalidateMeasure());
        if (IsOpen)
        {
            WatchOutsidePresses(true);
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _viewport?.Dispose();
        _viewport = null;
        WatchOutsidePresses(false);
    }

    #region Layout

    private double Token(MorphNavTokenKind kind, double fallback)
        => this.TryFindResource(MorphNavTokens.Key(kind), out var value) && value is double d ? d : fallback;

    /// <summary>The width the open panel takes: <see cref="PanelWidth"/> or the theme's, within the window less its gutters.</summary>
    internal double EffectivePanelWidth(double available)
    {
        var width = double.IsNaN(PanelWidth) ? Token(MorphNavTokenKind.PanelWidth, 600) : PanelWidth;
        var gutter = Token(MorphNavTokenKind.PanelGutter, 16);
        if (ArcViewport.GetSize(this) is { } viewport)
        {
            width = Math.Min(width, viewport.Width - 2 * gutter);
        }
        if (!double.IsInfinity(available) && HorizontalAlignment == HorizontalAlignment.Stretch)
        {
            width = Math.Min(width, Math.Max(available, 0));
        }
        return Math.Max(width, 160);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var panelWidth = EffectivePanelWidth(availableSize.Width);
        if (_panelHost?.Child is Layoutable panel && !panel.Width.Equals(panelWidth))
        {
            panel.Width = panelWidth;
        }
        // The surface is measured without the parent's limits: it grows over the page, not into the layout.
        base.MeasureOverride(new Size(double.PositiveInfinity, double.PositiveInfinity));
        _footprint = MeasurePill();
        return _footprint;
    }

    private Size MeasurePill()
    {
        if (_pill is null)
        {
            return default;
        }
        // The pill's size is kept from the last time it was shown: while the panel is open the pill is hidden.
        if (_pillHost is { IsVisible: true } && _pill.DesiredSize != default)
        {
            _pillSize = _pill.DesiredSize;
        }
        else if (_pillSize == default)
        {
            _pill.Measure(Size.Infinity);
            _pillSize = _pill.DesiredSize;
        }
        var edge = _surface?.BorderThickness ?? default;
        return new Size(_pillSize.Width + edge.Left + edge.Right, _pillSize.Height + edge.Top + edge.Bottom);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (VisualChildren.Count == 0 || VisualChildren[0] is not Layoutable root)
        {
            return finalSize;
        }
        var size = root.DesiredSize;
        var x = HorizontalAlignment switch
        {
            HorizontalAlignment.Right => finalSize.Width - size.Width,
            HorizontalAlignment.Center => (finalSize.Width - size.Width) / 2,
            _ => 0
        };
        x = KeepInViewport(x, size.Width);
        root.Arrange(new Rect(x, 0, size.Width, size.Height));
        return finalSize;
    }

    /// <summary>Shifts the surface so that it keeps the gutter from the window's sides.</summary>
    private double KeepInViewport(double x, double width)
    {
        if (TopLevel.GetTopLevel(this) is not { } top || ArcViewport.GetBounds(this) is not { } viewport
            || this.TranslatePoint(default, top) is not { } origin)
        {
            return x;
        }
        var gutter = Token(MorphNavTokenKind.PanelGutter, 16);
        var left = origin.X + x;
        var maxLeft = viewport.Right - gutter - width;
        var minLeft = viewport.Left + gutter;
        if (left > maxLeft)
        {
            left = maxLeft;
        }
        if (left < minLeft)
        {
            left = minLeft;
        }
        return left - origin.X;
    }

    #endregion

    #region Rows

    private void OnSectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _rowsDirty = true;
        RebuildRows();
    }

    private void OnLinksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _rowsDirty = true;
        RebuildRows();
    }

    /// <summary>Builds a block per section: its heading and a row per link.</summary>
    private void RebuildRows()
    {
        foreach (var section in _watched)
        {
            section.Links.CollectionChanged -= OnLinksChanged;
        }
        _watched = Sections.ToArray();
        foreach (var section in _watched)
        {
            section.Links.CollectionChanged += OnLinksChanged;
        }
        if (_sections is null || !_rowsDirty)
        {
            return;
        }
        _rowsDirty = false;
        foreach (var row in _rows)
        {
            row.Owner = null;
        }
        _rows.Clear();
        _rowSection.Clear();
        _sections.Children.Clear();
        for (var s = 0; s < _watched.Length; s++)
        {
            var section = _watched[s];
            var block = new StackPanel { Spacing = 2 };
            if (section.Header is { Length: > 0 } header)
            {
                var heading = new TextBlock { Text = header, Margin = new Thickness(10, 4, 10, 6) };
                ArcTokens.Bind(heading, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
                ArcTokens.Bind(heading, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
                ArcTokens.Bind(heading, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
                block.Children.Add(heading);
            }
            foreach (var link in section.Links)
            {
                var row = new MorphNavLinkRow(link) { Owner = this };
                _rows.Add(row);
                _rowSection.Add(s);
                block.Children.Add(row);
            }
            _sections.Children.Add(block);
        }
        UpdateCurrent();
        UpdateTabStops();
    }

    private void UpdateCurrent()
    {
        var current = CurrentLink;
        foreach (var row in _rows)
        {
            row.SetCurrent(ReferenceEquals(row.Link, current));
        }
        if (_pill is MorphNavPill pill)
        {
            pill.Current = current?.Text;
        }
        UpdateNames();
    }

    /// <summary>Only the panel's rows are tab stops while it is open; the pill is one while it is closed.</summary>
    private void UpdateTabStops()
    {
        var open = IsOpen;
        foreach (var row in _rows)
        {
            row.IsTabStop = open;
        }
        if (_pill is not null)
        {
            _pill.IsTabStop = !open;
            _pill.IsHitTestVisible = !open;
        }
    }

    /// <summary>A row was chosen: it becomes the current page, the event is raised and the panel closes.</summary>
    internal void Invoke(MorphNavLinkRow row)
    {
        if (!ReferenceEquals(row.Owner, this))
        {
            return;
        }
        SetCurrentValue(CurrentLinkProperty, row.Link);
        RaiseEvent(new MorphNavLinkEventArgs(LinkInvokedEvent, row.Link));
        ReturnFocus(SpecialFocusModality.LastWasPointer(this) != true);
        Close();
    }

    #endregion

    #region Open and close

    private void OnOpenChanged(bool open)
    {
        if (!open)
        {
            ReturnFocus(SpecialFocusModality.LastWasPointer(this) != true);
        }
        UpdateState();
        WatchOutsidePresses(open);
        if (open)
        {
            FocusPanelSoon();
        }
    }

    private void UpdateState()
    {
        var open = IsOpen;
        PseudoClasses.Set(OpenPseudoClass, open);
        if (_pillHost is not null)
        {
            _pillHost.IsPresent = !open;
        }
        if (_panelHost is not null)
        {
            _panelHost.IsPresent = open;
        }
        if (_pill is not null)
        {
            AutomationProperties.SetItemStatus(_pill, open ? "Expanded" : "Collapsed");
        }
        UpdateTabStops();
    }

    private void UpdateNames()
    {
        if (_pill is not null)
        {
            var name = CurrentLink?.Text is { Length: > 0 } current ? $"{MenuLabel}, {current}" : MenuLabel;
            AutomationProperties.SetName(_pill, name);
        }
        if (_close is not null)
        {
            AutomationProperties.SetName(_close, CloseLabel);
            Tooltip.SetTip(_close, CloseLabel);
        }
        if (_panelHost is not null)
        {
            AutomationProperties.SetName(_panelHost, Title ?? MenuLabel);
        }
    }

    private void FocusPanelSoon()
    {
        var request = ++_focusRequest;
        var method = SpecialFocusModality.For(this);
        Dispatcher.UIThread.Post(() =>
        {
            if (request != _focusRequest || !IsOpen)
            {
                return;
            }
            var current = _rows.FirstOrDefault(r => ReferenceEquals(r.Link, CurrentLink));
            Control? target = current ?? (_rows.Count > 0 ? _rows[0] : (Control?)_close);
            target?.Focus(method);
        }, DispatcherPriority.Loaded);
    }

    private void ReturnFocus(bool keyboard)
    {
        _focusRequest++;
        if (_pill is null || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not Visual focused)
        {
            return;
        }
        if (_panelHost is not null && (ReferenceEquals(focused, _panelHost) || _panelHost.IsVisualAncestorOf(focused)
                                       || (focused is ILogical logical && _panelHost.IsLogicalAncestorOf(logical))))
        {
            // The pill comes back first: it cannot take focus while it is hidden.
            if (_pillHost is not null)
            {
                _pillHost.IsPresent = true;
            }
            _pill.IsTabStop = true;
            _pill.IsHitTestVisible = true;
            _pill.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
        }
    }

    private void OnPillClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Open();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ReturnFocus(SpecialFocusModality.LastWasPointer(this) != true);
        Close();
    }

    private void WatchOutsidePresses(bool watch)
    {
        if (_pressRoot is not null)
        {
            _pressRoot.RemoveHandler(PointerPressedEvent, OnRootPointerPressed);
            _pressRoot = null;
        }
        if (watch && TopLevel.GetTopLevel(this) is { } root)
        {
            _pressRoot = root;
            root.AddHandler(PointerPressedEvent, OnRootPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
    }

    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsOpen || e.Source is not Visual source)
        {
            return;
        }
        if (ReferenceEquals(source, this) || this.IsVisualAncestorOf(source)
            || (source is ILogical logical && this.IsLogicalAncestorOf(logical)))
        {
            return;
        }
        Close();
    }

    #endregion

    #region Keyboard

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled)
        {
            base.OnKeyDown(e);
            return;
        }
        if (e.Key == Key.Escape && IsOpen)
        {
            e.Handled = true;
            ReturnFocus(keyboard: true);
            Close();
            return;
        }
        if (IsOpen && e.Source is MorphNavLinkRow row && _rows.IndexOf(row) is var index and >= 0)
        {
            var target = e.Key switch
            {
                Key.Down => Math.Min(index + 1, _rows.Count - 1),
                Key.Up => Math.Max(index - 1, 0),
                Key.Home => 0,
                Key.End => _rows.Count - 1,
                Key.Right => AcrossSections(index, 1),
                Key.Left => AcrossSections(index, -1),
                _ => -1
            };
            if (target >= 0)
            {
                e.Handled = true;
                _rows[target].Focus(NavigationMethod.Directional);
                return;
            }
            if (e.Key is Key.Enter or Key.Space)
            {
                e.Handled = true;
                Invoke(row);
                return;
            }
        }
        base.OnKeyDown(e);
    }

    /// <summary>
    /// The row at the same height in the next section to the side, when sections sit side by side; otherwise the row
    /// itself.
    /// </summary>
    internal int AcrossSections(int index, int direction)
    {
        var columns = _sections is MorphNavColumns panel ? panel.Columns : 1;
        if (columns <= 1)
        {
            return index;
        }
        var section = _rowSection[index];
        var targetSection = section + direction;
        if (targetSection < 0 || targetSection >= _watched.Length || targetSection / columns != section / columns)
        {
            return index;
        }
        var first = _rowSection.IndexOf(section);
        var offset = index - first;
        var targetFirst = _rowSection.IndexOf(targetSection);
        if (targetFirst < 0)
        {
            return index;
        }
        var targetCount = _rowSection.Count(s => s == targetSection);
        return targetFirst + Math.Min(offset, targetCount - 1);
    }

    #endregion
}
