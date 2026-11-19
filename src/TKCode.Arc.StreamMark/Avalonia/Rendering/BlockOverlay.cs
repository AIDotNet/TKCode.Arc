using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using StreamMark.Avalonia.Selection;
using StreamMark.Core.Model;

namespace StreamMark.Avalonia.Rendering;

/// <summary>
/// 全屏查看一个块（表格等）：显示在窗口覆盖层上，按内容自然宽度排版（不超过窗口），滚轮纵向滚动、Shift+滚轮横向滚动，
/// Esc 或点击背景关闭。副本使用独立的选区，不影响文档中的选区。不依赖宿主主题模板。
/// </summary>
public sealed class BlockOverlay : Control
{
    private const double Gap = 40;
    private const double Pad = 16;

    private readonly BlockView _view;
    private readonly MarkdownStyle _style;
    private OverlayLayer? _layer;
    private TopLevel? _topLevel;
    private Vector _scroll;
    private Rect _panel;

    private BlockOverlay(BlockView view, MarkdownStyle style)
    {
        _view = view;
        _style = style;
        Focusable = true;
        ClipToBounds = true;
        LogicalChildren.Add(view);
        VisualChildren.Add(view);
    }

    public BlockView View => _view;

    public static BlockOverlay? Show(Visual anchor, RenderContext context, MarkdownBlock block, Func<BlockView> create)
    {
        if (OverlayLayer.GetOverlayLayer(anchor) is not { } layer)
        {
            return null;
        }
        var view = create();
        // 独立的选区：覆盖层中的副本与文档中的片段编号相同
        var own = new RenderContext(context.Style, context.Services, new SelectionModel(), context.Entry, null, context.State, context.ImageBaseUri);
        var segment = 0;
        view.Update(own, block, block.Key, ref segment);
        var overlay = new BlockOverlay(view, context.Style);
        overlay._layer = layer;
        overlay._topLevel = TopLevel.GetTopLevel(anchor);
        layer.PropertyChanged += overlay.OnHostSizeChanged;
        if (overlay._topLevel is not null)
        {
            overlay._topLevel.PropertyChanged += overlay.OnHostSizeChanged;
        }
        layer.Children.Add(overlay);
        overlay.UpdateSize();
        overlay.Focus();
        return overlay;
    }

    private void OnHostSizeChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty || e.Property == TopLevel.ClientSizeProperty)
        {
            UpdateSize();
        }
    }

    private void UpdateSize()
    {
        var size = _layer?.Bounds.Size is { Width: > 0, Height: > 0 } bounds ? bounds : _topLevel?.ClientSize ?? default;
        if (size.Width > 0 && size.Height > 0)
        {
            Width = size.Width;
            Height = size.Height;
            InvalidateMeasure();
        }
    }

    public void Close()
    {
        if (_layer is not null)
        {
            _layer.PropertyChanged -= OnHostSizeChanged;
            _layer.Children.Remove(this);
        }
        if (_topLevel is not null)
        {
            _topLevel.PropertyChanged -= OnHostSizeChanged;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = new Size(double.IsFinite(Width) ? Width : availableSize.Width, double.IsFinite(Height) ? Height : availableSize.Height);
        var maxWidth = Math.Max(100, size.Width - Gap * 2 - Pad * 2);
        // 先按无限宽度取自然宽度，再限制在窗口内
        _view.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = Math.Min(maxWidth, _view.DesiredSize.Width);
        _view.Measure(new Size(width, double.PositiveInfinity));
        var panelWidth = width + Pad * 2;
        var panelHeight = Math.Min(size.Height - Gap * 2, _view.DesiredSize.Height + Pad * 2);
        _panel = new Rect((size.Width - panelWidth) / 2, (size.Height - panelHeight) / 2, panelWidth, Math.Max(40, panelHeight));
        ClampScroll();
        return size;
    }

    private void ClampScroll()
    {
        var maxY = Math.Max(0, _view.DesiredSize.Height - (_panel.Height - Pad * 2));
        _scroll = new Vector(0, Math.Clamp(_scroll.Y, 0, maxY));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _view.Arrange(new Rect(_panel.X + Pad, _panel.Y + Pad - _scroll.Y, _panel.Width - Pad * 2, _view.DesiredSize.Height));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(0xB3, 0, 0, 0)), new Rect(Bounds.Size));
        context.DrawRectangle(_style.CodeBackground, new Pen(_style.CodeBorderBrush, 1), _panel, 10, 10);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.Handled)
        {
            return;
        }
        _scroll = new Vector(0, _scroll.Y - e.Delta.Y * 48);
        ClampScroll();
        InvalidateArrange();
        UpdateClip();
        e.Handled = true;
    }

    // 子视图超出面板的部分不绘制
    private void UpdateClip() => _view.Clip = new RectangleGeometry(new Rect(-Pad, _scroll.Y - Pad + 1, _panel.Width, _panel.Height - 2));

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_panel.Contains(e.GetPosition(this)))
        {
            Close();
            e.Handled = true;
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateClip();
    }
}
