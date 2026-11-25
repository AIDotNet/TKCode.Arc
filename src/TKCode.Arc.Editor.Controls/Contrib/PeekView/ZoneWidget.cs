// Ported from vscode (src/vs/editor/contrib/zoneWidget/browser/zoneWidget.ts: ZoneWidget's show, hide, _showImpl,
// _decoratingElementsHeight, _getMaximumHeightInLines and the arrow; commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the widget is the view zone's own control (upstream: an empty zone plus an overlay widget that follows its top), so
//    it scrolls with the text; the gutter next to it gets a margin control with the frame lines; it starts where the
//    text starts, not at the editor's left edge;
//  - the arrow is a small triangle drawn at the top of the zone, at the x of the position's column;
//  - no sash to resize the zone, no accessibility or keyboard trap; the position marker is a decoration that tracks
//    where the zone goes (upstream's _positionMarkerId).

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

/// <summary>The options of a zone widget (upstream's IOptions).</summary>
internal sealed record ZoneWidgetOptions
{
    public bool ShowFrame { get; init; } = true;

    public bool ShowArrow { get; init; } = true;

    /// <summary>The thickness of the frame lines; one ninth of the line height when null.</summary>
    public double? FrameWidth { get; init; }

    /// <summary>Whether showing the zone leaves the editor's selection alone.</summary>
    public bool KeepEditorSelection { get; init; }
}

/// <summary>A widget shown in a view zone below a line, with a frame and an arrow pointing at a position (upstream's ZoneWidget).</summary>
internal abstract class ZoneWidget : IDisposable
{
    private readonly Grid _root;
    private readonly Canvas _arrowHost;
    private readonly Polygon _arrow;
    private readonly Border _margin;
    private CodeEditorViewZone? _viewZone;
    private string? _viewZoneId;
    private string[] _positionMarker = [];
    private double _arrowHeight;

    protected ZoneWidget(CodeEditor editor, ZoneWidgetOptions options)
    {
        Editor = editor;
        Options = options;
        Surface = new CodeEditorPeekSurface();
        Container = new Border();
        _arrow = new Polygon();
        _arrowHost = new Canvas { Children = { _arrow }, ClipToBounds = false };
        _root = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        _root.Children.Add(_arrowHost);
        Grid.SetRow(Container, 1);
        _root.Children.Add(Container);
        Surface.Child = _root;
        Surface.Background = null;
        Surface.BindBrush(_arrow, Shape.FillProperty, CodeEditorPeekSurface.FrameBrushProperty);
        Surface.BindBrush(Container, Border.BorderBrushProperty, CodeEditorPeekSurface.FrameBrushProperty);

        // the gutter next to the zone: the frame lines go across it
        _margin = new Border();
        Surface.BindBrush(_margin, Border.BorderBrushProperty, CodeEditorPeekSurface.FrameBrushProperty);
        _marginHost = new CodeEditorPeekSurface { Background = null, Child = _margin };
    }

    private readonly CodeEditorPeekSurface _marginHost;

    protected CodeEditor Editor { get; }

    protected ZoneWidgetOptions Options { get; }

    /// <summary>The surface whose brushes the widget's parts use.</summary>
    protected CodeEditorPeekSurface Surface { get; }

    /// <summary>The framed area below the arrow; subclasses put their content in it.</summary>
    protected Border Container { get; }

    /// <summary>The zone's control (for tests and layout).</summary>
    internal Control Control => Surface;

    /// <summary>The id of the zone, or null when hidden.</summary>
    internal string? ViewZoneId => _viewZoneId;

    /// <summary>Where the arrow points, tracked through edits (upstream's position).</summary>
    public TextPosition? Position
    {
        get
        {
            if (_positionMarker.Length == 0)
            {
                return null;
            }

            return Editor.Model.GetDecorationRange(_positionMarker[0])?.Start;
        }
    }

    public bool IsShowing => _viewZoneId is not null;

    /// <summary>Shows the widget below the line of <paramref name="where"/>'s start (upstream's show).</summary>
    public void Show(TextRange where, double heightInLines)
    {
        var position = where.Start;
        var lineHeight = Editor.View?.LineHeight ?? 18;

        // adjust heightInLines to the viewport
        var maxHeightInLines = Math.Max(12, Editor.GetLayoutSize().Height / lineHeight * 0.8);
        heightInLines = Math.Min(heightInLines, maxHeightInLines);

        _arrowHeight = Options.ShowArrow ? Math.Round(lineHeight / 3) : 0;
        var frameThickness = Options.ShowFrame ? Options.FrameWidth ?? Math.Round(lineHeight / 9) : 0;
        Container.BorderThickness = new Thickness(0, frameThickness, 0, frameThickness);
        _margin.BorderThickness = Container.BorderThickness;
        _margin.Margin = new Thickness(0, _arrowHeight, 0, 0);
        _arrowHost.Height = _arrowHeight;

        Editor.ChangeViewZones(accessor =>
        {
            if (_viewZoneId is { } old)
            {
                accessor.RemoveZone(old);
            }

            _viewZone = new CodeEditorViewZone
            {
                AfterLineNumber = position.LineNumber,
                AfterColumn = position.Column,
                HeightInLines = heightInLines,
                Control = Surface,
                MarginControl = _marginHost,
                SuppressMouseDown = true,
                OnDomNodeTop = _ => UpdateArrow(),
                OnComputedHeight = OnComputedHeight,
            };
            _viewZoneId = accessor.AddZone(_viewZone);
        });

        _positionMarker = Editor.Model.DeltaDecorations(_positionMarker,
            [new ModelDeltaDecoration(new TextRange(position), new ModelDecorationOptions("zone-widget-position") { Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges })]);

        var containerHeight = (heightInLines * lineHeight) - DecoratingElementsHeight(lineHeight, frameThickness);
        DoLayout(containerHeight);
        UpdateArrow();

        if (!Options.KeepEditorSelection)
        {
            Editor.SetSelection(TextSelection.FromRange(where));
        }

        var model = Editor.Model;
        var revealed = model.ValidateRange(new TextRange(where.StartLineNumber, 1, where.EndLineNumber + 1, 1));
        RevealRange(revealed, revealed.StartLineNumber == model.GetLineCount());
    }

    /// <summary>Removes the zone (upstream's hide).</summary>
    public void Hide()
    {
        if (_viewZoneId is { } id)
        {
            Editor.ChangeViewZones(accessor => accessor.RemoveZone(id));
            _viewZoneId = null;
            _viewZone = null;
        }

        if (_positionMarker.Length > 0 && !Editor.Model.IsDisposed())
        {
            _positionMarker = Editor.Model.DeltaDecorations(_positionMarker, []);
        }
    }

    public virtual void Dispose() => Hide();

    /// <summary>Lays the content out for a container height (upstream's _doLayout).</summary>
    protected abstract void DoLayout(double heightInPixel);

    /// <summary>Reveals the range the widget belongs to (upstream's revealRange): the zone below it should be in view.</summary>
    protected virtual void RevealRange(TextRange range, bool isLastLine)
    {
        if (isLastLine)
        {
            Editor.RevealLine(range.StartLineNumber, VerticalRevealType.CenterIfOutsideViewport, ScrollType.Immediate);
        }
        else
        {
            Editor.RevealRange(range, VerticalRevealType.CenterIfOutsideViewport, ScrollType.Immediate);
        }
    }

    private void OnComputedHeight(double height)
    {
        Surface.Height = height;
        _marginHost.Height = height;
    }

    private static double DecoratingElementsHeight(double lineHeight, double frameThickness) =>
        (2 * Math.Round(lineHeight / 3)) + (2 * frameThickness);

    // the arrow under the position, a triangle pointing up (upstream's Arrow)
    private void UpdateArrow()
    {
        if (!Options.ShowArrow || Editor.View is not { } view || view.ViewModel is not { } viewModel || Position is not { } position)
        {
            _arrow.IsVisible = false;
            return;
        }

        var viewPosition = viewModel.CoordinatesConverter.ConvertModelPositionToViewPosition(position);
        var x = view.GetX(viewPosition.LineNumber, viewPosition.Column) - view.GutterWidth;
        var h = _arrowHeight;
        _arrow.IsVisible = true;
        _arrow.Points = [new Point(x - h, h), new Point(x, 0), new Point(x + h, h)];
    }
}
