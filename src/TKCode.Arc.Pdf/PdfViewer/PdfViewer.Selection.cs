using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Localization;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class PdfViewer
{
    #region Properties

    /// <summary>Lets the pointer select text in <see cref="PageInteractionMode.Select"/> mode.</summary>
    public static readonly StyledProperty<bool> IsTextSelectionEnabledProperty =
        AvaloniaProperty.Register<PdfViewer, bool>(nameof(IsTextSelectionEnabled), true);

    /// <summary>The selected text: pages in order with a line break between them. Pages between the ends of a long selection are read in the background, and it is complete once they are.</summary>
    public static readonly DirectProperty<PdfViewer, string> SelectedTextProperty =
        AvaloniaProperty.RegisterDirect<PdfViewer, string>(nameof(SelectedText), o => o.SelectedText);

    /// <inheritdoc cref="IsTextSelectionEnabledProperty"/>
    public bool IsTextSelectionEnabled { get => GetValue(IsTextSelectionEnabledProperty); set => SetValue(IsTextSelectionEnabledProperty, value); }

    /// <inheritdoc cref="SelectedTextProperty"/>
    public string SelectedText
    {
        get => _selectedText;
        private set => SetAndRaise(SelectedTextProperty, ref _selectedText, value);
    }

    #endregion

    private PdfSelectionLayer _selectionLayer = null!;
    private string _selectedText = string.Empty;
    private MenuSurface? _contextMenu;
    private MenuEntry? _copyEntry;
    private MenuEntry? _selectAllEntry;
    private IBrush? _selectionBrush;

    /// <summary>The selection layer.</summary>
    internal PdfSelectionLayer SelectionLayer => _selectionLayer;

    /// <summary>Over paper, in the accent.</summary>
    internal IBrush? SelectionBrush => _selectionBrush;

    /// <summary>The context menu, once it has been opened (tests).</summary>
    internal MenuSurface? PageContextMenu => _contextMenu;

    /// <summary>Whether the pointer selects text now: not in pan mode, and only with a document on show.</summary>
    internal bool CanSelectText => IsTextSelectionEnabled && InteractionMode != PageInteractionMode.Pan && _textCache is not null;

    /// <summary>Selects the text of the current page.</summary>
    public void SelectAll()
    {
        if (IsTextSelectionEnabled && _textCache is not null && PageCount > 0)
        {
            _selectionLayer.SelectPage(CurrentPageIndex);
        }
    }

    /// <summary>Removes the selection.</summary>
    public void ClearSelection() => _selectionLayer.Clear();

    /// <summary>Copies the selected text to the clipboard; a long selection goes once its pages have been read.</summary>
    public void CopySelection() => _ = CopySelectionAsync();

    /// <summary>The work of <see cref="CopySelection"/>. True when text reached the clipboard.</summary>
    internal async Task<bool> CopySelectionAsync()
    {
        await _selectionLayer.WhenComplete;
        var text = SelectedText;
        return text.Length > 0 && await ArcClipboard.CopyTextAsync(this, text);
    }

    internal PdfPageText? TryGetText(int pageIndex) => _textCache?.TryGet(pageIndex);

    internal Size PageSizeOf(int pageIndex) =>
        EffectivePageSource is { } source && pageIndex >= 0 && pageIndex < source.PageCount ? source.GetPageSize(pageIndex) : default;

    /// <summary>Starts reading a page's text unless it has arrived; only the asker's layer hears when it does. Never waits.</summary>
    internal async void RequestText(int pageIndex)
    {
        if (_textCache is not { } cache || cache.TryGet(pageIndex) is not null || pageIndex < 0 || pageIndex >= PageCount)
        {
            return;
        }
        try
        {
            await cache.GetAsync(pageIndex, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // The document is gone.
            return;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return;
        }
        if (ReferenceEquals(cache, _textCache))
        {
            _selectionLayer.OnTextArrived(pageIndex);
        }
    }

    internal void SetSelectedText(string text) => SelectedText = text;

    /// <summary>
    /// A selection color that shows on white paper in every theme (spec section 5.6: the accent's subtle tint). The token itself
    /// cannot be used: in a dark theme it is a light color meant for a dark ground, which all but vanishes on paper. So a light
    /// accent is darkened to a tone that shows, and made translucent, less so the darker it is, so the text stays readable.
    /// </summary>
    internal static Color SelectionColorFor(Color accent)
    {
        const double MaxLuminance = 0.22;
        const double DarkLuminance = 0.08;
        static double Linear(byte channel) => Math.Pow(channel / 255.0, 2.2);
        var luminance = 0.2126 * Linear(accent.R) + 0.7152 * Linear(accent.G) + 0.0722 * Linear(accent.B);
        var factor = luminance > MaxLuminance ? Math.Pow(MaxLuminance / luminance, 1 / 2.2) : 1;
        byte alpha = luminance < DarkLuminance ? (byte)0x33 : (byte)0x4D;
        return Color.FromArgb(alpha, (byte)(accent.R * factor), (byte)(accent.G * factor), (byte)(accent.B * factor));
    }

    private bool HandleSelectionKey(KeyEventArgs e)
    {
        var modifiers = e.KeyModifiers;
        if ((modifiers & (KeyModifiers.Control | KeyModifiers.Meta)) == 0 || (modifiers & (KeyModifiers.Alt | KeyModifiers.Shift)) != 0 || IsInTextField(e.Source))
        {
            return false;
        }
        switch (e.Key)
        {
            case Key.C when SelectedText.Length > 0 || _selectionLayer.HasSelection:
                CopySelection();
                return true;
            case Key.A when IsTextSelectionEnabled && _textCache is not null:
                SelectAll();
                return true;
            default:
                return false;
        }
    }

    /// <summary>The menu shows over the pages only: the toolbar and the sidebar have nothing to copy.</summary>
    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled || !CanSelectText || !IsPresenter(e.Source))
        {
            return;
        }
        Point position;
        if (!e.TryGetPosition(this, out position))
        {
            position = new Point(Bounds.Width / 2, Bounds.Height / 2);
        }
        else if (!PageHitTest(position, out _, out _))
        {
            return;
        }
        e.Handled = true;
        OpenContextMenu(position);
    }

    internal void OpenContextMenu(Point position)
    {
        if (_contextMenu is null)
        {
            _copyEntry = new MenuEntry { Icon = ArcIcons.Copy };
            _selectAllEntry = new MenuEntry { Icon = ArcIcons.TextSelect };
            _contextMenu = new MenuSurface
            {
                Variant = MenuSurfaceVariant.Context,
                ClosesOnViewportChange = true,
                Items = { _copyEntry, _selectAllEntry }
            };
            _contextMenu.ItemClicked += OnContextMenuClicked;
        }
        _copyEntry!.Header = PdfViewerLangResources.Get(PdfViewerLangResourceKind.Copy);
        _copyEntry.IsEnabled = _selectionLayer.HasSelection;
        _selectAllEntry!.Header = PdfViewerLangResources.Get(PdfViewerLangResourceKind.SelectAll);
        _contextMenu.OpenAt(this, position);
    }

    private void OnContextMenuClicked(object? sender, MenuEntryClickedEventArgs e)
    {
        if (ReferenceEquals(e.Entry, _copyEntry))
        {
            CopySelection();
        }
        else if (ReferenceEquals(e.Entry, _selectAllEntry))
        {
            SelectAll();
        }
    }

    private static bool IsPresenter(object? source) => source is Control { Name: PresenterPart };

    /// <summary>A press on the canvas between the pages reaches nothing else: it clears the selection like a press on a page does.</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.Handled && IsPresenter(e.Source) && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && _selectionLayer.HasSelection
            && InteractionMode != PageInteractionMode.Pan)
        {
            _selectionLayer.Clear();
        }
    }

    private static IBrush ToBrush(Color color) => new ImmutableSolidColorBrush(color);
}
