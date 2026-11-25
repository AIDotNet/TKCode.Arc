// The bar of a hidden unchanged region (upstream's CollapsedCodeOverlayWidget in
// src/vs/editor/browser/widget/diffEditor/features/hideUnchangedRegionsFeature.ts, commit b57d5c9e; MIT license).
// Differences: a click shows the whole region (upstream: "N hidden lines" also expands it fully, and drag handles and
// "show more" links expand it in steps); the text is not localized yet.

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Editor.Diff;

namespace TKCode.Arc;

/// <summary>A bar that stands for hidden unchanged lines and shows them when clicked.</summary>
internal sealed class DiffEditorCollapsedRegionView : Control
{
    private readonly DiffEditor _owner;

    public DiffEditorCollapsedRegionView(DiffEditor owner, UnchangedRegion region, int hiddenLineCount, bool showLabel = true)
    {
        _owner = owner;
        ShowLabel = showLabel;
        Region = region;
        HiddenLineCount = hiddenLineCount;
        Cursor = new Cursor(StandardCursorType.Hand);
        ClipToBounds = true;
    }

    /// <summary>The region this bar shows.</summary>
    public UnchangedRegion Region { get; }

    /// <summary>Whether the chevron and the count show (false: the part of the bar in the gutter).</summary>
    public bool ShowLabel { get; }

    /// <summary>The number of lines hidden on this side.</summary>
    public int HiddenLineCount { get; }

    /// <summary>The bar's label (upstream: "{0} hidden lines").</summary>
    public string Text => HiddenLineCount == 1 ? "1 hidden line" : string.Create(CultureInfo.InvariantCulture, $"{HiddenLineCount} hidden lines");

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            e.Handled = true;
            Region.ShowAll();
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (_owner.CollapsedRegionBackground is { } background)
        {
            context.FillRectangle(background, bounds);
        }

        if (!ShowLabel || _owner.CollapsedRegionForeground is not { } foreground)
        {
            return;
        }

        // a double chevron (unfold) and the count
        var size = Math.Min(16, bounds.Height);
        var glyphCell = new Rect(4, (bounds.Height - size) / 2, size, size);
        DiffEditor.DrawGlyph(context, UnfoldGlyph, foreground, glyphCell);

        var text = new FormattedText(
            Text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(TextElement.GetFontFamily(this)),
            Math.Max(1, TextElement.GetFontSize(this) - 1),
            foreground);
        context.DrawText(text, new Point(glyphCell.Right + 6, (bounds.Height - text.Height) / 2));
    }

    /// <summary>Two chevrons pointing away from each other, in a 16 × 16 box.</summary>
    private static readonly Geometry UnfoldGlyph = Geometry.Parse("M8,1.5 L12,5.5 L11,6.5 L8,3.5 L5,6.5 L4,5.5 Z M8,14.5 L4,10.5 L5,9.5 L8,12.5 L11,9.5 L12,10.5 Z");
}
