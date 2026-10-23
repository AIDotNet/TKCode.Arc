using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>What an <see cref="AiPanelText"/> is on the page; its theme picks the type and color from it.</summary>
internal enum AiPanelTextRole
{
    /// <summary>A heading block: display font 14 / 15.4, weight 500, Foreground.</summary>
    Heading,

    /// <summary>A paragraph block: 14 / 22.4, TextSecondary.</summary>
    Paragraph,

    /// <summary>A table header cell: 12 / 16.8, TextMuted.</summary>
    ColumnHeader,

    /// <summary>A row header cell (the first column): 14 / 19.6, weight 500, Foreground.</summary>
    RowHeader,

    /// <summary>A table cell: 14 / 19.6, TextSecondary.</summary>
    Cell
}

/// <summary>
/// One run of page text (a heading, a paragraph or a table cell) that shows part of the page selection. It never takes
/// the pointer or focus itself: <see cref="AiPanelPageView"/> owns the selection across texts and sets
/// <c>SelectionStart</c> / <c>SelectionEnd</c> here (the base draws the SurfaceMuted highlight and the Foreground text over
/// it). The role selects the look in the theme.
/// </summary>
internal sealed class AiPanelText : SelectableTextBlock
{
    public const string HeadingPseudoClass = ":heading";
    public const string ParagraphPseudoClass = ":paragraph";
    public const string ColumnHeaderPseudoClass = ":columnheader";
    public const string RowHeaderPseudoClass = ":rowheader";
    public const string CellPseudoClass = ":cell";
    public const string NumericPseudoClass = ":numeric";

    static AiPanelText()
    {
        FocusableProperty.OverrideDefaultValue<AiPanelText>(false);
        IsHitTestVisibleProperty.OverrideDefaultValue<AiPanelText>(false);
    }

    public AiPanelText(AiPanelTextRole role, bool numeric = false)
    {
        Role = role;
        PseudoClasses.Set(HeadingPseudoClass, role == AiPanelTextRole.Heading);
        PseudoClasses.Set(ParagraphPseudoClass, role == AiPanelTextRole.Paragraph);
        PseudoClasses.Set(ColumnHeaderPseudoClass, role == AiPanelTextRole.ColumnHeader);
        PseudoClasses.Set(RowHeaderPseudoClass, role == AiPanelTextRole.RowHeader);
        PseudoClasses.Set(CellPseudoClass, role == AiPanelTextRole.Cell);
        PseudoClasses.Set(NumericPseudoClass, numeric);
    }

    /// <summary>What the text is on the page.</summary>
    public AiPanelTextRole Role { get; }

    /// <summary>The length of the text (selection offsets run from 0 to it).</summary>
    public int Length => Text?.Length ?? 0;

    /// <summary>Selects <paramref name="start"/>..<paramref name="end"/>; equal ends clear the selection.</summary>
    public void Select(int start, int end)
    {
        var length = Length;
        start = Math.Clamp(start, 0, length);
        end = Math.Clamp(end, 0, length);
        if (start == end)
        {
            start = end = 0;
        }
        if (SelectionStart != start)
        {
            SetCurrentValue(SelectionStartProperty, start);
        }
        if (SelectionEnd != end)
        {
            SetCurrentValue(SelectionEndProperty, end);
        }
    }

    /// <summary>The character offset nearest to <paramref name="point"/> (in this text's coordinates).</summary>
    public int OffsetAt(Point point)
    {
        if (Length == 0)
        {
            return 0;
        }
        var padding = Padding;
        var local = new Point(point.X - padding.Left, point.Y - padding.Top);
        return Math.Clamp(TextLayout.HitTestPoint(local).TextPosition, 0, Length);
    }

    /// <summary>The line rectangles of <paramref name="start"/>..<paramref name="end"/> in this text's coordinates.</summary>
    public IEnumerable<Rect> RangeRects(int start, int end)
    {
        if (end <= start || Length == 0)
        {
            return [];
        }
        var padding = Padding;
        var origin = new Vector(padding.Left, padding.Top);
        return TextLayout.HitTestTextRange(start, end - start).Select(r => r.Translate(origin));
    }

    protected override Type StyleKeyOverride => typeof(AiPanelText);
}
