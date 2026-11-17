using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Terminal.Core;

namespace TKCode.Terminal.Avalonia;

/// <summary>
/// The cursor (05-rendering.md §9.3) and the blink clock shared by the cursor and SGR 5 text (§9.8). One one-shot
/// <see cref="DispatcherTimer"/> is armed for the next phase change of whichever blinks; it does not run when nothing
/// blinks, the view is hidden or detached, or (for the cursor) the view does not have focus.
/// </summary>
public sealed partial class TerminalView
{
    private static readonly TimeSpan s_textBlinkHalfPeriod = TimeSpan.FromMilliseconds(500);
    private static readonly Stopwatch s_clock = Stopwatch.StartNew();

    private DispatcherTimer? _blinkTimer;
    private TimeSpan _cursorEpoch;
    private bool _hasBlinkingText;
    private bool _textBlinkOn = true;

    /// <summary>Tests: the time the blink phases are computed from.</summary>
    internal Func<TimeSpan> Clock { get; set; } = static () => s_clock.Elapsed;

    /// <summary>Tests: whether the blink timer is armed.</summary>
    internal bool IsBlinkTimerRunning => _blinkTimer?.IsEnabled == true;

    /// <summary>Tests: the cursor drawn by the last render (bounds in DIP and shape), or <c>null</c> when none was.</summary>
    internal (Rect Bounds, CursorStyle Shape, Color Color)? DrawnCursor { get; private set; }

    /// <summary>Tests: the timer fired.</summary>
    internal void BlinkTick() => OnBlinkTick(null, EventArgs.Empty);

    /// <summary>Whether the cursor blinks now: focused, visible, and asked to by <see cref="CursorBlink"/> or the program.</summary>
    private bool CursorBlinks =>
        IsFocused && CursorBlinkInterval > TimeSpan.Zero && Snapshot is { Cursor.Visible: true } frame &&
        (CursorBlink == TerminalCursorBlink.Always || (CursorBlink == TerminalCursorBlink.Program && frame.Cursor.Blinking));

    /// <summary>Whether the cursor is in its visible phase (always, when it does not blink).</summary>
    internal bool CursorPhaseOn
    {
        get
        {
            if (!CursorBlinks)
            {
                return true;
            }
            var elapsed = Clock() - _cursorEpoch;
            return elapsed < TimeSpan.Zero || (elapsed.Ticks / CursorBlinkInterval.Ticks) % 2 == 0;
        }
    }

    /// <summary>Typing and new output show the cursor again and restart its blink.</summary>
    private void ResetCursorBlink() => _cursorEpoch = Clock();

    private void OnOutput() => ResetCursorBlink();

    private void UpdateBlinkTimer()
    {
        var text = TextBlink && _hasBlinkingText;
        var cursor = CursorBlinks;
        if ((!text && !cursor) || !IsEffectivelyVisible || _topLevel is null)
        {
            StopBlinkTimer();
            return;
        }
        var now = Clock();
        var next = TimeSpan.MaxValue;
        if (text)
        {
            next = TimeSpan.FromTicks(s_textBlinkHalfPeriod.Ticks - now.Ticks % s_textBlinkHalfPeriod.Ticks);
        }
        if (cursor)
        {
            var elapsed = Math.Max(0, (now - _cursorEpoch).Ticks);
            var untilCursor = TimeSpan.FromTicks(CursorBlinkInterval.Ticks - elapsed % CursorBlinkInterval.Ticks);
            next = untilCursor < next ? untilCursor : next;
        }
        if (_blinkTimer is null)
        {
            _blinkTimer = new DispatcherTimer(DispatcherPriority.Render);
            _blinkTimer.Tick += OnBlinkTick;
        }
        _blinkTimer.Stop();
        _blinkTimer.Interval = next < TimeSpan.FromMilliseconds(16) ? TimeSpan.FromMilliseconds(16) : next;
        _blinkTimer.Start();
    }

    private void StopBlinkTimer() => _blinkTimer?.Stop();

    // One shot: the render this asks for arms the timer again for the next phase change, if anything still blinks.
    private void OnBlinkTick(object? sender, EventArgs e)
    {
        StopBlinkTimer();
        InvalidateVisual();
    }

    private void UpdateTextBlinkPhase() =>
        _textBlinkOn = !TextBlink || (Clock().Ticks / s_textBlinkHalfPeriod.Ticks) % 2 == 0;

    private void DrawCursor(DrawingContext context, FrameSnapshot frame, TerminalPalette palette, CellMetrics m, double pad)
    {
        DrawnCursor = null;
        var cursor = frame.Cursor;
        if (!cursor.Visible || cursor.Row >= frame.Rows.Length || cursor.Column >= frame.Columns)
        {
            return;
        }
        var shape = cursor.Style;
        if (!IsFocused)
        {
            switch (UnfocusedCursorStyle)
            {
                case TerminalUnfocusedCursorStyle.None:
                    return;
                case TerminalUnfocusedCursorStyle.Hollow:
                    shape = CursorStyle.BlockHollow;
                    break;
            }
        }
        else if (!CursorPhaseOn)
        {
            return;
        }

        // A cursor on a wide character (or its tail) covers both of its cells.
        var row = frame.Rows[cursor.Row];
        var column = Math.Min(cursor.Column, row.Cells.Length - 1);
        var columns = 1;
        if ((cursor.WideTail || TCell.Width(row.Cells[column]) == CellWidth.SpacerTail) && column > 0 &&
            TCell.Width(row.Cells[column - 1]) == CellWidth.Wide)
        {
            column--;
            columns = 2;
        }
        else if (TCell.Width(row.Cells[column]) == CellWidth.Wide && column + 1 < row.Cells.Length)
        {
            columns = 2;
        }
        var cell = row.Cells[column];
        var scale = m.Scaling;
        var rect = new Rect((pad + column * m.CellWidth) / scale, (pad + cursor.Row * m.CellHeight) / scale, columns * m.CellWidth / scale, m.CellHeight / scale);

        // Alacritty's guard: a cursor that would vanish on the cell's background takes the cell's foreground.
        var (cellFg, cellBg) = ResolveCellColors(row, cell, palette, contrastExempt: false);
        var cursorColor = palette.Cursor ?? palette.Foreground;
        if (TerminalColorMath.ContrastRatio(cursorColor, cellBg) < 1.5)
        {
            cursorColor = cellFg;
        }
        var cursorBrush = Brush(cursorColor);
        DrawnCursor = (rect, shape, cursorBrush.Color);
        switch (shape)
        {
            case CursorStyle.Bar:
                var bar = Math.Max(1, Math.Round(CursorBarWidth * scale));
                context.FillRectangle(cursorBrush, rect.WithWidth(bar / scale));
                break;
            case CursorStyle.Underline:
                context.FillRectangle(cursorBrush, new Rect(rect.X, rect.Bottom - m.UnderlineThickness / scale, rect.Width, m.UnderlineThickness / scale));
                break;
            case CursorStyle.BlockHollow:
                context.DrawRectangle(HollowCursorPen(cursorBrush, scale), rect.Deflate(0.5 / scale));
                break;
            default:
                context.FillRectangle(cursorBrush, rect);
                DrawUnderBlockCursor(context, row, column, columns, cell, palette, rect, m);
                break;
        }
    }

    /// <summary>The character under a block cursor, at its full width, in <see cref="CursorTextColor"/> or the default background.</summary>
    private void DrawUnderBlockCursor(DrawingContext context, RowSnapshot row, int column, int columns, ulong cell, TerminalPalette palette, Rect rect, CellMetrics m)
    {
        var cp = (int)TCell.Codepoint(cell);
        var style = row.Styles[TCell.StyleIndex(cell)];
        if (cp is 0 or ' ' || style.Flags.HasFlag(StyleFlags.Invisible))
        {
            return;
        }
        var color = CursorTextColor is { } c ? new TerminalRgb(c.R, c.G, c.B) : palette.Background;
        if (SpriteProviderFor(cp) is { } provider)
        {
            if (SpriteOf(provider, cp, columns * m.CellWidth, m) is { } glyph)
            {
                using (context.PushTransform(Matrix.CreateTranslation(0, rect.Y)))
                {
                    DrawSprite(context, new Sprite(glyph, column, rect.X, columns * m.CellWidth, Brush(color, glyph.Opacity), false), m.Scaling);
                }
            }
            return;
        }
        DrawCellText(context, cp, GraphemesOf(row, column, cell), StyleOf(style), columns, color, rect.TopLeft, m);
    }
}
