using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>
/// The toolbar of a <see cref="PageViewer"/>: page number field and page count, previous and next, zoom out, zoom menu
/// (steps and fit modes), zoom in, layout and pan toggles, with the viewer's start and end slots at the two ends. A viewer
/// narrower than <see cref="NarrowWidth"/> keeps only the page field and the zoom; with no pages only the two slots are left.
/// </summary>
[PseudoClasses(NarrowPseudoClass, SinglePseudoClass, PanPseudoClass, EmptyPseudoClass)]
internal sealed class PageViewerToolbar : TemplatedControl
{
    public const string NarrowPseudoClass = ":narrow";
    public const string SinglePseudoClass = ":single";
    public const string PanPseudoClass = ":pan";

    /// <summary>Set while there are no pages: the page and zoom controls say nothing then, and the theme hides them (their buttons are disabled as well).</summary>
    public const string EmptyPseudoClass = ":empty";

    public const string PageBoxPart = "PART_PageBox";
    public const string PageTotalPart = "PART_PageTotal";
    public const string PreviousPart = "PART_Previous";
    public const string NextPart = "PART_Next";
    public const string ZoomOutPart = "PART_ZoomOut";
    public const string ZoomMenuPart = "PART_ZoomMenu";
    public const string ZoomLabelPart = "PART_ZoomLabel";
    public const string ZoomInPart = "PART_ZoomIn";
    public const string LayoutPart = "PART_Layout";
    public const string LayoutGlyphPart = "PART_LayoutGlyph";
    public const string PanPart = "PART_Pan";
    public const string PanGlyphPart = "PART_PanGlyph";

    /// <summary>Below this width the toolbar drops everything but the page field and the zoom.</summary>
    public const double NarrowWidth = 520;

    // Lucide "hand"; not in Core's subset, and only the toolbar draws it.
    private const string HandPath = "M18 11V6a2 2 0 0 0-2-2a2 2 0 0 0-2 2 M14 10V4a2 2 0 0 0-2-2a2 2 0 0 0-2 2v2 M10 10.5V6a2 2 0 0 0-2-2a2 2 0 0 0-2 2v8 M18 8a2 2 0 1 1 4 0v6a8 8 0 0 1-8 8h-2c-2.8 0-4.5-.86-5.99-2.34l-3.6-3.6a2 2 0 0 1 2.83-2.82L7 15";

    /// <summary>Content at the left end.</summary>
    public static readonly StyledProperty<object?> StartProperty =
        AvaloniaProperty.Register<PageViewerToolbar, object?>(nameof(Start));

    /// <summary>Content at the right end.</summary>
    public static readonly StyledProperty<object?> EndProperty =
        AvaloniaProperty.Register<PageViewerToolbar, object?>(nameof(End));

    private enum ZoomChoice
    {
        ActualSize,
        FitWidth,
        FitHeight,
        FitPage
    }

    private PageViewer? _viewer;
    private Input? _pageBox;
    private TextBlock? _pageTotal;
    private Button? _previous;
    private Button? _next;
    private Button? _zoomOut;
    private Button? _zoomMenuButton;
    private TextBlock? _zoomLabel;
    private Button? _zoomIn;
    private Button? _layout;
    private ArcGlyph? _layoutGlyph;
    private Button? _pan;
    private MenuSurface? _zoomMenu;

    public object? Start { get => GetValue(StartProperty); set => SetValue(StartProperty, value); }

    public object? End { get => GetValue(EndProperty); set => SetValue(EndProperty, value); }

    /// <summary>The zoom menu once it was opened (built on first use).</summary>
    internal MenuSurface? ZoomMenu => _zoomMenu;

    public void SetOwner(PageViewer? viewer)
    {
        _viewer = viewer;
        Sync();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        var scope = e.NameScope;
        _pageBox = scope.Find<Input>(PageBoxPart);
        _pageTotal = scope.Find<TextBlock>(PageTotalPart);
        _previous = scope.Find<Button>(PreviousPart);
        _next = scope.Find<Button>(NextPart);
        _zoomOut = scope.Find<Button>(ZoomOutPart);
        _zoomMenuButton = scope.Find<Button>(ZoomMenuPart);
        _zoomLabel = scope.Find<TextBlock>(ZoomLabelPart);
        _zoomIn = scope.Find<Button>(ZoomInPart);
        _layout = scope.Find<Button>(LayoutPart);
        _layoutGlyph = scope.Find<ArcGlyph>(LayoutGlyphPart);
        _pan = scope.Find<Button>(PanPart);
        if (scope.Find<ArcGlyph>(PanGlyphPart) is { } panGlyph)
        {
            panGlyph.Data = ArcIcons.Parse(HandPath);
        }
        Hook();
        Sync();
    }

    private void Hook()
    {
        if (_pageBox is { } box)
        {
            box.AddHandler(KeyDownEvent, OnPageBoxKeyDown, RoutingStrategies.Tunnel);
            box.GotFocus += OnPageBoxGotFocus;
            box.LostFocus += OnPageBoxLostFocus;
        }
        Click(_previous, OnPreviousClick);
        Click(_next, OnNextClick);
        Click(_zoomOut, OnZoomOutClick);
        Click(_zoomMenuButton, OnZoomMenuClick);
        Click(_zoomIn, OnZoomInClick);
        Click(_layout, OnLayoutClick);
        Click(_pan, OnPanClick);
    }

    private void Unhook()
    {
        if (_pageBox is { } box)
        {
            box.RemoveHandler(KeyDownEvent, OnPageBoxKeyDown);
            box.GotFocus -= OnPageBoxGotFocus;
            box.LostFocus -= OnPageBoxLostFocus;
        }
        Unclick(_previous, OnPreviousClick);
        Unclick(_next, OnNextClick);
        Unclick(_zoomOut, OnZoomOutClick);
        Unclick(_zoomMenuButton, OnZoomMenuClick);
        Unclick(_zoomIn, OnZoomInClick);
        Unclick(_layout, OnLayoutClick);
        Unclick(_pan, OnPanClick);
    }

    private static void Click(Button? button, EventHandler<RoutedEventArgs> handler)
    {
        if (button is not null)
        {
            button.Click += handler;
        }
    }

    private static void Unclick(Button? button, EventHandler<RoutedEventArgs> handler)
    {
        if (button is not null)
        {
            button.Click -= handler;
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        PseudoClasses.Set(NarrowPseudoClass, e.NewSize.Width < NarrowWidth);
    }

    /// <summary>Shows the viewer's state.</summary>
    public void Sync()
    {
        if (_viewer is not { } viewer)
        {
            return;
        }
        PseudoClasses.Set(SinglePseudoClass, viewer.LayoutMode == PageLayoutMode.Single);
        PseudoClasses.Set(PanPseudoClass, viewer.InteractionMode == PageInteractionMode.Pan);
        var count = viewer.PageCount;
        var index = viewer.CurrentPageIndex;
        PseudoClasses.Set(EmptyPseudoClass, count == 0);
        if (_pageBox is { IsFocused: false } box)
        {
            box.Text = PageText(viewer);
        }
        if (_pageBox is { } enabledBox)
        {
            enabledBox.IsEnabled = count > 0;
        }
        if (_pageTotal is { } total)
        {
            total.Text = "/ " + count.ToString(CultureInfo.InvariantCulture);
        }
        SetEnabled(_previous, count > 0 && index > 0);
        SetEnabled(_next, count > 0 && index < count - 1);
        SetEnabled(_zoomOut, count > 0 && viewer.Zoom > viewer.MinZoom);
        SetEnabled(_zoomIn, count > 0 && viewer.Zoom < viewer.MaxZoom);
        SetEnabled(_zoomMenuButton, count > 0);
        SetEnabled(_layout, count > 0);
        SetEnabled(_pan, count > 0);
        if (_zoomLabel is { } label)
        {
            label.Text = ZoomText(viewer.Zoom);
        }
        if (_layoutGlyph is { } glyph)
        {
            glyph.Data = viewer.LayoutMode == PageLayoutMode.Single ? ArcIcons.File : ArcIcons.Rows2;
        }
    }

    private static void SetEnabled(Button? button, bool enabled)
    {
        if (button is not null)
        {
            button.IsEnabled = enabled;
        }
    }

    private static string PageText(PageViewer viewer) =>
        viewer.PageCount == 0 ? string.Empty : (viewer.CurrentPageIndex + 1).ToString(CultureInfo.InvariantCulture);

    private static string ZoomText(double zoom) =>
        Math.Round(zoom * 100).ToString(CultureInfo.InvariantCulture) + "%";

    private void OnPreviousClick(object? sender, RoutedEventArgs e) => _viewer?.PreviousPage();

    private void OnNextClick(object? sender, RoutedEventArgs e) => _viewer?.NextPage();

    private void OnZoomOutClick(object? sender, RoutedEventArgs e) => _viewer?.ZoomOut();

    private void OnZoomInClick(object? sender, RoutedEventArgs e) => _viewer?.ZoomIn();

    private void OnLayoutClick(object? sender, RoutedEventArgs e)
    {
        if (_viewer is { } viewer)
        {
            viewer.SetCurrentValue(PageViewer.LayoutModeProperty,
                viewer.LayoutMode == PageLayoutMode.Single ? PageLayoutMode.Continuous : PageLayoutMode.Single);
        }
    }

    private void OnPanClick(object? sender, RoutedEventArgs e)
    {
        if (_viewer is { } viewer)
        {
            viewer.SetCurrentValue(PageViewer.InteractionModeProperty,
                viewer.InteractionMode == PageInteractionMode.Pan ? PageInteractionMode.Select : PageInteractionMode.Pan);
        }
    }

    private void OnPageBoxGotFocus(object? sender, FocusChangedEventArgs e) => _pageBox?.SelectAll();

    private void OnPageBoxLostFocus(object? sender, FocusChangedEventArgs e) => Sync();

    private void OnPageBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (_pageBox is not { } box || _viewer is not { } viewer)
        {
            return;
        }
        if (e.Key == Key.Enter)
        {
            if (int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                viewer.GoToPage(number - 1);
            }
            box.Text = PageText(viewer);
            box.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            box.Text = PageText(viewer);
            box.SelectAll();
            e.Handled = true;
        }
    }

    #region Zoom menu

    private void OnZoomMenuClick(object? sender, RoutedEventArgs e)
    {
        if (_zoomMenuButton is not { } anchor || _viewer is null)
        {
            return;
        }
        var menu = EnsureZoomMenu();
        if (!menu.IsOpen)
        {
            FillZoomMenu(menu);
        }
        menu.Toggle(anchor);
    }

    private MenuSurface EnsureZoomMenu()
    {
        if (_zoomMenu is null)
        {
            _zoomMenu = new MenuSurface { Variant = MenuSurfaceVariant.Dropdown, Alignment = MenuAlignment.Center, AnchorGap = 6 };
            _zoomMenu.ItemClicked += OnZoomMenuItemClicked;
        }
        return _zoomMenu;
    }

    /// <summary>Rebuilt for every opening, so it shows the language and the state of that moment.</summary>
    private void FillZoomMenu(MenuSurface menu)
    {
        var viewer = _viewer!;
        menu.Items.Clear();
        foreach (var step in PageLayoutEngine.ZoomSteps)
        {
            if (step < viewer.MinZoom || step > viewer.MaxZoom)
            {
                continue;
            }
            menu.Items.Add(new MenuEntry
            {
                Header = ZoomText(step),
                Tag = step,
                ToggleType = MenuEntryToggleType.Radio,
                GroupName = "zoom",
                IsChecked = viewer.FitMode == PageFitMode.None && Math.Abs(viewer.Zoom - step) < 1e-3
            });
        }
        menu.Items.Add(FitEntry(ZoomChoice.ActualSize, PageViewerLangResourceKind.ActualSize, false, true));
        menu.Items.Add(FitEntry(ZoomChoice.FitWidth, PageViewerLangResourceKind.FitWidth, viewer.FitMode == PageFitMode.Width, false));
        menu.Items.Add(FitEntry(ZoomChoice.FitHeight, PageViewerLangResourceKind.FitHeight, viewer.FitMode == PageFitMode.Height, false));
        menu.Items.Add(FitEntry(ZoomChoice.FitPage, PageViewerLangResourceKind.FitPage, viewer.FitMode == PageFitMode.Page, false));
    }

    private static MenuEntry FitEntry(ZoomChoice choice, PageViewerLangResourceKind text, bool isChecked, bool separatorBefore) => new()
    {
        Header = PageViewerLangResources.Get(text),
        Tag = choice,
        ToggleType = MenuEntryToggleType.Radio,
        GroupName = "zoom",
        IsChecked = isChecked,
        HasSeparatorBefore = separatorBefore
    };

    private void OnZoomMenuItemClicked(object? sender, MenuEntryClickedEventArgs e)
    {
        if (_viewer is not { } viewer)
        {
            return;
        }
        switch (e.Entry.Tag)
        {
            case double step:
                viewer.ZoomToStep(step);
                break;
            case ZoomChoice.ActualSize:
                viewer.ResetZoom();
                break;
            case ZoomChoice.FitWidth:
                viewer.SetCurrentValue(PageViewer.FitModeProperty, PageFitMode.Width);
                break;
            case ZoomChoice.FitHeight:
                viewer.SetCurrentValue(PageViewer.FitModeProperty, PageFitMode.Height);
                break;
            case ZoomChoice.FitPage:
                viewer.SetCurrentValue(PageViewer.FitModeProperty, PageFitMode.Page);
                break;
        }
    }

    #endregion
}
