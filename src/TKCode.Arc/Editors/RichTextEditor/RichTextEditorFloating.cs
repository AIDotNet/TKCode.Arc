using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Rendering;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The editor's floating layer, hosted in the window's adorner layer so it draws above neighbouring content while
/// following the editor (and clipping like Arc's absolutely positioned layers inside scrolling ancestors). Pointer
/// presses inside it never take focus from the writing surface, so the selection stays (Arc's mousedown preventDefault).
/// </summary>
internal sealed class RichTextEditorOverlay : Panel
{
    public RichTextEditorOverlay()
    {
        AddHandler(PointerPressedEvent, OnTunnelPointer, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnTunnelPointer, RoutingStrategies.Tunnel);
    }

    private static void OnTunnelPointer(object? sender, PointerEventArgs e)
    {
        for (var visual = e.Source as Visual; visual is not null && visual is not RichTextEditorOverlay; visual = visual.GetVisualParentSafe())
        {
            if (visual is TextBox)
            {
                return;
            }
        }
        // Handled here, so the focus manager (which acts at the source) leaves focus in the editor; the buttons
        // below still see the press through handledEventsToo.
        e.Handled = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(Size.Infinity);
        }
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            child.Arrange(new Rect(child.DesiredSize));
        }
        return finalSize;
    }
}

internal static class RichTextEditorVisualExtensions
{
    public static Visual? GetVisualParentSafe(this Visual visual) => Avalonia.VisualTree.VisualExtensions.GetVisualParent(visual);
}

/// <summary>
/// A floating panel: the shadowed, bordered surface is drawn at an animated size while its content is clipped to it,
/// so the panel can spring between sizes without relaying out anything outside it.
/// </summary>
internal abstract class RichTextEditorFloatingSurface : Control, ICustomHitTest
{
    public static readonly StyledProperty<IBrush?> SurfaceBackgroundProperty =
        AvaloniaProperty.Register<RichTextEditorFloatingSurface, IBrush?>(nameof(SurfaceBackground));

    public static readonly StyledProperty<IBrush?> SurfaceBorderBrushProperty =
        AvaloniaProperty.Register<RichTextEditorFloatingSurface, IBrush?>(nameof(SurfaceBorderBrush));

    public static readonly StyledProperty<BoxShadows> SurfaceShadowProperty =
        AvaloniaProperty.Register<RichTextEditorFloatingSurface, BoxShadows>(nameof(SurfaceShadow));

    public static readonly StyledProperty<double> SurfaceRadiusProperty =
        AvaloniaProperty.Register<RichTextEditorFloatingSurface, double>(nameof(SurfaceRadius), 16);

    public IBrush? SurfaceBackground { get => GetValue(SurfaceBackgroundProperty); set => SetValue(SurfaceBackgroundProperty, value); }
    public IBrush? SurfaceBorderBrush { get => GetValue(SurfaceBorderBrushProperty); set => SetValue(SurfaceBorderBrushProperty, value); }
    public BoxShadows SurfaceShadow { get => GetValue(SurfaceShadowProperty); set => SetValue(SurfaceShadowProperty, value); }
    public double SurfaceRadius { get => GetValue(SurfaceRadiusProperty); set => SetValue(SurfaceRadiusProperty, value); }

    private readonly MatrixTransform _transform = new();
    private IPen? _pen;

    static RichTextEditorFloatingSurface()
    {
        AffectsRender<RichTextEditorFloatingSurface>(SurfaceBackgroundProperty, SurfaceBorderBrushProperty, SurfaceShadowProperty, SurfaceRadiusProperty);
    }

    protected RichTextEditorFloatingSurface()
    {
        Host = new Canvas { ClipToBounds = true };
        VisualChildren.Add(Host);
        LogicalChildren.Add(Host);
        RenderTransform = _transform;
        RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
        IsVisible = false;
    }

    /// <summary>The clipped content host (children are placed with Canvas.Left/Top inside the border).</summary>
    protected Canvas Host { get; }

    /// <summary>The surface's current (animated) outer size.</summary>
    protected Size SurfaceSize { get; private set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SurfaceBorderBrushProperty)
        {
            _pen = null;
        }
    }

    protected void SetSurfaceSize(Size size)
    {
        if (size == SurfaceSize)
        {
            return;
        }
        SurfaceSize = size;
        // Only the clip host follows the spring; nothing outside this surface relayouts.
        InvalidateArrange();
        InvalidateVisual();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Host.Arrange(new Rect(1, 1, Math.Max(0, SurfaceSize.Width - 2), Math.Max(0, SurfaceSize.Height - 2)));
        return finalSize;
    }

    /// <summary>Places the surface: <paramref name="left"/>/<paramref name="top"/> in the overlay, scaled around an origin inside it.</summary>
    protected void SetPlacement(double left, double top, double scale, double originX, double originY)
    {
        _transform.Matrix = Matrix.CreateTranslation(-originX, -originY) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(originX + left, originY + top);
    }

    public override void Render(DrawingContext context)
    {
        if (SurfaceSize.Width <= 0 || SurfaceSize.Height <= 0)
        {
            return;
        }
        _pen ??= SurfaceBorderBrush is { } border ? new Avalonia.Media.Immutable.ImmutablePen(border.ToImmutable(), 1) : null;
        var rect = new Rect(SurfaceSize).Deflate(0.5);
        context.DrawRectangle(SurfaceBackground, _pen, new RoundedRect(rect, SurfaceRadius), SurfaceShadow);
    }

    public bool HitTest(Point point) => IsVisible && new Rect(SurfaceSize).Contains(point);
}

/// <summary>
/// A toolbar or menu button that never takes focus from the editor on a pointer press (it is still reachable with
/// Alt+F10 and the arrow keys). <see cref="IsChecked"/> is Arc's <c>aria-pressed</c>.
/// </summary>
[PseudoClasses(CheckedPseudoClass, DownPseudoClass)]
internal sealed class RichTextEditorToolButton : AvaloniaButton
{
    public const string CheckedPseudoClass = ":checked";
    public const string DownPseudoClass = ":down";

    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<RichTextEditorToolButton, bool>(nameof(IsChecked));

    public static readonly StyledProperty<bool> IsToggleProperty =
        AvaloniaProperty.Register<RichTextEditorToolButton, bool>(nameof(IsToggle));

    public bool IsChecked { get => GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    /// <summary>True for formatting buttons that report a pressed state.</summary>
    public bool IsToggle { get => GetValue(IsToggleProperty); set => SetValue(IsToggleProperty, value); }

    protected override Type StyleKeyOverride => typeof(RichTextEditorToolButton);

    public RichTextEditorToolButton()
    {
        AddHandler(PointerPressedEvent, OnPress, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnRelease, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, (_, _) => PseudoClasses.Set(DownPseudoClass, false), RoutingStrategies.Direct);
    }

    public RichTextEditorToolButton(Geometry icon, string label, bool toggle = false) : this()
    {
        Content = new ArcGlyph { Data = icon, Size = 16, StrokeThickness = 1.75 };
        AutomationProperties.SetName(this, label);
        IsToggle = toggle;
    }

    private void OnPress(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        PseudoClasses.Set(DownPseudoClass, true);
        e.Pointer.Capture(this);
    }

    private void OnRelease(object? sender, PointerReleasedEventArgs e)
    {
        if (!PseudoClasses.Contains(DownPseudoClass))
        {
            return;
        }
        PseudoClasses.Set(DownPseudoClass, false);
        if (ReferenceEquals(e.Pointer.Captured, this))
        {
            e.Pointer.Capture(null);
        }
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            Invoke();
        }
    }

    /// <summary>Runs the button's click (and command) as a click would.</summary>
    internal void Invoke() => OnClick();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty)
        {
            PseudoClasses.Set(CheckedPseudoClass, IsChecked);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => IsToggle ? new RichTextEditorToolButtonAutomationPeer(this) : new ButtonAutomationPeer(this);
}

/// <summary>Formatting buttons are toggle buttons for assistive technology (Arc's <c>aria-pressed</c>).</summary>
internal sealed class RichTextEditorToolButtonAutomationPeer : ButtonAutomationPeer, IToggleProvider
{
    public RichTextEditorToolButtonAutomationPeer(RichTextEditorToolButton owner) : base(owner)
    {
    }

    private RichTextEditorToolButton Button => (RichTextEditorToolButton)Owner;

    public ToggleState ToggleState => Button.IsChecked ? ToggleState.On : ToggleState.Off;

    public void Toggle() => Button.Invoke();
}

/// <summary>
/// Arc's floating selection toolbar: one surface whose position glides with the selection and whose size springs
/// between its format face and its link face; the faces slide and unblur past each other.
/// </summary>
internal sealed class RichTextEditorToolbar : RichTextEditorFloatingSurface
{
    private const double Travel = 18;
    private static readonly SpringSpec s_morph = SpringSpec.FromVisual(0.42, 0.12);
    private static readonly SpringSpec s_glide = SpringSpec.FromVisual(0.3, 0.08);

    private readonly MotionValue<Size> _size;
    private readonly MotionValue<Point> _position;
    private readonly MotionValue<double> _scale;
    private readonly MotionValue<double> _offsetY;
    private readonly MotionValue<double> _opacity;
    private readonly Face _format;
    private readonly Face _link;
    private bool _shown;
    private bool _below;
    private bool _linkFace;
    private double _rootWidth;

    public RichTextEditorToolbar()
    {
        Bold = new RichTextEditorToolButton(ArcIcons.Bold, "Bold", true);
        Italic = new RichTextEditorToolButton(ArcIcons.Italic, "Italic", true);
        Strike = new RichTextEditorToolButton(ArcIcons.Strikethrough, "Strikethrough", true);
        Code = new RichTextEditorToolButton(ArcIcons.Code, "Inline code", true);
        Link = new RichTextEditorToolButton(ArcIcons.Link, "Link", true);
        Heading1 = new RichTextEditorToolButton(ArcIcons.Heading1, "Heading 1", true);
        Heading2 = new RichTextEditorToolButton(ArcIcons.Heading2, "Heading 2", true);
        Quote = new RichTextEditorToolButton(ArcIcons.Quote, "Quote", true);
        Back = new RichTextEditorToolButton(ArcIcons.ArrowLeft, "Back to formatting");
        Unlink = new RichTextEditorToolButton(ArcIcons.Unlink, "Remove link") { IsVisible = false };
        Apply = new RichTextEditorToolButton(ArcIcons.Check, "Apply link") { Classes = { "apply" } };
        ((ArcGlyph)Apply.Content!).StrokeThickness = 2;
        SetTip(Bold, "Bold (⌘B)");
        SetTip(Italic, "Italic (⌘I)");
        SetTip(Strike, "Strikethrough (⌘⇧X)");
        SetTip(Code, "Inline code (⌘E)");
        SetTip(Link, "Link (⌘K)");
        SetTip(Heading1, "Heading 1");
        SetTip(Heading2, "Heading 2");
        SetTip(Quote, "Quote");

        Divider = new Border { Width = 1, Height = 18, Margin = new Thickness(4, 0) };
        ArcTokens.Bind(Divider, Border.BackgroundProperty, DesignTokens.ArcDesignTokenKind.Border);
        BlockTools = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 2, Children = { Divider, Heading1, Heading2, Quote } };
        var formatRow = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 2, Margin = new Thickness(4),
            Children = { Bold, Italic, Strike, Code, Link, BlockTools }
        };
        LinkBox = SelectInputBox.Create();
        LinkBox.Width = 240;
        LinkBox.Height = 32;
        LinkBox.Padding = new Thickness(8, 0);
        LinkBox.PlaceholderText = "Paste or type a link";
        LinkBox.Classes.Add("rich-text-link-input");
        AutomationProperties.SetName(LinkBox, "Link address");
        var linkRow = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 2, Margin = new Thickness(4),
            Children = { Back, LinkBox, Unlink, Apply }
        };
        _format = new Face(this, formatRow);
        _link = new Face(this, linkRow);
        Host.Children.Add(formatRow);
        Host.Children.Add(linkRow);
        _link.Hide();

        _size = new MotionValue<Size>(this, default, MotionVectors.Size);
        _position = new MotionValue<Point>(this, default, MotionVectors.Point);
        _scale = new MotionValue<double>(this, 1, MotionVectors.Double);
        _offsetY = new MotionValue<double>(this, 0, MotionVectors.Double);
        _opacity = new MotionValue<double>(this, 0, MotionVectors.Double);
        _size.Changed += value =>
        {
            SetSurfaceSize(value);
            UpdatePlacement();
        };
        _position.Changed += _ => UpdatePlacement();
        _scale.Changed += _ => UpdatePlacement();
        _offsetY.Changed += _ => UpdatePlacement();
        _opacity.Changed += value => Opacity = value;
        Opacity = 0;
        AutomationProperties.SetName(this, "Formatting");
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Content);
    }

    private static void SetTip(Control control, string tip) => Tooltip.SetTip(control, tip);

    public RichTextEditorToolButton Bold { get; }
    public RichTextEditorToolButton Italic { get; }
    public RichTextEditorToolButton Strike { get; }
    public RichTextEditorToolButton Code { get; }
    public RichTextEditorToolButton Link { get; }
    public RichTextEditorToolButton Heading1 { get; }
    public RichTextEditorToolButton Heading2 { get; }
    public RichTextEditorToolButton Quote { get; }
    public RichTextEditorToolButton Back { get; }
    public RichTextEditorToolButton Unlink { get; }
    public RichTextEditorToolButton Apply { get; }
    public TextBox LinkBox { get; }
    public Border Divider { get; }
    public StackPanel BlockTools { get; }

    public bool IsShown => _shown;

    public bool IsLinkFace => _linkFace;

    /// <summary>The format face's buttons in keyboard order (Alt+F10, ArrowLeft and ArrowRight).</summary>
    public IEnumerable<RichTextEditorToolButton> FormatButtons
    {
        get
        {
            yield return Bold;
            yield return Italic;
            yield return Strike;
            yield return Code;
            yield return Link;
            if (BlockTools.IsVisible)
            {
                yield return Heading1;
                yield return Heading2;
                yield return Quote;
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new EditorsGroupAutomationPeer(this, AutomationControlType.ToolBar);

    private Size FaceSize(Face face)
    {
        face.Content.Measure(Size.Infinity);
        var size = face.Content.DesiredSize;
        return new Size(size.Width + 2, size.Height + 2);
    }

    private Face Current => _linkFace ? _link : _format;

    /// <summary>Opens at, or glides to, <paramref name="x"/> (selection centre) and <paramref name="y"/> (top, or bottom when <paramref name="below"/>).</summary>
    public void Present(double x, double y, bool below, double rootWidth)
    {
        _rootWidth = rootWidth;
        var full = ArcMotion.IsFull(this);
        var target = FaceSize(Current);
        var half = (target.Width > 0 ? target.Width : 280) / 2;
        var clampedX = rootWidth > 0 ? Math.Max(half + 4, Math.Min(x, rootWidth - half - 4)) : x;
        _below = below;
        var point = new Point(clampedX, y);
        if (!_shown || !full)
        {
            var fresh = !_shown;
            _shown = true;
            IsVisible = true;
            IsHitTestVisible = true;
            _position.Jump(point);
            _size.Jump(target);
            if (fresh)
            {
                Enter(full);
            }
            return;
        }
        _ = _position.AnimateTo(point, MotionSpec.Spring(s_glide));
        if (_size.Target != target)
        {
            _ = _size.AnimateTo(target, MotionSpec.Spring(s_morph));
        }
    }

    private void Enter(bool full)
    {
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            _scale.Jump(1);
            _offsetY.Jump(0);
            _opacity.Jump(1);
            return;
        }
        _opacity.Jump(0);
        _ = _opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
        if (full)
        {
            _scale.Jump(0.94);
            _offsetY.Jump(_below ? -4 : 4);
            _ = _scale.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
            _ = _offsetY.AnimateTo(0, MotionSpec.Spring(ArcSpring.Snappy));
        }
        else
        {
            _scale.Jump(1);
            _offsetY.Jump(0);
        }
    }

    /// <summary>Closes with Arc's exit (fade and a slight shrink); the size is kept so the exit keeps its shape.</summary>
    public async void Dismiss()
    {
        if (!_shown)
        {
            return;
        }
        _shown = false;
        IsHitTestVisible = false;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off)
        {
            _opacity.Jump(0);
        }
        else
        {
            var duration = level == ArcMotionLevel.Full ? TimeSpan.FromMilliseconds(144) : ArcMotionPresets.Fast;
            if (level == ArcMotionLevel.Full)
            {
                _ = _scale.AnimateTo(0.96, MotionSpec.Tween(duration, ArcEasings.Standard));
            }
            await _opacity.AnimateTo(0, MotionSpec.Tween(duration, ArcEasings.Standard));
        }
        if (!_shown)
        {
            IsVisible = false;
            if (_linkFace)
            {
                SetLinkFace(false, animate: false);
            }
        }
    }

    /// <summary>Switches between the format face and the link face.</summary>
    public void SetLinkFace(bool link, bool animate = true)
    {
        if (link == _linkFace)
        {
            return;
        }
        _linkFace = link;
        var incoming = link ? _link : _format;
        var outgoing = link ? _format : _link;
        var direction = link ? 1 : -1;
        var level = animate && _shown ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        incoming.Show(direction, level);
        outgoing.Leave(direction, level);
        var target = FaceSize(incoming);
        if (level == ArcMotionLevel.Full && _size.Value.Width > 0)
        {
            _ = _size.AnimateTo(target, MotionSpec.Spring(s_morph));
        }
        else
        {
            _size.Jump(target);
        }
        if (_shown)
        {
            // Keep the morphing surface centred on the selection and inside the editor.
            var half = target.Width / 2;
            var x = _rootWidth > 0 ? Math.Max(half + 4, Math.Min(_position.Target.X, _rootWidth - half - 4)) : _position.Target.X;
            if (Math.Abs(x - _position.Target.X) > 0.5)
            {
                _ = _position.AnimateTo(new Point(x, _position.Target.Y), MotionSpec.Spring(s_glide));
            }
        }
    }

    /// <summary>Re-measures the current face (block tools hidden on narrow editors, the remove link button).</summary>
    public void Refit()
    {
        if (!_shown)
        {
            return;
        }
        var target = FaceSize(Current);
        if (_size.Target == target)
        {
            return;
        }
        if (ArcMotion.IsFull(this))
        {
            _ = _size.AnimateTo(target, MotionSpec.Spring(s_morph));
        }
        else
        {
            _size.Jump(target);
        }
    }

    private void UpdatePlacement()
    {
        var size = _size.Value;
        var position = _position.Value;
        var left = position.X - size.Width / 2;
        var top = (_below ? position.Y : position.Y - size.Height) + _offsetY.Value;
        SetPlacement(left, top, _scale.Value, size.Width / 2, _below ? 0 : size.Height);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var format = FaceSize(_format);
        var link = FaceSize(_link);
        Host.Measure(Size.Infinity);
        return new Size(Math.Max(format.Width, link.Width), Math.Max(format.Height, link.Height));
    }

    /// <summary>One face of the toolbar: it slides in from the side it comes from and unblurs, or fades under reduced motion.</summary>
    private sealed class Face
    {
        private static readonly SpringSpec s_morphSpring = SpringSpec.FromVisual(0.42, 0.12);
        private readonly TranslateTransform _translate = new();
        private readonly Avalonia.Media.BlurEffect _blur = new() { Radius = 0 };
        private readonly MotionValue<double> _x;
        private readonly MotionValue<double> _fade;
        private int _generation;

        public Face(Visual owner, Control content)
        {
            Content = content;
            content.RenderTransform = _translate;
            _x = new MotionValue<double>(owner, 0, MotionVectors.Double);
            _fade = new MotionValue<double>(owner, 1, MotionVectors.Double);
            _x.Changed += value => _translate.X = value;
            _fade.Changed += value =>
            {
                content.Opacity = value;
                var radius = (1 - value) * 2;
                _blur.Radius = radius;
                content.Effect = radius > 0.05 && _blurring ? _blur : null;
            };
        }

        private bool _blurring;

        public Control Content { get; }

        public void Hide()
        {
            Content.IsVisible = false;
            Content.IsHitTestVisible = false;
            _fade.Jump(0);
        }

        public void Show(int direction, ArcMotionLevel level)
        {
            _generation++;
            Content.IsVisible = true;
            Content.IsHitTestVisible = true;
            _blurring = level == ArcMotionLevel.Full;
            if (level == ArcMotionLevel.Off)
            {
                _x.Jump(0);
                _fade.Jump(1);
                return;
            }
            _fade.Jump(0);
            if (level == ArcMotionLevel.Full)
            {
                _x.Jump(direction * Travel);
                _ = _x.AnimateTo(0, MotionSpec.Spring(s_morphSpring));
                _ = _fade.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(200), ArcEasings.Enter, TimeSpan.FromMilliseconds(30)));
            }
            else
            {
                _x.Jump(0);
                _ = _fade.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(140), ArcEasings.Standard));
            }
        }

        public async void Leave(int direction, ArcMotionLevel level)
        {
            var generation = ++_generation;
            Content.IsHitTestVisible = false;
            _blurring = level == ArcMotionLevel.Full;
            if (level == ArcMotionLevel.Off)
            {
                Hide();
                return;
            }
            Task<bool> done;
            if (level == ArcMotionLevel.Full)
            {
                _ = _x.AnimateTo(direction * -Travel, MotionSpec.Spring(s_morphSpring));
                done = _fade.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(120), ArcEasings.Standard));
            }
            else
            {
                done = _fade.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(100), ArcEasings.Standard));
            }
            await done;
            if (generation == _generation)
            {
                Content.IsVisible = false;
            }
        }
    }
}

/// <summary>A row of the slash menu: icon, label and the Markdown it stands for.</summary>
internal sealed record RichTextEditorSlashItem(RichTextBlockType Type, string Label, string Hint, string Keywords, Geometry Icon)
{
    public static readonly RichTextEditorSlashItem[] All =
    {
        new(RichTextBlockType.Paragraph, "Text", string.Empty, "paragraph plain body", ArcIcons.Pilcrow),
        new(RichTextBlockType.Heading1, "Heading 1", "#", "title h1 large", ArcIcons.Heading1),
        new(RichTextBlockType.Heading2, "Heading 2", "##", "subtitle h2 section", ArcIcons.Heading2),
        new(RichTextBlockType.Heading3, "Heading 3", "###", "h3 small", ArcIcons.Heading3),
        new(RichTextBlockType.BulletedList, "Bulleted list", "-", "bullet unordered ul points", ArcIcons.List),
        new(RichTextBlockType.NumberedList, "Numbered list", "1.", "ordered ol steps numbers", ArcIcons.ListOrdered),
        new(RichTextBlockType.Quote, "Quote", ">", "blockquote citation callout", ArcIcons.Quote),
        new(RichTextBlockType.CodeBlock, "Code block", "```", "pre snippet monospace", ArcIcons.SquareCode),
        new(RichTextBlockType.Divider, "Divider", "---", "rule hr line separator", ArcIcons.Minus)
    };

    /// <summary>Arc's filter: a word of the label or keywords starts with the query, or the label contains it.</summary>
    public static List<RichTextEditorSlashItem> Filter(string query)
    {
        var q = query.Trim().ToLowerInvariant();
        if (q.Length == 0)
        {
            return All.ToList();
        }
        return All.Where(item =>
            $"{item.Label} {item.Keywords}".ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Any(word => word.StartsWith(q, StringComparison.Ordinal))
            || item.Label.ToLowerInvariant().Contains(q, StringComparison.Ordinal)).ToList();
    }
}

/// <summary>One option of the slash menu (a list item for assistive technology).</summary>
[PseudoClasses(ActivePseudoClass)]
internal sealed class RichTextEditorSlashRow : TemplatedControl
{
    public const string ActivePseudoClass = ":active";

    public static readonly StyledProperty<Geometry?> IconProperty = AvaloniaProperty.Register<RichTextEditorSlashRow, Geometry?>(nameof(Icon));
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<RichTextEditorSlashRow, string?>(nameof(Label));
    public static readonly StyledProperty<string?> HintProperty = AvaloniaProperty.Register<RichTextEditorSlashRow, string?>(nameof(Hint));
    public static readonly StyledProperty<bool> IsActiveProperty = AvaloniaProperty.Register<RichTextEditorSlashRow, bool>(nameof(IsActive));

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Hint { get => GetValue(HintProperty); set => SetValue(HintProperty, value); }
    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    public RichTextEditorSlashRow() : this(RichTextEditorSlashItem.All[0])
    {
    }

    public RichTextEditorSlashRow(RichTextEditorSlashItem item)
    {
        Item = item;
        Icon = item.Icon;
        Label = item.Label;
        Hint = item.Hint;
        AutomationProperties.SetName(this, item.Label);
    }

    public RichTextEditorSlashItem Item { get; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty)
        {
            PseudoClasses.Set(ActivePseudoClass, IsActive);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new EditorsGroupAutomationPeer(this, AutomationControlType.ListItem);
}

/// <summary>
/// Arc's slash menu: block types filtered by the typed query, a highlight that glides between rows, and a panel whose
/// height springs to its rows (capped by the room left in the clipping ancestor).
/// </summary>
internal sealed class RichTextEditorSlashMenu : RichTextEditorFloatingSurface
{
    private const double RowHeight = 34;
    private const double Padding = 6;
    private const double PanelWidth = 240;
    private static readonly SpringSpec s_glide = SpringSpec.FromVisual(0.3, 0.08);
    private static readonly SpringSpec s_grow = SpringSpec.FromVisual(0.4, 0);

    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<RichTextEditorSlashMenu, IBrush?>(nameof(HighlightBrush));

    public IBrush? HighlightBrush { get => GetValue(HighlightBrushProperty); set => SetValue(HighlightBrushProperty, value); }

    private readonly Dictionary<RichTextBlockType, RichTextEditorSlashRow> _rows = new();
    private readonly Highlight _highlight;
    private readonly MotionValue<double> _height;
    private readonly MotionValue<Rect> _highlightRect;
    private readonly MotionValue<double> _scale;
    private readonly MotionValue<double> _offsetY;
    private readonly MotionValue<double> _opacity;
    private List<RichTextEditorSlashItem> _items = new();
    private int _active;
    private bool _shown;
    private bool _above;
    private bool _highlightPlaced;
    private double _maxHeight = 320;
    private double _scroll;
    private Point _anchor;
    private double _width = PanelWidth;

    public RichTextEditorSlashMenu()
    {
        SurfaceRadius = 18;
        _highlight = new Highlight(this);
        Host.Children.Add(_highlight);
        foreach (var item in RichTextEditorSlashItem.All)
        {
            var row = new RichTextEditorSlashRow(item);
            row.PointerMoved += (_, _) => Hovered?.Invoke(_items.IndexOf(row.Item));
            row.AddHandler(PointerReleasedEvent, (_, e) =>
            {
                if (e.InitialPressMouseButton == MouseButton.Left || e.Pointer.Type != PointerType.Mouse)
                {
                    Chosen?.Invoke(row.Item);
                }
            }, RoutingStrategies.Bubble, handledEventsToo: true);
            _rows[item.Type] = row;
            Host.Children.Add(row);
        }
        _height = new MotionValue<double>(this, 0, MotionVectors.Double);
        _highlightRect = new MotionValue<Rect>(this, default, MotionVectors.Rect);
        _scale = new MotionValue<double>(this, 1, MotionVectors.Double);
        _offsetY = new MotionValue<double>(this, 0, MotionVectors.Double);
        _opacity = new MotionValue<double>(this, 0, MotionVectors.Double);
        _height.Changed += _ =>
        {
            SetSurfaceSize(new Size(_width, _height.Value));
            var scroll = _scroll;
            ClampScroll();
            if (scroll != _scroll)
            {
                PlaceRows();
            }
            UpdatePlacement();
        };
        _highlightRect.Changed += _ => _highlight.InvalidateVisual();
        _scale.Changed += _ => UpdatePlacement();
        _offsetY.Changed += _ => UpdatePlacement();
        _opacity.Changed += value => Opacity = value;
        Opacity = 0;
        AutomationProperties.SetName(this, "Blocks");
        PointerWheelChanged += (_, e) =>
        {
            _scroll -= e.Delta.Y * 40;
            ClampScroll();
            PlaceRows();
            e.Handled = true;
        };
    }

    /// <summary>Raised with the index of the row under the pointer.</summary>
    public event Action<int>? Hovered;

    /// <summary>Raised when a row is clicked.</summary>
    public event Action<RichTextEditorSlashItem>? Chosen;

    public bool IsShown => _shown;

    public IReadOnlyList<RichTextEditorSlashItem> Items => _items;

    public int ActiveIndex => _active;

    protected override AutomationPeer OnCreateAutomationPeer() => new EditorsGroupAutomationPeer(this, AutomationControlType.List);

    private double ContentHeight => Padding * 2 + _items.Count * RowHeight;

    /// <summary>Shows the filtered rows at <paramref name="anchor"/> (top left, or bottom left when <paramref name="above"/>).</summary>
    public void Present(Point anchor, bool above, double maxHeight, double maxWidth, IReadOnlyList<RichTextEditorSlashItem> items, int active)
    {
        var fresh = !_shown || anchor != _anchor || above != _above;
        _anchor = anchor;
        _above = above;
        _maxHeight = maxHeight;
        _width = Math.Max(120, Math.Min(PanelWidth, maxWidth));
        var changed = !_items.SequenceEqual(items);
        _items = items.ToList();
        foreach (var row in _rows.Values)
        {
            row.IsVisible = false;
        }
        foreach (var item in _items)
        {
            _rows[item.Type].IsVisible = true;
        }
        _active = Math.Clamp(active, 0, Math.Max(0, _items.Count - 1));
        for (var i = 0; i < _items.Count; i++)
        {
            _rows[_items[i].Type].IsActive = i == _active;
        }
        var full = ArcMotion.IsFull(this);
        var target = Math.Min(ContentHeight, _maxHeight);
        if (fresh)
        {
            var wasShown = _shown;
            _shown = true;
            IsVisible = true;
            IsHitTestVisible = true;
            _scroll = 0;
            _highlightPlaced = false;
            _height.Jump(target);
            if (!wasShown || fresh)
            {
                Enter(full);
            }
        }
        else if (changed || Math.Abs(_height.Target - target) > 0.5)
        {
            if (full)
            {
                _ = _height.AnimateTo(target, MotionSpec.Spring(s_grow));
            }
            else
            {
                _height.Jump(target);
            }
        }
        SetSurfaceSize(new Size(_width, _height.Value));
        InvalidateMeasure();
        InvalidateArrange();
        MoveHighlight(full);
        UpdatePlacement();
    }

    private void Enter(bool full)
    {
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            _opacity.Jump(1);
            _scale.Jump(1);
            _offsetY.Jump(0);
            return;
        }
        _opacity.Jump(0);
        _ = _opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
        if (full)
        {
            _scale.Jump(0.96);
            _offsetY.Jump(_above ? 4 : -4);
            _ = _scale.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
            _ = _offsetY.AnimateTo(0, MotionSpec.Spring(ArcSpring.Snappy));
        }
        else
        {
            _scale.Jump(1);
            _offsetY.Jump(0);
        }
    }

    public async void Dismiss()
    {
        if (!_shown)
        {
            return;
        }
        _shown = false;
        IsHitTestVisible = false;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off)
        {
            _opacity.Jump(0);
        }
        else
        {
            var duration = level == ArcMotionLevel.Full ? TimeSpan.FromMilliseconds(144) : ArcMotionPresets.Fast;
            if (level == ArcMotionLevel.Full)
            {
                _ = _scale.AnimateTo(0.97, MotionSpec.Tween(duration, ArcEasings.Standard));
            }
            await _opacity.AnimateTo(0, MotionSpec.Tween(duration, ArcEasings.Standard));
        }
        if (!_shown)
        {
            IsVisible = false;
        }
    }

    private void MoveHighlight(bool full)
    {
        if (_items.Count == 0)
        {
            return;
        }
        var top = Padding + _active * RowHeight;
        var rect = new Rect(Padding, top, _width - Padding * 2, RowHeight);
        // A short panel scrolls to keep the active row in view.
        var visible = _height.Target;
        if (top - Padding < _scroll)
        {
            _scroll = top - Padding;
        }
        else if (top + RowHeight + Padding > _scroll + visible)
        {
            _scroll = top + RowHeight + Padding - visible;
        }
        ClampScroll();
        PlaceRows();
        if (!_highlightPlaced || !full)
        {
            _highlightRect.Jump(rect);
            _highlightPlaced = true;
        }
        else
        {
            _ = _highlightRect.AnimateTo(rect, MotionSpec.Spring(s_glide));
        }
    }

    private void ClampScroll()
    {
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, ContentHeight - _height.Value));
    }

    private void UpdatePlacement()
    {
        var height = _height.Value;
        var top = (_above ? _anchor.Y - height : _anchor.Y) + _offsetY.Value;
        SetPlacement(_anchor.X, top, _scale.Value, 16, _above ? height : 0);
    }

    /// <summary>Places the rows inside the clip host (one pixel in, under the border), shifted by the scroll offset.</summary>
    private void PlaceRows()
    {
        var rowWidth = _width - Padding * 2;
        _highlight.Width = _width;
        _highlight.Height = Math.Max(ContentHeight, _maxHeight);
        for (var i = 0; i < _items.Count; i++)
        {
            var row = _rows[_items[i].Type];
            row.Width = rowWidth;
            Canvas.SetLeft(row, Padding - 1);
            Canvas.SetTop(row, Padding - 1 + i * RowHeight - _scroll);
        }
        _highlight.InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        PlaceRows();
        Host.Measure(Size.Infinity);
        return new Size(_width, Math.Max(ContentHeight, double.IsFinite(_maxHeight) ? _maxHeight : ContentHeight));
    }

    /// <summary>The gliding row highlight, drawn behind the rows.</summary>
    private sealed class Highlight(RichTextEditorSlashMenu menu) : Control
    {
        public override void Render(DrawingContext context)
        {
            if (menu.HighlightBrush is { } brush && menu._items.Count > 0)
            {
                var rect = menu._highlightRect.Value.Translate(new Vector(-1, -1 - menu._scroll));
                context.DrawRectangle(brush, null, new RoundedRect(rect, 12));
            }
        }
    }
}
