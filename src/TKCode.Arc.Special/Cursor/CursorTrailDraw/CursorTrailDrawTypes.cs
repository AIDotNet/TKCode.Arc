using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The tools of a <see cref="CursorTrailDraw"/> board.</summary>
public enum CursorTrailDrawTool
{
    /// <summary>Ink that stays on the board; rough shapes snap clean when snapping is on.</summary>
    Pen,

    /// <summary>A pointer that leaves a short trail which fades on its own; nothing is added to the board.</summary>
    Laser
}

/// <summary>What an item on a <see cref="CursorTrailDraw"/> board is.</summary>
public enum CursorTrailDrawShape
{
    /// <summary>Freehand ink, kept as drawn.</summary>
    Stroke,

    /// <summary>A straight line (two points).</summary>
    Line,

    /// <summary>A circle (its outline as points).</summary>
    Circle,

    /// <summary>An ellipse (its outline as points).</summary>
    Ellipse,

    /// <summary>A rectangle (four corners).</summary>
    Rectangle,

    /// <summary>A square (four corners).</summary>
    Square,

    /// <summary>A triangle (three corners).</summary>
    Triangle,

    /// <summary>Any other four sided shape (four corners).</summary>
    Quad
}

/// <summary>Why the items of a <see cref="CursorTrailDraw"/> board changed.</summary>
public enum CursorTrailDrawChange
{
    /// <summary>A freehand stroke landed.</summary>
    Added,

    /// <summary>A stroke landed and snapped to a clean shape.</summary>
    Snapped,

    /// <summary>A snapped shape went back to the stroke as drawn.</summary>
    KeptAsDrawn,

    /// <summary>The last item was removed.</summary>
    Undone,

    /// <summary>Every item was removed.</summary>
    Cleared
}

/// <summary>
/// One item on a <see cref="CursorTrailDraw"/> board. Coordinates are board units: the board is always 1000 units wide
/// and as tall as its shape needs, so a saved sketch scales with the board.
/// </summary>
/// <param name="Id">A stable identifier.</param>
/// <param name="Shape">What the item is.</param>
/// <param name="InkId">The <see cref="CursorTrailDrawInk.Id"/> of its ink.</param>
/// <param name="Width">Its line width in board units.</param>
/// <param name="Points">
/// Its centerline in board units: the samples of a stroke, the two ends of a line, the corners of a polygon, or points
/// around a circle or ellipse. Polygons and ovals are closed; the first point is not repeated.
/// </param>
public sealed record CursorTrailDrawItem(string Id, CursorTrailDrawShape Shape, string InkId, double Width, IReadOnlyList<Point> Points);

/// <summary>
/// One ink in the toolbar of a <see cref="CursorTrailDraw"/> board. Without a <see cref="Brush"/> an ink named
/// <c>accent</c> uses the board's accent, one named <c>red</c> the theme's danger color and any other the foreground.
/// </summary>
public class CursorTrailDrawInk : AvaloniaObject
{
    /// <summary>The identifier stored on items drawn with this ink.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<CursorTrailDrawInk, string>(nameof(Id), string.Empty);

    /// <summary>The name of the ink, read out by assistive technology ("Ink color").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<CursorTrailDrawInk, string>(nameof(Label), string.Empty);

    /// <summary>The color of the ink; null picks a theme color by <see cref="Id"/>.</summary>
    public static readonly StyledProperty<IBrush?> BrushProperty =
        AvaloniaProperty.Register<CursorTrailDrawInk, IBrush?>(nameof(Brush));

    /// <summary>Whether the ink can be picked.</summary>
    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<CursorTrailDrawInk, bool>(nameof(IsEnabled), true);

    /// <summary>Whether the ink is shown in the toolbar. Items drawn with a hidden ink keep their color.</summary>
    public static readonly StyledProperty<bool> IsVisibleProperty =
        AvaloniaProperty.Register<CursorTrailDrawInk, bool>(nameof(IsVisible), true);

    /// <summary>Creates an ink; set <see cref="Id"/> and <see cref="Label"/>.</summary>
    public CursorTrailDrawInk()
    {
    }

    /// <summary>Creates an ink with an identifier, a name and an optional color.</summary>
    public CursorTrailDrawInk(string id, string label, IBrush? brush = null)
    {
        Id = id;
        Label = label;
        Brush = brush;
    }

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="BrushProperty"/>
    public IBrush? Brush { get => GetValue(BrushProperty); set => SetValue(BrushProperty, value); }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public bool IsEnabled { get => GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }

    /// <inheritdoc cref="IsVisibleProperty"/>
    public bool IsVisible { get => GetValue(IsVisibleProperty); set => SetValue(IsVisibleProperty, value); }
}

/// <summary>Arguments of <see cref="CursorTrailDraw.ItemsChanged"/>.</summary>
public sealed class CursorTrailDrawItemsChangedEventArgs : RoutedEventArgs
{
    internal CursorTrailDrawItemsChangedEventArgs(RoutedEvent routedEvent, object source, CursorTrailDrawChange change,
        CursorTrailDrawItem? item, IReadOnlyList<CursorTrailDrawItem> items)
        : base(routedEvent, source)
    {
        Change = change;
        Item = item;
        Items = items;
    }

    /// <summary>Why the items changed.</summary>
    public CursorTrailDrawChange Change { get; }

    /// <summary>The item that landed, snapped, was kept or removed; null when the board was cleared.</summary>
    public CursorTrailDrawItem? Item { get; }

    /// <summary>Every item on the board after the change, oldest first.</summary>
    public IReadOnlyList<CursorTrailDrawItem> Items { get; }
}
