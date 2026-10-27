using TKCode.Terminal.Core.Interop;

namespace TKCode.Terminal.Core;

/// <summary>
/// 编码按键、鼠标、焦点所需的终端模式（02 §13.1）。视图 actor 每处理完一批 Write 调用 <see cref="TerminalCore.ReadInputModes"/>，
/// 有变化才发布给 UI 线程；不随 FrameSnapshot 发布（mode 2026 期间快照会被扣住，视图不可见时也不生成）。
/// </summary>
public readonly record struct InputModes
{
    /// <summary>与终端复位状态一致的默认值（1035、1036、1007 默认开启）。</summary>
    public static readonly InputModes Defaults = new() { IgnoreKeypadWithNumLock = true, AltEscPrefix = true, AlternateScroll = true };

    public bool CursorKeyApplication { get; init; }
    public bool KeypadApplication { get; init; }
    public bool IgnoreKeypadWithNumLock { get; init; }
    public bool AltEscPrefix { get; init; }
    public bool BackarrowSendsBackspace { get; init; }
    public bool ModifyOtherKeys2 { get; init; }
    public byte KittyFlags { get; init; }
    public MouseTracking MouseTracking { get; init; }
    public MouseFormat MouseFormat { get; init; }
    public bool AlternateScroll { get; init; }
    public bool AlternateScreen { get; init; }
    public bool BracketedPaste { get; init; }
    public bool FocusEvents { get; init; }
    public bool GraphemeCluster { get; init; }

    /// <summary>win32-input-mode（9001，overlay O1）：Windows 平台层改用 <c>CSI Vk;Sc;Uc;Kd;Cs;Rc _</c> 编码按键。</summary>
    public bool Win32Input { get; init; }

    public bool CursorAtPrompt { get; init; }
}

public enum MouseTracking
{
    None,
    X10,
    Normal,
    Button,
    Any,
}

public enum MouseFormat
{
    X10,
    Utf8,
    Sgr,
    Urxvt,
    SgrPixels,
}

[Flags]
public enum KeyMods : ushort
{
    None = 0,
    Shift = 1 << 0,
    Ctrl = 1 << 1,
    Alt = 1 << 2,
    Super = 1 << 3,
    CapsLock = 1 << 4,
    NumLock = 1 << 5,
    /// <summary>右侧 Shift（与 Shift 同时置位）。</summary>
    RightShift = 1 << 6,
    RightCtrl = 1 << 7,
    RightAlt = 1 << 8,
    RightSuper = 1 << 9,
}

public enum KeyAction
{
    Release = 0,
    Press = 1,
    Repeat = 2,
}

/// <summary>
/// 一次按键（06-input.md §3）。<see cref="Text"/> 是该键产生的文本（已应用键盘布局；Ctrl 组合通常为空）；
/// <see cref="ConsumedMods"/> 是产生 Text 时已经"用掉"的修饰键（例如 Shift+a → "A" 用掉了 Shift）；
/// <see cref="UnshiftedCodepoint"/> 是不按 Shift 时该键的码点（Kitty 协议需要）。
/// </summary>
public readonly record struct KeyInput(
    TerminalKey Key,
    KeyAction Action,
    KeyMods Mods,
    string? Text = null,
    KeyMods ConsumedMods = KeyMods.None,
    uint UnshiftedCodepoint = 0,
    bool Composing = false);

public enum MouseAction
{
    Press,
    Release,
    Motion,
}

public enum MouseButton
{
    None = 0,
    Left = 1,
    Right = 2,
    Middle = 3,
    WheelUp = 4,
    WheelDown = 5,
    WheelLeft = 6,
    WheelRight = 7,
}

/// <summary>一次鼠标事件；位置是相对终端视图左上角（含内边距）的物理像素。</summary>
public readonly record struct MouseInput(MouseAction Action, MouseButton Button, KeyMods Mods, float X, float Y);

/// <summary>鼠标编码需要的几何信息（物理像素）。</summary>
public readonly record struct MouseGeometry(int ScreenWidth, int ScreenHeight, int CellWidth, int CellHeight, int PaddingLeft, int PaddingTop,
    int PaddingRight, int PaddingBottom);

public enum OptionAsAlt
{
    None,
    Both,
    Left,
    Right,
}

/// <summary>
/// 按键 / 鼠标 / 焦点编码器（02 §13）。不绑定终端：在 UI 线程上使用，选项来自最近一次发布的 <see cref="InputModes"/>。
/// 非线程安全。
/// </summary>
public sealed unsafe class InputEncoder : IDisposable
{
    private nint _keyEncoder;
    private nint _keyEvent;
    private nint _mouseEncoder;
    private nint _mouseEvent;
    private InputModes? _applied;
    private OptionAsAlt _optionAsAlt = (OptionAsAlt)(-1);

    public InputEncoder()
    {
        GhosttyRuntime.EnsureInitialized();
        nint ke, kv, me, mv;
        TerminalCore.Check(GhosttyNative.KeyEncoderNew(null, &ke));
        TerminalCore.Check(GhosttyNative.KeyEventNew(null, &kv));
        TerminalCore.Check(GhosttyNative.MouseEncoderNew(null, &me));
        TerminalCore.Check(GhosttyNative.MouseEventNew(null, &mv));
        (_keyEncoder, _keyEvent, _mouseEncoder, _mouseEvent) = (ke, kv, me, mv);
        Apply(InputModes.Defaults);
        OptionAsAlt = OptionAsAlt.None;
    }

    /// <summary>macOS：Option 作为 Alt（来自用户设置，不来自终端）。</summary>
    public OptionAsAlt OptionAsAlt
    {
        get => _optionAsAlt;
        set
        {
            if (_optionAsAlt == value)
            {
                return;
            }
            _optionAsAlt = value;
            var v = value switch
            {
                OptionAsAlt.Both => GhosttyOptionAsAlt.True,
                OptionAsAlt.Left => GhosttyOptionAsAlt.Left,
                OptionAsAlt.Right => GhosttyOptionAsAlt.Right,
                _ => GhosttyOptionAsAlt.False,
            };
            GhosttyNative.KeyEncoderSetopt(_keyEncoder, GhosttyKeyEncoderOption.MacosOptionAsAlt, &v);
        }
    }

    public InputModes Modes => _applied ?? default;

    /// <summary>应用新的终端模式（只在变化时调用 setopt）。</summary>
    public void Apply(in InputModes modes)
    {
        if (_applied == modes)
        {
            return;
        }
        _applied = modes;
        SetKey(GhosttyKeyEncoderOption.CursorKeyApplication, modes.CursorKeyApplication);
        SetKey(GhosttyKeyEncoderOption.KeypadKeyApplication, modes.KeypadApplication);
        SetKey(GhosttyKeyEncoderOption.IgnoreKeypadWithNumlock, modes.IgnoreKeypadWithNumLock);
        SetKey(GhosttyKeyEncoderOption.AltEscPrefix, modes.AltEscPrefix);
        SetKey(GhosttyKeyEncoderOption.ModifyOtherKeysState2, modes.ModifyOtherKeys2);
        SetKey(GhosttyKeyEncoderOption.BackarrowKeyMode, modes.BackarrowSendsBackspace);
        var kitty = modes.KittyFlags;
        GhosttyNative.KeyEncoderSetopt(_keyEncoder, GhosttyKeyEncoderOption.KittyFlags, &kitty);
        var tracking = (GhosttyMouseTrackingMode)(int)modes.MouseTracking;
        var format = (GhosttyMouseFormat)(int)modes.MouseFormat;
        GhosttyNative.MouseEncoderSetopt(_mouseEncoder, GhosttyMouseEncoderOption.Event, &tracking);
        GhosttyNative.MouseEncoderSetopt(_mouseEncoder, GhosttyMouseEncoderOption.Format, &format);
    }

    private void SetKey(GhosttyKeyEncoderOption option, bool value) => GhosttyNative.KeyEncoderSetopt(_keyEncoder, option, &value);

    /// <summary>编码一次按键；返回写入的字节数，0 表示该键不产生输入（例如单独的修饰键）。</summary>
    public int EncodeKey(in KeyInput key, Span<byte> destination)
    {
        GhosttyNative.KeyEventSetKey(_keyEvent, (GhosttyKey)(int)key.Key);
        GhosttyNative.KeyEventSetAction(_keyEvent, (GhosttyKeyAction)(int)key.Action);
        GhosttyNative.KeyEventSetMods(_keyEvent, (ushort)key.Mods);
        GhosttyNative.KeyEventSetConsumedMods(_keyEvent, (ushort)key.ConsumedMods);
        GhosttyNative.KeyEventSetUnshiftedCodepoint(_keyEvent, key.UnshiftedCodepoint);
        GhosttyNative.KeyEventSetComposing(_keyEvent, key.Composing);
        Span<byte> utf8 = stackalloc byte[64];
        var n = string.IsNullOrEmpty(key.Text) ? 0 : System.Text.Encoding.UTF8.GetBytes(key.Text, utf8);
        fixed (byte* text = utf8)
        fixed (byte* output = destination)
        {
            GhosttyNative.KeyEventSetUtf8(_keyEvent, n == 0 ? null : text, (nuint)n);
            nuint written;
            var r = GhosttyNative.KeyEncoderEncode(_keyEncoder, _keyEvent, output, (nuint)destination.Length, &written);
            if (r == GhosttyResult.OutOfSpace)
            {
                throw new ArgumentException("Destination buffer too small for the encoded key.", nameof(destination));
            }
            TerminalCore.Check(r);
            return (int)written;
        }
    }

    /// <summary>编码一次鼠标事件（没有开启鼠标上报时返回 0）。</summary>
    public int EncodeMouse(in MouseInput mouse, in MouseGeometry geometry, bool anyButtonPressed, Span<byte> destination)
    {
        var size = GhosttyMouseEncoderSize.Create();
        size.ScreenWidth = (uint)geometry.ScreenWidth;
        size.ScreenHeight = (uint)geometry.ScreenHeight;
        size.CellWidth = (uint)geometry.CellWidth;
        size.CellHeight = (uint)geometry.CellHeight;
        size.PaddingLeft = (uint)geometry.PaddingLeft;
        size.PaddingTop = (uint)geometry.PaddingTop;
        size.PaddingRight = (uint)geometry.PaddingRight;
        size.PaddingBottom = (uint)geometry.PaddingBottom;
        GhosttyNative.MouseEncoderSetopt(_mouseEncoder, GhosttyMouseEncoderOption.Size, &size);
        GhosttyNative.MouseEncoderSetopt(_mouseEncoder, GhosttyMouseEncoderOption.AnyButtonPressed, &anyButtonPressed);
        var track = true;
        GhosttyNative.MouseEncoderSetopt(_mouseEncoder, GhosttyMouseEncoderOption.TrackLastCell, &track);

        GhosttyNative.MouseEventSetAction(_mouseEvent, (GhosttyMouseAction)(int)mouse.Action);
        if (mouse.Button == MouseButton.None)
        {
            GhosttyNative.MouseEventClearButton(_mouseEvent);
        }
        else
        {
            GhosttyNative.MouseEventSetButton(_mouseEvent, (GhosttyMouseButton)(int)mouse.Button);
        }
        GhosttyNative.MouseEventSetMods(_mouseEvent, (ushort)mouse.Mods);
        GhosttyNative.MouseEventSetPosition(_mouseEvent, new GhosttyMousePosition { X = mouse.X, Y = mouse.Y });
        fixed (byte* output = destination)
        {
            nuint written;
            TerminalCore.Check(GhosttyNative.MouseEncoderEncode(_mouseEncoder, _mouseEvent, output, (nuint)destination.Length, &written));
            return (int)written;
        }
    }

    /// <summary>焦点上报（mode 1004 开启时）。返回写入的字节数。</summary>
    public static int EncodeFocus(bool focused, Span<byte> destination)
    {
        fixed (byte* output = destination)
        {
            nuint written;
            TerminalCore.Check(GhosttyNative.FocusEncode(focused ? GhosttyFocusEvent.Gained : GhosttyFocusEvent.Lost, output, (nuint)destination.Length, &written));
            return (int)written;
        }
    }

    public void Dispose()
    {
        if (_keyEncoder == 0)
        {
            return;
        }
        GhosttyNative.KeyEventFree(_keyEvent);
        GhosttyNative.KeyEncoderFree(_keyEncoder);
        GhosttyNative.MouseEventFree(_mouseEvent);
        GhosttyNative.MouseEncoderFree(_mouseEncoder);
        _keyEncoder = 0;
    }
}

public enum PasteOutcome
{
    /// <summary>已编码（经 bracketed paste 包裹，或剥离了危险控制字符）。</summary>
    Written,

    /// <summary>没有可粘贴的文本。</summary>
    Empty,

    /// <summary>内容可能注入命令（换行、ESC[201~ 等），需要用户确认后以 allowUnsafe 再次调用。</summary>
    RequiresConfirmation,
}

/// <summary>不依赖终端实例的粘贴编码（Agent 写入、测试）：按给定的 2004 状态编码，永不放行不安全内容（02 §13.3）。</summary>
public static unsafe class PasteEncoder
{
    public static bool IsSafe(ReadOnlySpan<byte> text)
    {
        fixed (byte* p = text)
        {
            return GhosttyNative.PasteIsSafe(p, (nuint)text.Length);
        }
    }

    public static byte[] Encode(ReadOnlySpan<byte> text, bool bracketed)
    {
        var buffer = new byte[text.Length * 2 + 16];
        fixed (byte* p = text)
        fixed (byte* output = buffer)
        {
            nuint written;
            var r = GhosttyNative.PasteEncode(p, (nuint)text.Length, bracketed, output, (nuint)buffer.Length, &written);
            TerminalCore.Check(r);
            return buffer.AsSpan(0, (int)written).ToArray();
        }
    }
}
