// Ported from vscode (src/vs/editor/browser/editorBrowser.ts: IContentWidget, IContentWidgetPosition,
// ContentWidgetPositionPreference, IOverlayWidget, IOverlayWidgetPosition, OverlayWidgetPositionPreference, IViewZone,
// IViewZoneChangeAccessor; commit b57d5c9e), MIT license.
// Differences from upstream:
//  - a widget's DOM node is an Avalonia Control (Control / MarginControl), which the editor puts into its widget host
//    (CodeEditorWidgetHost, a panel over the view): what it does not cover still reaches the text;
//  - content widgets never overflow the editor (allowEditorOverflow is not ported: they are clipped to the editor);
//  - beforeRender's preferred size is the control's desired size (it is measured against the text area);
//  - a view zone's id is the editor's own, not the view layout's whitespace id (the zones outlive a new view model).

using Avalonia;
using Avalonia.Controls;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc;

/// <summary>Where a content widget goes relative to its position (upstream's ContentWidgetPositionPreference).</summary>
internal enum ContentWidgetPositionPreference
{
    /// <summary>Exactly at the position (its top-left corner on the top of the line).</summary>
    Exact,

    /// <summary>Above the line of the position.</summary>
    Above,

    /// <summary>Below the line of the position.</summary>
    Below,
}

/// <summary>Where a content widget is anchored (upstream's IContentWidgetPosition).</summary>
/// <param name="Position">The model position the widget touches; null hides it.</param>
/// <param name="Preference">The placements to try, in order: the first that fits, else the first.</param>
internal sealed record ContentWidgetPosition(TextPosition? Position, IReadOnlyList<ContentWidgetPositionPreference> Preference)
{
    /// <summary>A second position on the same line the widget should touch if it can (the start of a word being completed).</summary>
    public TextPosition? SecondaryPosition { get; init; }

    /// <summary>Which side of injected text the position sticks to.</summary>
    public PositionAffinity? PositionAffinity { get; init; }
}

/// <summary>A control anchored to a text position, scrolling with it (upstream's IContentWidget): suggest, hover, parameter hints.</summary>
internal interface ICodeEditorContentWidget
{
    /// <summary>A unique id.</summary>
    string Id { get; }

    /// <summary>The control the editor shows.</summary>
    Control Control { get; }

    /// <summary>Where to show it now; null hides it. The editor asks again on every layout.</summary>
    ContentWidgetPosition? GetPosition();

    /// <summary>Called after a layout with the placement used (null when the widget is hidden).</summary>
    void AfterRender(ContentWidgetPositionPreference? position, Point? coordinate)
    {
    }
}

/// <summary>A corner an overlay widget sits in (upstream's OverlayWidgetPositionPreference).</summary>
internal enum OverlayWidgetPositionPreference
{
    /// <summary>The top right corner (the find widget).</summary>
    TopRightCorner,

    /// <summary>The bottom right corner.</summary>
    BottomRightCorner,

    /// <summary>Centered at the top.</summary>
    TopCenter,
}

/// <summary>Where an overlay widget sits (upstream's IOverlayWidgetPosition): a corner, or coordinates relative to the editor.</summary>
internal sealed record OverlayWidgetPosition(OverlayWidgetPositionPreference? Preference)
{
    /// <summary>The top-left corner relative to the editor's view, instead of a corner preference.</summary>
    public Point? Coordinates { get; init; }

    /// <summary>Widgets with a higher ordinal are on top.</summary>
    public int StackOrdinal { get; init; }
}

/// <summary>A control fixed to the editor, not the text (upstream's IOverlayWidget): the find widget.</summary>
internal interface ICodeEditorOverlayWidget
{
    /// <summary>A unique id.</summary>
    string Id { get; }

    /// <summary>The control the editor shows.</summary>
    Control Control { get; }

    /// <summary>Where to show it now; null leaves it where the widget placed itself (top-left at 0, 0).</summary>
    OverlayWidgetPosition? GetPosition();
}

/// <summary>
/// Space between lines that a control fills (upstream's IViewZone): peek views, inline diffs, code lens rows. The lines
/// below move down by its height.
/// </summary>
internal sealed class CodeEditorViewZone
{
    /// <summary>The model line the zone goes after; 0 puts it above the first line.</summary>
    public int AfterLineNumber { get; set; }

    /// <summary>The model column the zone goes after, for wrapped lines; null after the whole line.</summary>
    public int? AfterColumn { get; set; }

    /// <summary>Which side of injected text <see cref="AfterColumn"/> sticks to.</summary>
    public PositionAffinity? AfterColumnAffinity { get; set; }

    /// <summary>Whether the zone shows when its line is folded away.</summary>
    public bool ShowInHiddenAreas { get; set; }

    /// <summary>The order of zones after the same line (default: after the line number).</summary>
    public int? Ordinal { get; set; }

    /// <summary>Whether a press in the zone leaves the editor's selection alone.</summary>
    public bool SuppressMouseDown { get; set; }

    /// <summary>The height in lines (used when <see cref="HeightInPx"/> is null; one line when both are null).</summary>
    public double? HeightInLines { get; set; }

    /// <summary>The height in pixels.</summary>
    public double? HeightInPx { get; set; }

    /// <summary>The smallest width of the content the zone needs (the horizontal scroll range grows to it).</summary>
    public double MinWidthInPx { get; set; }

    /// <summary>The control over the text area, or null for empty space.</summary>
    public Control? Control { get; set; }

    /// <summary>A control in the gutter next to the zone, or null.</summary>
    public Control? MarginControl { get; set; }

    /// <summary>Called with the zone's top relative to the view when it is laid out.</summary>
    public Action<double>? OnDomNodeTop { get; set; }

    /// <summary>Called with the zone's height in pixels when it was computed.</summary>
    public Action<double>? OnComputedHeight { get; set; }
}

/// <summary>Adds, removes and lays out view zones inside <see cref="CodeEditor.ChangeViewZones"/> (upstream's IViewZoneChangeAccessor).</summary>
internal interface IViewZoneChangeAccessor
{
    /// <summary>Adds a zone; returns its id.</summary>
    string AddZone(CodeEditorViewZone zone);

    /// <summary>Removes the zone <paramref name="id"/>.</summary>
    void RemoveZone(string id);

    /// <summary>Applies a change of the zone's line or height.</summary>
    void LayoutZone(string id);
}

/// <summary>Where the editor put a content widget (for tests and AfterRender).</summary>
internal sealed record ContentWidgetPlacement(Rect Bounds, ContentWidgetPositionPreference Preference);

/// <summary>
/// The panel over the editor's view that hosts view zones, content widgets and overlay widgets (bottom to top). It has
/// no background, so pointer input where no widget is goes to the view underneath; the editor computes where each
/// child goes (CodeEditor.Widgets.cs).
/// </summary>
internal sealed class CodeEditorWidgetHost : Panel
{
    internal Func<Size, IReadOnlyList<(Control Control, Rect Bounds)>>? ArrangeCallback { get; set; }

    /// <summary>How many times the host arranged its children (for tests).</summary>
    internal int ArrangeCount { get; private set; }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(availableSize);
        }

        // the host never asks for room: the editor's size is the view's
        return default;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        ArrangeCount++;
        var placed = new HashSet<Control>();
        if (ArrangeCallback?.Invoke(finalSize) is { } layout)
        {
            foreach (var (control, bounds) in layout)
            {
                control.Arrange(bounds);
                placed.Add(control);
            }
        }

        foreach (var child in Children)
        {
            if (!placed.Contains(child))
            {
                child.Arrange(default);
            }
        }

        return finalSize;
    }
}
