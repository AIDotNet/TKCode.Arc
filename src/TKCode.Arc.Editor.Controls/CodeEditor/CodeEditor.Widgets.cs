// Ported from vscode (src/vs/editor/browser/widget/codeEditor/codeEditorWidget.ts: addContentWidget, layoutContentWidget,
// removeContentWidget, addOverlayWidget, layoutOverlayWidget, removeOverlayWidget, changeViewZones; and
// src/vs/editor/browser/viewParts/viewZones/viewZones.ts: _computeWhitespaceProps, _heightInPixels, _addZone,
// _removeZone, _layoutZone, onLineMappingChanged; commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the zones are kept by the editor and put into each view model it creates (a font change makes a new one);
//  - the widgets are controls in the widget host (CodeEditorWidgetHost) over the view; they are laid out when the
//    host arranges, which the editor asks for on scrolling, cursor, content, zone and size changes;
//  - a zone's control is as wide as the text area and does not scroll horizontally (upstream's view zones scroll with
//    the text); its margin control fills the gutter next to it.

using Avalonia;
using Avalonia.Controls;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewLayout;
using EditorViewModel = TKCode.Arc.Editor.ViewModel.ViewModel;

namespace TKCode.Arc;

public sealed partial class CodeEditor
{
    private readonly Dictionary<string, ICodeEditorContentWidget> _contentWidgets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ICodeEditorOverlayWidget> _overlayWidgets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ViewZoneEntry> _viewZones = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ContentWidgetPlacement?> _contentWidgetPlacements = new(StringComparer.Ordinal);
    private CodeEditorWidgetHost? _widgetHost;
    private int _nextZoneId;

    /// <summary>The panel the widgets and zones are in (null before the template is applied).</summary>
    internal CodeEditorWidgetHost? WidgetHost => _widgetHost;

    // ---- content widgets

    /// <summary>Shows a content widget (upstream's addContentWidget).</summary>
    internal void AddContentWidget(ICodeEditorContentWidget widget)
    {
        if (_contentWidgets.ContainsKey(widget.Id))
        {
            UnexpectedErrors.Report(new InvalidOperationException($"Cannot have two content widgets with the same id {widget.Id}"));
            return;
        }

        _contentWidgets[widget.Id] = widget;
        AddWidgetControl(widget.Control);
        LayoutWidgets();
    }

    /// <summary>Places a content widget again after its position changed (upstream's layoutContentWidget).</summary>
    internal void LayoutContentWidget(ICodeEditorContentWidget widget) => LayoutWidgets();

    /// <summary>Removes a content widget (upstream's removeContentWidget).</summary>
    internal void RemoveContentWidget(ICodeEditorContentWidget widget)
    {
        if (_contentWidgets.Remove(widget.Id))
        {
            _contentWidgetPlacements.Remove(widget.Id);
            _widgetHost?.Children.Remove(widget.Control);
        }
    }

    /// <summary>Where a content widget was placed last, or null when it is hidden (for tests).</summary>
    internal ContentWidgetPlacement? GetContentWidgetPlacement(string id) => _contentWidgetPlacements.GetValueOrDefault(id);

    // ---- overlay widgets

    /// <summary>Shows an overlay widget (upstream's addOverlayWidget).</summary>
    internal void AddOverlayWidget(ICodeEditorOverlayWidget widget)
    {
        if (_overlayWidgets.ContainsKey(widget.Id))
        {
            UnexpectedErrors.Report(new InvalidOperationException($"Cannot have two overlay widgets with the same id {widget.Id}"));
            return;
        }

        _overlayWidgets[widget.Id] = widget;
        AddWidgetControl(widget.Control);
        LayoutWidgets();
    }

    /// <summary>Places an overlay widget again after its position changed (upstream's layoutOverlayWidget).</summary>
    internal void LayoutOverlayWidget(ICodeEditorOverlayWidget widget) => LayoutWidgets();

    /// <summary>Removes an overlay widget (upstream's removeOverlayWidget).</summary>
    internal void RemoveOverlayWidget(ICodeEditorOverlayWidget widget)
    {
        if (_overlayWidgets.Remove(widget.Id))
        {
            _widgetHost?.Children.Remove(widget.Control);
        }
    }

    // ---- view zones

    /// <summary>
    /// Adds, removes or lays out view zones (upstream's changeViewZones); the lines move and the zones' controls are
    /// placed when the callback returns.
    /// </summary>
    internal void ChangeViewZones(Action<IViewZoneChangeAccessor> callback)
    {
        var accessor = new ZoneAccessor(this);
        callback(accessor);
        if (accessor.Changed)
        {
            ApplyViewZones();
            LayoutWidgets();
        }
    }

    /// <summary>The zone <paramref name="id"/>, or null.</summary>
    internal CodeEditorViewZone? GetViewZone(string id) => _viewZones.GetValueOrDefault(id)?.Zone;

    /// <summary>Where a zone is now relative to the view (top and height), or null when it is not on screen or has no view.</summary>
    internal (double Top, double Height)? GetViewZoneBounds(string id)
    {
        if (ViewModel is not { } viewModel || !_viewZones.TryGetValue(id, out var entry) || entry.WhitespaceId is not { } whitespaceId)
        {
            return null;
        }

        foreach (var whitespace in viewModel.ViewLayout.GetWhitespaceViewportData())
        {
            if (whitespace.Id == whitespaceId)
            {
                return (whitespace.VerticalOffset - viewModel.ViewLayout.GetCurrentScrollTop(), whitespace.Height);
            }
        }

        return null;
    }

    /// <summary>The id of the zone that has a view layout whitespace, or null.</summary>
    internal string? GetZoneIdOfWhitespace(string whitespaceId)
    {
        foreach (var (id, entry) in _viewZones)
        {
            if (entry.WhitespaceId == whitespaceId && entry.ViewModel == ViewModel)
            {
                return id;
            }
        }

        return null;
    }

    private void AddWidgetControl(Control control)
    {
        if (_widgetHost is { } host && !host.Children.Contains(control))
        {
            host.Children.Add(control);
        }
    }

    private void AttachWidgetHost(CodeEditorWidgetHost? host)
    {
        if (_widgetHost is { } old)
        {
            old.ArrangeCallback = null;
            old.Children.Clear();
        }

        _widgetHost = host;
        if (host is null)
        {
            return;
        }

        host.ArrangeCallback = ArrangeWidgets;
        foreach (var entry in _viewZones.Values)
        {
            AddZoneControls(entry);
        }

        foreach (var widget in _contentWidgets.Values)
        {
            AddWidgetControl(widget.Control);
        }

        foreach (var widget in _overlayWidgets.Values.OrderBy(static w => w.GetPosition()?.StackOrdinal ?? 0))
        {
            AddWidgetControl(widget.Control);
        }
    }

    private void AddZoneControls(ViewZoneEntry entry)
    {
        if (_widgetHost is not { } host)
        {
            return;
        }

        // zones go below the widgets
        if (entry.Zone.Control is { } control && !host.Children.Contains(control))
        {
            host.Children.Insert(0, control);
        }

        if (entry.Zone.MarginControl is { } margin && !host.Children.Contains(margin))
        {
            host.Children.Insert(0, margin);
        }
    }

    /// <summary>Asks the widget host to place the widgets and zones again (scrolling, typing, resizing do).</summary>
    internal void LayoutWidgets()
    {
        if (_widgetHost is not { } host)
        {
            return;
        }

        foreach (var widget in _contentWidgets.Values)
        {
            // a widget without a position is hidden (upstream: display none)
            widget.Control.IsVisible = widget.GetPosition() is { Position: not null, Preference.Count: > 0 };
        }

        host.InvalidateMeasure();
        host.InvalidateArrange();
    }

    // The host's arrange pass: zones from the view layout's whitespaces, content widgets at their positions, overlay
    // widgets in their corners.
    private IReadOnlyList<(Control Control, Rect Bounds)> ArrangeWidgets(Size size)
    {
        var result = new List<(Control, Rect)>();
        if (_view is not { } view || ViewModel is not { } viewModel)
        {
            return result;
        }

        var gutterWidth = view.GutterWidth;
        // zones span the text area (upstream: the layout's contentWidth), left of the minimap and the vertical scroll bar
        var textRight = Math.Min(Math.Min(size.Width, view.TextAreaRight), size.Width - view.OverlayRightInset);
        var textWidth = Math.Max(0, textRight - gutterWidth);

        // zones: the visible whitespaces
        var byWhitespace = new Dictionary<string, ViewZoneEntry>(StringComparer.Ordinal);
        foreach (var entry in _viewZones.Values)
        {
            if (entry.WhitespaceId is { } id)
            {
                byWhitespace[id] = entry;
            }
        }

        var scrollTop = viewModel.ViewLayout.GetCurrentScrollTop();
        var visible = new HashSet<ViewZoneEntry>();
        foreach (var whitespace in viewModel.ViewLayout.GetWhitespaceViewportData())
        {
            if (!byWhitespace.TryGetValue(whitespace.Id, out var entry) || whitespace.Height <= 0)
            {
                continue;
            }

            var top = whitespace.VerticalOffset - scrollTop;
            visible.Add(entry);
            entry.Zone.OnDomNodeTop?.Invoke(top);
            if (entry.Zone.Control is { } control)
            {
                result.Add((control, new Rect(gutterWidth, top, textWidth, whitespace.Height)));
            }

            if (entry.Zone.MarginControl is { } margin)
            {
                result.Add((margin, new Rect(0, top, gutterWidth, whitespace.Height)));
            }
        }

        foreach (var entry in _viewZones.Values)
        {
            if (!visible.Contains(entry))
            {
                // off screen: out of the host's clip
                if (entry.Zone.Control is { } control)
                {
                    result.Add((control, OffScreen(control)));
                }

                if (entry.Zone.MarginControl is { } margin)
                {
                    result.Add((margin, OffScreen(margin)));
                }
            }
        }

        // content widgets
        foreach (var widget in _contentWidgets.Values)
        {
            var control = widget.Control;
            ContentWidgetPlacement? placement = null;
            if (control.IsVisible && widget.GetPosition() is { } position)
            {
                placement = view.PlaceContentWidget(position, control.DesiredSize);
            }

            _contentWidgetPlacements[widget.Id] = placement;
            result.Add((control, placement?.Bounds ?? OffScreen(control)));
            widget.AfterRender(placement?.Preference, placement?.Bounds.TopLeft);
        }

        // overlay widgets
        foreach (var widget in _overlayWidgets.Values)
        {
            var control = widget.Control;
            result.Add((control, view.PlaceOverlayWidget(widget.GetPosition(), control.DesiredSize, size)));
        }

        return result;
    }

    private static Rect OffScreen(Control control) =>
        new(-100000, -100000, Math.Max(0, control.DesiredSize.Width), Math.Max(0, control.DesiredSize.Height));

    // ---- zones in the view model (upstream's ViewZones)

    // Puts every zone into the current view model: computes its view line and height and inserts, changes or removes
    // its whitespace. Called after a zone change, a new view model and a change of the line mapping (wrapping, folding).
    private void ApplyViewZones()
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var lineHeight = _view?.LineHeight ?? 19;
        var computed = new List<(ViewZoneEntry Entry, double Height)>();
        viewModel.ChangeWhitespace(accessor =>
        {
            foreach (var entry in _viewZones.Values)
            {
                var (afterViewLineNumber, height, minWidth) = ComputeWhitespaceProps(viewModel, entry.Zone, lineHeight);
                if (entry.WhitespaceId is null || entry.ViewModel != viewModel)
                {
                    entry.WhitespaceId = accessor.InsertWhitespace(afterViewLineNumber, GetZoneOrdinal(entry.Zone), height, minWidth);
                    entry.ViewModel = viewModel;
                }
                else if (entry.AfterViewLineNumber != afterViewLineNumber || entry.Height != height)
                {
                    accessor.ChangeOneWhitespace(entry.WhitespaceId, afterViewLineNumber, height);
                }
                else
                {
                    continue;
                }

                entry.AfterViewLineNumber = afterViewLineNumber;
                entry.Height = height;
                computed.Add((entry, height));
            }
        });

        foreach (var (entry, height) in computed)
        {
            entry.Zone.OnComputedHeight?.Invoke(height);
        }
    }

    /// <summary>The line mapping changed (wrapping, folding): the zones' view lines change with it.</summary>
    internal void OnViewLineMappingChanged()
    {
        ApplyViewZones();
        LayoutWidgets();
    }

    private void OnViewModelCreatedForWidgets(EditorViewModel viewModel)
    {
        foreach (var entry in _viewZones.Values)
        {
            entry.WhitespaceId = null;
            entry.ViewModel = null;
        }

        ApplyViewZones();
        LayoutWidgets();
    }

    private (int AfterViewLineNumber, double Height, double MinWidth) ComputeWhitespaceProps(EditorViewModel viewModel, CodeEditorViewZone zone, double lineHeight)
    {
        var heightInPx = zone.HeightInPx ?? (zone.HeightInLines is { } lines ? lineHeight * lines : lineHeight);
        var minWidth = zone.MinWidthInPx;
        if (zone.AfterLineNumber <= 0)
        {
            return (0, heightInPx, minWidth);
        }

        var model = Model;
        TextPosition zoneAfterModelPosition;
        if (zone.AfterColumn is { } afterColumn)
        {
            zoneAfterModelPosition = model.ValidatePosition(new TextPosition(zone.AfterLineNumber, afterColumn));
        }
        else
        {
            var validAfterLineNumber = model.ValidatePosition(new TextPosition(zone.AfterLineNumber, 1)).LineNumber;
            zoneAfterModelPosition = new TextPosition(validAfterLineNumber, model.GetLineMaxColumn(validAfterLineNumber));
        }

        var zoneBeforeModelPosition = zoneAfterModelPosition.Column == model.GetLineMaxColumn(zoneAfterModelPosition.LineNumber)
            ? model.ValidatePosition(new TextPosition(zoneAfterModelPosition.LineNumber + 1, 1))
            : model.ValidatePosition(new TextPosition(zoneAfterModelPosition.LineNumber, zoneAfterModelPosition.Column + 1));

        var converter = viewModel.CoordinatesConverter;
        var viewPosition = converter.ConvertModelPositionToViewPosition(zoneAfterModelPosition, zone.AfterColumnAffinity ?? PositionAffinity.None, true);
        var isVisible = zone.ShowInHiddenAreas || converter.ModelPositionIsVisible(zoneBeforeModelPosition);
        return (viewPosition.LineNumber, isVisible ? heightInPx : 0, minWidth);
    }

    private static int GetZoneOrdinal(CodeEditorViewZone zone) => zone.Ordinal ?? zone.AfterColumn ?? 10000;

    private sealed class ViewZoneEntry(CodeEditorViewZone zone)
    {
        public CodeEditorViewZone Zone { get; } = zone;

        public string? WhitespaceId { get; set; }

        public EditorViewModel? ViewModel { get; set; }

        public int AfterViewLineNumber { get; set; } = -1;

        public double Height { get; set; } = -1;
    }

    private sealed class ZoneAccessor(CodeEditor editor) : IViewZoneChangeAccessor
    {
        public bool Changed { get; private set; }

        public string AddZone(CodeEditorViewZone zone)
        {
            Changed = true;
            var id = "zone" + (++editor._nextZoneId).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var entry = new ViewZoneEntry(zone);
            editor._viewZones[id] = entry;
            editor.AddZoneControls(entry);
            return id;
        }

        public void RemoveZone(string id)
        {
            if (!editor._viewZones.Remove(id, out var entry))
            {
                return;
            }

            Changed = true;
            if (entry.WhitespaceId is { } whitespaceId && entry.ViewModel is { } viewModel && viewModel == editor.ViewModel)
            {
                viewModel.ChangeWhitespace(accessor => accessor.RemoveWhitespace(whitespaceId));
            }

            if (editor._widgetHost is { } host)
            {
                if (entry.Zone.Control is { } control)
                {
                    host.Children.Remove(control);
                }

                if (entry.Zone.MarginControl is { } margin)
                {
                    host.Children.Remove(margin);
                }
            }
        }

        public void LayoutZone(string id)
        {
            if (editor._viewZones.ContainsKey(id))
            {
                Changed = true;
            }
        }
    }
}
