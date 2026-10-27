using Avalonia;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Media;
using TKCode.Terminal.Core;

namespace TKCode.Terminal.Avalonia;

/// <summary>
/// 输入法（IME）。平台通过 <see cref="TextInputMethodClient"/> 得到光标矩形（候选窗口位置）并推送组合中的文字（preedit）；
/// 组合结束后提交的文字经 TextInput 事件送来，与普通输入走同一条路径（06 §5）。
/// 组合文字画在光标处、带下划线，不写入终端，直到提交。
/// </summary>
public sealed partial class TerminalView
{
    private TerminalInputMethodClient? _ime;
    private string? _preedit;

    /// <summary>组合中的文字（没有组合时为 null）。</summary>
    public string? Preedit => _preedit;

    /// <summary>光标所在格子的矩形（DIP，相对视图）。没有可见光标时为空矩形。</summary>
    public Rect CursorRectangle
    {
        get
        {
            var frame = Snapshot;
            if (frame is null || Metrics is not { } m)
            {
                return default;
            }
            var c = frame.Cursor;
            var pad = Math.Round(Padding * m.Scaling);
            var scale = m.Scaling;
            return new Rect((pad + c.Column * m.CellWidth) / scale, (pad + c.Row * m.CellHeight) / scale, m.CellWidth / scale, m.CellHeight / scale);
        }
    }

    internal void InitializeInputMethod()
    {
        _ime = new TerminalInputMethodClient(this);
        TextInputMethodClientRequested += (_, e) => e.Client = _ime;
        InputMethod.SetIsInputMethodEnabled(this, true);
    }

    internal void SetPreedit(string? text)
    {
        var value = string.IsNullOrEmpty(text) ? null : text;
        if (value == _preedit)
        {
            return;
        }
        _preedit = value;
        InvalidateVisual();
    }

    /// <summary>光标移动（新帧）后通知平台更新候选窗口位置。</summary>
    internal void NotifyCursorMoved() => _ime?.RaiseCursor();

    /// <summary>在光标处绘制组合文字：覆盖底色，逐字符按显示宽度排列，下划线标出组合区域。</summary>
    private void DrawPreedit(DrawingContext context, TerminalRgb foreground, TerminalRgb background)
    {
        if (_preedit is not { Length: > 0 } text || Snapshot is not { } frame || Metrics is not { } m)
        {
            return;
        }
        var scale = m.Scaling;
        var pad = Math.Round(Padding * scale);
        var col = frame.Cursor.Column;
        var row = frame.Cursor.Row;
        var x = pad + col * m.CellWidth;
        var y = pad + row * m.CellHeight;
        var cells = 0;
        var runes = new List<(string Text, int Width)>();
        foreach (var rune in text.EnumerateRunes())
        {
            var width = IsWide(rune.Value) ? 2 : 1;
            runes.Add((rune.ToString(), width));
            cells += width;
        }
        context.FillRectangle(Brush(background), new Rect(x / scale, y / scale, cells * m.CellWidth / scale, m.CellHeight / scale));
        var at = x;
        foreach (var (s, width) in runes)
        {
            DrawText(context, s, width == 2 ? CellWidth.Wide : CellWidth.Narrow, foreground, new Rect(at / scale, y / scale, width * m.CellWidth / scale, m.CellHeight / scale), m);
            at += width * m.CellWidth;
        }
        var underline = new Rect(x / scale, (y + m.CellHeight - m.UnderlineThickness) / scale, cells * m.CellWidth / scale, Math.Max(1, m.UnderlineThickness) / scale);
        context.FillRectangle(Brush(foreground), underline);
    }

    /// <summary>东亚宽字符（CJK、全角形式、假名、韩文音节）占两列。与核心的宽度判断保持大致一致，组合文字只用于显示。</summary>
    private static bool IsWide(int cp) =>
        cp is >= 0x1100 and <= 0x115F or >= 0x2E80 and <= 0xA4CF or >= 0xAC00 and <= 0xD7A3 or >= 0xF900 and <= 0xFAFF or >= 0xFE30 and <= 0xFE4F
            or >= 0xFF00 and <= 0xFF60 or >= 0xFFE0 and <= 0xFFE6 or >= 0x1F300 and <= 0x1F64F or >= 0x1F900 and <= 0x1F9FF or >= 0x20000 and <= 0x3FFFD;
}

/// <summary>平台输入法的客户端：光标矩形、preedit 支持，以及平台推送的组合文字。</summary>
internal sealed class TerminalInputMethodClient(TerminalView view) : TextInputMethodClient
{
    private TextSelection _selection;

    public override Visual TextViewVisual => view;

    public override bool SupportsPreedit => true;

    public override bool SupportsSurroundingText => false;

    public override string SurroundingText => string.Empty;

    public override Rect CursorRectangle => view.CursorRectangle;

    public override TextSelection Selection
    {
        get => _selection;
        set => _selection = value;
    }

    public override void SetPreeditText(string? text) => view.SetPreedit(text);

    public override void SetPreeditText(string? text, int? caretPosition) => view.SetPreedit(text);

    public void RaiseCursor() => RaiseCursorRectangleChanged();
}
