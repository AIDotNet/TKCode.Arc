using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The drawn plate of an <see cref="ArmingSwitch"/>: brushed aluminium with engraving and screws, the chrome bat toggle
/// under a translucent hinged cover, and the lamp with its hold ring (Arc Arming switch, design reference; all geometry
/// in plate widths W, the plate W × 0.62 W, at most <see cref="ArmingSwitchToken.PlateMaxWidth"/>, with 0.12 W above it
/// for the flipped cover and 0.025 W below). Three <see cref="ArmingSwitchHit"/> children stand over the cover, the
/// switch and the lamp for focus, pointer and assistive technology.
/// </summary>
internal sealed partial class ArmingSwitchStage : Control
{
    public static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<ArmingSwitchStage, IBrush?>(nameof(FocusRingBrush));

    // Arc Arming switch (design reference): the stage's padding around the plate, in plate widths.
    private const double TopRoom = 0.12;
    private const double BottomRoom = 0.025;

    /// <summary>The plate's width for a stage <paramref name="width"/> wide: the stage keeps 2.5% of its width on each side (1% at 310 px and below), up to the token's maximum.</summary>
    private double PlateWidthFor(double width)
    {
        var side = width >= 460 ? 0.025 : 0.01 + (0.015 * Math.Clamp((width - 310) / 150, 0, 1));
        return Math.Max(120, Math.Min(width * (1 - (2 * side)), PlateMaxWidth));
    }

    private readonly ArmingSwitchView _view = new();
    private readonly ArmingSwitchHit _coverHit;
    private readonly ArmingSwitchHit _switchHit;
    private readonly ArmingSwitchHit _lampHit;
    private Rect _coverRect;
    private Rect _switchRect;
    private Rect _lampRect;
    private double _w = 442;
    private Point _plateOffset;

    static ArmingSwitchStage()
    {
        AffectsRender<ArmingSwitchStage>(FocusRingBrushProperty);
    }

    public ArmingSwitchStage()
    {
        ClipToBounds = false;
        _coverHit = new ArmingSwitchHit(this, ArmingSwitchPart.Cover);
        _switchHit = new ArmingSwitchHit(this, ArmingSwitchPart.Switch);
        _lampHit = new ArmingSwitchHit(this, ArmingSwitchPart.Lamp);
        foreach (var hit in new[] { _coverHit, _switchHit, _lampHit })
        {
            hit.Bind(ArmingSwitchHit.FocusRingBrushProperty, this.GetObservable(FocusRingBrushProperty));
            LogicalChildren.Add(hit);
            VisualChildren.Add(hit);
        }
        _loop = new LiveLoop(this, OnFrame) { MinimumLevel = ArcMotionLevel.Off };
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        InitMotion();
    }

    public IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }

    public ArmingSwitch? Owner { get; private set; }

    internal ArmingSwitchHit CoverHit => _coverHit;

    internal ArmingSwitchHit SwitchHit => _switchHit;

    internal ArmingSwitchHit LampHit => _lampHit;

    /// <summary>The plate's width in pixels after the last layout.</summary>
    internal double PlateWidth => _w;

    internal ArmingSwitchView View => _view;

    public void Attach(ArmingSwitch owner)
    {
        Owner = owner;
        SnapToState();
        InvalidateLook();
        UpdateAutomation();
    }

    public void Detach()
    {
        CancelHold();
        Owner = null;
    }

    /// <summary>The accessible name of a part.</summary>
    internal string NameOf(ArmingSwitchPart part)
    {
        var owner = Owner;
        if (owner is null)
        {
            return string.Empty;
        }
        return part switch
        {
            ArmingSwitchPart.Cover => owner.IsCoverOpen ? ArmingSwitch.CloseCoverName : ArmingSwitch.LiftCoverName,
            ArmingSwitchPart.Switch => owner.ArmLabel + " switch",
            _ => owner.HoldLabel
        };
    }

    public void UpdateAutomation()
    {
        var owner = Owner;
        if (owner is null)
        {
            return;
        }
        var hint = owner.CurrentHint;
        // Under a closed cover the pointer reaches the cover, not the switch (the keyboard still reaches both).
        _switchHit.IsHitTestVisible = owner.IsCoverOpen;
        AutomationProperties.SetName(_coverHit, NameOf(ArmingSwitchPart.Cover));
        AutomationProperties.SetName(_switchHit, NameOf(ArmingSwitchPart.Switch));
        AutomationProperties.SetName(_lampHit, NameOf(ArmingSwitchPart.Lamp));
        foreach (var hit in new[] { _coverHit, _switchHit, _lampHit })
        {
            AutomationProperties.SetHelpText(hit, hint);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ThemeVariantScope.ActualThemeVariantProperty || change.Property == TextElement.FontFamilyProperty)
        {
            InvalidateLook();
        }
        else if (change.Property == ArcMotion.LevelProperty)
        {
            Kick();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        InvalidateLook();
        _shownClock = 0;
        Kick();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CancelHold();
        SnapToState();
    }

    private double PlateMaxWidth => Owner?.Token(ArmingSwitchTokenKind.PlateMaxWidth, 560) ?? 560;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? PlateMaxWidth / 0.95 : availableSize.Width;
        var w = PlateWidthFor(width);
        foreach (var hit in new[] { _coverHit, _switchHit, _lampHit })
        {
            hit.Measure(Size.Infinity);
        }
        return new Size(width, w * (TopRoom + ArmingSwitchView.PlateHeight + BottomRoom));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var w = PlateWidthFor(finalSize.Width);
        if (Math.Abs(w - _w) > 0.01)
        {
            _w = w;
            InvalidateText();
        }
        _plateOffset = new Point((finalSize.Width - w) / 2, w * TopRoom);
        UpdateView();
        PlaceHits(force: true);
        return finalSize;
    }

    /// <summary>Moves the three hit areas over their parts as they are drawn now.</summary>
    private void PlaceHits(bool force)
    {
        var cover = CoverHitRect();
        var sw = PlateRect(0.225, 0.17, 0.19, 0.34);
        var lamp = PlateRect(0.58, 0.15, 0.30, 0.30);
        var moved = force || Differs(cover, _coverRect) || Differs(sw, _switchRect) || Differs(lamp, _lampRect);
        _coverRect = cover;
        _switchRect = sw;
        _lampRect = lamp;
        var radius = 0.03 * _w;
        _coverHit.Radius = radius;
        _switchHit.Radius = radius;
        _lampHit.Radius = lamp.Width / 2;
        if (force)
        {
            _coverHit.Arrange(cover);
            _switchHit.Arrange(sw);
            _lampHit.Arrange(lamp);
        }
        else if (moved)
        {
            InvalidateArrange();
        }
    }

    private static bool Differs(Rect a, Rect b) =>
        Math.Abs(a.X - b.X) > 0.5 || Math.Abs(a.Y - b.Y) > 0.5 || Math.Abs(a.Width - b.Width) > 0.5 || Math.Abs(a.Height - b.Height) > 0.5;

    /// <summary>The projected bounds of a rectangle on the plate (in plate widths).</summary>
    private Rect PlateRect(double x, double y, double width, double height, double z = 0)
    {
        var a = _view.Project(new ArmingVec(x, y, z));
        var b = _view.Project(new ArmingVec(x + width, y, z));
        var c = _view.Project(new ArmingVec(x, y + height, z));
        var d = _view.Project(new ArmingVec(x + width, y + height, z));
        var left = Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X));
        var top = Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y));
        var right = Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X));
        var bottom = Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y));
        return new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>The cover's hit area: its projected box at the current angle, grown by 0.035 W (Arc: 0.33 × 0.5 W when closed).</summary>
    private Rect CoverHitRect()
    {
        double left = double.MaxValue, top = double.MaxValue, right = double.MinValue, bottom = double.MinValue;
        for (var i = 0; i < 8; i++)
        {
            var p = _view.Project(CoverCorner(i));
            left = Math.Min(left, p.X);
            top = Math.Min(top, p.Y);
            right = Math.Max(right, p.X);
            bottom = Math.Max(bottom, p.Y);
        }
        var grow = 0.035 * _w;
        return new Rect(left - grow, top - grow, (right - left) + (2 * grow), (bottom - top) + (2 * grow) + (0.01 * _w));
    }
}
