namespace TKCode.Terminal.Core;

/// <summary>
/// 终端尺寸。像素是**单个 cell** 的尺寸（0 表示未知）；整块区域像素（TIOCSWINSZ 的 ws_xpixel / ws_ypixel）
/// = 列数 × CellWidth、行数 × CellHeight，由需要的一方计算（01-architecture.md §6.1）。
/// </summary>
public readonly record struct TerminalSize(int Columns, int Rows, int CellWidth, int CellHeight)
{
    public int PixelWidth => Columns * CellWidth;
    public int PixelHeight => Rows * CellHeight;
}
