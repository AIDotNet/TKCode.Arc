using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.TextFormatting;
using Avalonia.Input;
using Avalonia.Media;

namespace StreamMark.Avalonia.Rendering;

/// <summary>
/// 自绘的小图标按钮（代码块头部的复制 / 换行 / 切换等）。不依赖宿主主题中的 Button 模板。
/// </summary>
public sealed class IconButton : Control
{
    private bool _hover;
    private bool _pressed;
    private Geometry? _icon;
    private TextLayout? _label;
    private string? _labelText;

    public IconButton()
    {
        Cursor = new Cursor(StandardCursorType.Hand);
        Focusable = false;
    }

    public event EventHandler? Click;

    /// <summary>以代码触发点击（无障碍 Invoke、键盘激活）。</summary>
    public void PerformClick() => Click?.Invoke(this, EventArgs.Empty);

    protected override global::Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new Automation.IconButtonAutomationPeer(this);

    public IBrush? Foreground { get; set; }

    public IBrush? HoverBackground { get; set; }

    /// <summary>16×16 坐标系下的图标路径。</summary>
    public Geometry? Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            InvalidateVisual();
        }
    }

    /// <summary>可选的文字标签（显示在图标右侧，例如"已复制"）。</summary>
    public string? Label
    {
        get => _labelText;
        set
        {
            _labelText = value;
            _label = string.IsNullOrEmpty(value) ? null : new TextLayout(value, Typeface.Default, 12, Foreground);
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    public bool IsActive { get; set; }

    protected override Size MeasureOverride(Size availableSize) => new(26 + (_label is null ? 0 : _label.WidthIncludingTrailingWhitespace + 4), 26);

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if ((_hover || IsActive) && HoverBackground is not null)
        {
            context.DrawRectangle(HoverBackground, null, bounds, 5, 5);
        }
        else
        {
            // 透明背景保证整个区域可命中
            context.DrawRectangle(Brushes.Transparent, null, bounds);
        }
        var foreground = Foreground ?? Brushes.Gray;
        if (_icon is not null)
        {
            using (context.PushTransform(Matrix.CreateTranslation(5, 5)))
            {
                context.DrawGeometry(null, new Pen(foreground, 1.4, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), _icon);
            }
        }
        _label?.Draw(context, new Point(26, (bounds.Height - _label.Height) / 2));
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _hover = true;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = false;
        _pressed = false;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pressed && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            Click?.Invoke(this, EventArgs.Empty);
        }
        _pressed = false;
        e.Handled = true;
    }
}

/// <summary>16×16 线条图标。</summary>
public static class MarkdownIcons
{
    public static Geometry Copy { get; } = Geometry.Parse("M5,5 L5,2.5 A1,1 0 0 1 6,1.5 L13,1.5 A1,1 0 0 1 14,2.5 L14,9.5 A1,1 0 0 1 13,10.5 L11,10.5 M2.5,5.5 L10,5.5 A1,1 0 0 1 11,6.5 L11,13.5 A1,1 0 0 1 10,14.5 L2.5,14.5 A1,1 0 0 1 1.5,13.5 L1.5,6.5 A1,1 0 0 1 2.5,5.5 Z");

    public static Geometry Check { get; } = Geometry.Parse("M2.5,8.5 L6,12 L13.5,4");

    public static Geometry Wrap { get; } = Geometry.Parse("M1.5,3.5 L14.5,3.5 M1.5,8 L12,8 A2.25,2.25 0 0 1 12,12.5 L8,12.5 M9.5,10.5 L7.5,12.5 L9.5,14.5 M1.5,12.5 L5,12.5");

    public static Geometry Code { get; } = Geometry.Parse("M5.5,4 L1.5,8 L5.5,12 M10.5,4 L14.5,8 L10.5,12");

    public static Geometry Diagram { get; } = Geometry.Parse("M1.5,2.5 L6,2.5 L6,6 L1.5,6 Z M10,10 L14.5,10 L14.5,13.5 L10,13.5 Z M3.75,6 L3.75,11.75 L10,11.75");

    public static Geometry Chevron { get; } = Geometry.Parse("M4,6 L8,10 L12,6");

    public static Geometry Expand { get; } = Geometry.Parse("M2.5,6 L2.5,2.5 L6,2.5 M10,2.5 L13.5,2.5 L13.5,6 M13.5,10 L13.5,13.5 L10,13.5 M6,13.5 L2.5,13.5 L2.5,10");

    public static Geometry ChevronUp { get; } = Geometry.Parse("M4,10 L8,6 L12,10");

    public static Geometry Download { get; } = Geometry.Parse("M8,2 L8,10.5 M4.5,7 L8,10.5 L11.5,7 M2.5,13.5 L13.5,13.5");
}
