using Avalonia;
using Avalonia.Input;
using TKCode.Terminal.Core;
using MouseButton = TKCode.Terminal.Core.MouseButton;

namespace TKCode.Terminal.Avalonia;

/// <summary>按键事件：<see cref="Input"/> 由持有会话的一方编码（需要 UI 侧核心的输入模式）。设 <see cref="Handled"/> 表示已消费（例如复制、粘贴快捷键）。</summary>
public sealed class TerminalKeyEventArgs(KeyInput input, KeyModifiers modifiers) : EventArgs
{
    public KeyInput Input { get; } = input;
    public KeyModifiers Modifiers { get; } = modifiers;
    public bool Handled { get; set; }
}

public enum TerminalPointerKind
{
    Press,
    Release,
    Move,
    Wheel,
}

/// <summary>
/// 指针事件。<see cref="Cell"/> 是点击所在的格子（视口坐标）；<see cref="Mouse"/> 是编码鼠标报告所需的物理像素（相对视图左上角，含内边距）。
/// </summary>
public sealed class TerminalPointerEventArgs(TerminalPointerKind kind, ViewportPoint cell, MouseInput mouse, MouseGeometry geometry, double wheelDelta) : EventArgs
{
    public TerminalPointerKind Kind { get; } = kind;
    public ViewportPoint Cell { get; } = cell;
    public MouseInput Mouse { get; } = mouse;
    public MouseGeometry Geometry { get; } = geometry;

    /// <summary>滚轮的行数（正数向上滚，与 xterm 一致）。</summary>
    public double WheelDelta { get; } = wheelDelta;

    public bool Handled { get; set; }
}

public sealed partial class TerminalView
{
    public event EventHandler<TerminalKeyEventArgs>? KeyPressed;

    /// <summary>可以直接作为字符输入的文本（已应用键盘布局）。</summary>
    public event EventHandler<string>? TextEntered;

    public event EventHandler<TerminalPointerEventArgs>? PointerChanged;

    public TerminalView()
    {
        Focusable = true;
        LostFocus += (_, _) => SetPreedit(null);  // 失去焦点时组合中止
        InitializeInputMethod();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        Dispatch(e, GlobalKey(e.Key, e.KeyModifiers, text: null), e.Handled);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        // 释放事件只在程序开启了 Kitty 键盘协议的 release 标志时才会被编码，这里总是交给编码器决定
        Dispatch(e, GlobalKey(e.Key, e.KeyModifiers, text: null, KeyAction.Release), e.Handled);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        // 提交的文字结束组合：先清除组合文字
        SetPreedit(null);
        if (!string.IsNullOrEmpty(e.Text))
        {
            TextEntered?.Invoke(this, e.Text);
            e.Handled = true;
        }
    }



    private void Dispatch(KeyEventArgs e, KeyInput? input, bool alreadyHandled)
    {
        if (alreadyHandled || input is not { } key || !TerminalKeys.TryMap(e.Key, out _))
        {
            return;
        }
        var args = new TerminalKeyEventArgs(key, e.KeyModifiers);
        KeyPressed?.Invoke(this, args);
        e.Handled = args.Handled;
    }

    /// <summary>
    /// 生成 <see cref="KeyInput"/>。可打印键（无 Ctrl / Alt / Meta）返回 null，由 TextInput 负责字符。
    /// 不可映射的键也返回 null。
    /// </summary>
    private static KeyInput? GlobalKey(Key key, KeyModifiers modifiers, string? text, KeyAction action = KeyAction.Press)
    {
        if (!TerminalKeys.TryMap(key, out var terminal))
        {
            return null;
        }
        if (action == KeyAction.Press && TerminalKeys.ProducesText(key, modifiers))
        {
            return null;
        }
        return new KeyInput(terminal, action, TerminalKeys.Mods(modifiers), text);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        RaisePointer(e, TerminalPointerKind.Press, Button(e.GetCurrentPoint(this).Properties));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        RaisePointer(e, TerminalPointerKind.Release, Button(e.GetCurrentPoint(this).Properties));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        RaisePointer(e, TerminalPointerKind.Move, MouseButton.None);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var args = Pointer(e, TerminalPointerKind.Wheel, MouseButton.None, e.Delta.Y);
        PointerChanged?.Invoke(this, args);
        e.Handled = args.Handled;
    }

    private void RaisePointer(PointerEventArgs e, TerminalPointerKind kind, MouseButton button)
    {
        var args = Pointer(e, kind, button, 0);
        PointerChanged?.Invoke(this, args);
        e.Handled = args.Handled;
    }

    private TerminalPointerEventArgs Pointer(PointerEventArgs e, TerminalPointerKind kind, MouseButton button, double wheel)
    {
        var m = Metrics;
        var scale = m.Scaling;
        var pos = e.GetPosition(this);
        var pad = Math.Round(Padding * scale);
        var px = pos.X * scale;
        var py = pos.Y * scale;
        var col = (int)Math.Floor((px - pad) / m.CellWidth);
        var row = (int)Math.Floor((py - pad) / m.CellHeight);
        var size = Bounds.Size;
        var geometry = new MouseGeometry((int)(size.Width * scale), (int)(size.Height * scale), m.CellWidth, m.CellHeight,
            (int)pad, (int)pad, (int)pad, (int)pad);
        var mouse = new MouseInput(
            kind switch { TerminalPointerKind.Press => MouseAction.Press, TerminalPointerKind.Release => MouseAction.Release, _ => MouseAction.Motion },
            kind == TerminalPointerKind.Wheel ? (wheel > 0 ? MouseButton.WheelUp : MouseButton.WheelDown) : button,
            TerminalKeys.Mods(e.KeyModifiers), (float)px, (float)py);
        return new TerminalPointerEventArgs(kind, new ViewportPoint(Math.Max(0, col), Math.Max(0, row)), mouse, geometry, wheel);
    }

    private static MouseButton Button(PointerPointProperties p) => p.PointerUpdateKind switch
    {
        PointerUpdateKind.LeftButtonPressed or PointerUpdateKind.LeftButtonReleased => MouseButton.Left,
        PointerUpdateKind.RightButtonPressed or PointerUpdateKind.RightButtonReleased => MouseButton.Right,
        PointerUpdateKind.MiddleButtonPressed or PointerUpdateKind.MiddleButtonReleased => MouseButton.Middle,
        _ => MouseButton.None,
    };
}
