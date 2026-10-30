using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The status mark at the start of the status line of a <see cref="BuildingBlocks"/>: a dashed square while idle, four
/// small blocks while building (with full motion a light runs round them, 300ms a block, on the owner's clock so it pauses
/// off screen; otherwise they fill one per phase) and a check on Ready, so the state never relies on color alone.
/// </summary>
internal sealed class BuildingBlocksMark : Control
{
    public static readonly StyledProperty<BuildingBlocksTone> ToneProperty =
        BuildingBlocks.ToneProperty.AddOwner<BuildingBlocksMark>();

    public static readonly StyledProperty<Color?> TintProperty =
        BuildingBlocks.TintProperty.AddOwner<BuildingBlocksMark>();

    public static readonly StyledProperty<IBrush?> AccentInkProperty = AvaloniaProperty.Register<BuildingBlocksMark, IBrush?>(nameof(AccentInk));
    public static readonly StyledProperty<IBrush?> ForegroundInkProperty = AvaloniaProperty.Register<BuildingBlocksMark, IBrush?>(nameof(ForegroundInk));
    public static readonly StyledProperty<IBrush?> MutedInkProperty = AvaloniaProperty.Register<BuildingBlocksMark, IBrush?>(nameof(MutedInk));

    /// <summary>Seconds the light spends on each of the four blocks.</summary>
    public const double BeatSeconds = 0.3;

    /// <summary>The opacity of a block the light is not on.</summary>
    public const double RestOpacity = 0.28;

    private BuildingBlocksPhase _phase = BuildingBlocksPhase.Idle;
    private BuildingBlocksTween _check;
    private double _now;
    private double _beat;
    private bool _animated;
    private bool _paletteDirty = true;
    private IBrush? _tint;
    private IPen? _checkPen;
    private IPen? _dashPen;

    public BuildingBlocksMark()
    {
        IsHitTestVisible = false;
        Focusable = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        _check.Jump(0);
    }

    public BuildingBlocksTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    public Color? Tint { get => GetValue(TintProperty); set => SetValue(TintProperty, value); }

    public IBrush? AccentInk { get => GetValue(AccentInkProperty); set => SetValue(AccentInkProperty, value); }

    public IBrush? ForegroundInk { get => GetValue(ForegroundInkProperty); set => SetValue(ForegroundInkProperty, value); }

    public IBrush? MutedInk { get => GetValue(MutedInkProperty); set => SetValue(MutedInkProperty, value); }

    /// <summary>The phase drawn.</summary>
    internal BuildingBlocksPhase Phase => _phase;

    /// <summary>The light runs round the blocks (building, full motion).</summary>
    internal bool IsLightRunning => _animated && IsBuilding(_phase);

    /// <summary>The check's appearance is still fading in.</summary>
    internal bool IsMoving => _check.IsMoving(_now);

    private static bool IsBuilding(BuildingBlocksPhase phase) =>
        phase is BuildingBlocksPhase.Layout or BuildingBlocksPhase.Components or BuildingBlocksPhase.Styling;

    /// <summary>Shows <paramref name="phase"/>; <paramref name="level"/> decides whether the light runs and the check fades.</summary>
    internal void Show(BuildingBlocksPhase phase, ArcMotionLevel level)
    {
        _animated = level == ArcMotionLevel.Full;
        if (phase == _phase)
        {
            return;
        }
        _phase = phase;
        if (phase == BuildingBlocksPhase.Ready)
        {
            _check.Jump(0);
            _check.To(1, _now, level == ArcMotionLevel.Off ? 0 : ArcMotionPresets.Fast.TotalSeconds, 0, ArcEasings.Enter);
        }
        else
        {
            _check.Jump(0);
        }
        InvalidateVisual();
    }

    /// <summary>Moves the light and the check on (the owner's clock, which pauses off screen).</summary>
    internal void Advance(double seconds)
    {
        var redraw = IsLightRunning || IsMoving;
        _now += seconds;
        if (IsLightRunning)
        {
            _beat = (_beat + seconds) % (4 * BeatSeconds);
        }
        if (redraw)
        {
            InvalidateVisual();
        }
    }

    /// <summary>The opacity block <paramref name="index"/> (clockwise from the top left) is drawn with.</summary>
    internal double BlockOpacity(int index)
    {
        if (!IsLightRunning)
        {
            var filled = _phase switch
            {
                BuildingBlocksPhase.Layout => 0,
                BuildingBlocksPhase.Components => 1,
                _ => 2
            };
            return index <= filled ? 1 : RestOpacity;
        }
        var light = _beat / BeatSeconds;
        var distance = Math.Abs(light - index);
        distance = Math.Min(distance, 4 - distance);
        return RestOpacity + (1 - RestOpacity) * Math.Max(0, 1 - distance);
    }

    protected override Size MeasureOverride(Size availableSize) => new(16, 16);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ToneProperty || change.Property == TintProperty || change.Property == AccentInkProperty
            || change.Property == ForegroundInkProperty || change.Property == MutedInkProperty)
        {
            _paletteDirty = true;
            InvalidateVisual();
        }
    }

    private void EnsurePalette()
    {
        if (!_paletteDirty)
        {
            return;
        }
        _paletteDirty = false;
        var tint = BuildingBlocksPalette.Tint(Tint, Tone, AccentInk, ForegroundInk);
        var muted = BuildingBlocksPalette.ColorOf(MutedInk, Color.Parse("#7D7D7D"));
        _tint = new ImmutableSolidColorBrush(tint);
        _checkPen = new ImmutablePen(new ImmutableSolidColorBrush(tint), 2.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        _dashPen = new ImmutablePen(new ImmutableSolidColorBrush(muted), 1, new ImmutableDashStyle([2, 2], 0));
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0)
        {
            return;
        }
        EnsurePalette();
        var box = new Rect((Bounds.Width - size) / 2, (Bounds.Height - size) / 2, size, size);
        switch (_phase)
        {
            case BuildingBlocksPhase.Idle:
            {
                var inner = box.Deflate(size * 0.125 + 0.5);
                context.DrawRectangle(null, _dashPen, inner, size * 0.18, size * 0.18);
                break;
            }
            case BuildingBlocksPhase.Ready:
            {
                var scale = size / 24;
                using (context.PushOpacity(_check.Value(_now)))
                using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(box.X, box.Y)))
                {
                    context.DrawGeometry(null, _checkPen, ArcIcons.Check);
                }
                break;
            }
            default:
            {
                var gap = size * 0.125;
                var inset = size * 0.0625;
                var cell = (size - 2 * inset - gap) / 2;
                var r = cell * 0.28;
                for (var k = 0; k < 4; k++)
                {
                    // Clockwise from the top left: 0 TL, 1 TR, 2 BR, 3 BL.
                    var col = k is 1 or 2 ? 1 : 0;
                    var row = k >= 2 ? 1 : 0;
                    var rect = new Rect(box.X + inset + col * (cell + gap), box.Y + inset + row * (cell + gap), cell, cell);
                    using (context.PushOpacity(BlockOpacity(k)))
                    {
                        context.DrawRectangle(_tint, null, rect, r, r);
                    }
                }
                break;
            }
        }
    }
}
