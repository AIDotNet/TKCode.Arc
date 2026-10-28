using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The drawing layer of <see cref="InkFluid"/>: it owns the <see cref="InkFluidSolver"/>, feeds it the pointer's strokes
/// and the ambient stir, runs it on a <see cref="LiveLoop"/> (paused off screen, while hidden and below full motion) and
/// draws the composed ink stretched over its bounds with smooth filtering. Under reduced motion it composes one still
/// frame instead.
/// </summary>
internal sealed class InkFluidCanvas : Control
{
    /// <summary>The ambient stir starts this long after the last pointer move.</summary>
    public const double AmbientDelay = 1.2;

    /// <summary>Resizes wait for the size to settle this long before the grids are rebuilt.</summary>
    public static readonly TimeSpan ResizeDebounce = TimeSpan.FromMilliseconds(120);

    // Pointer strokes: the blob size (share of the short side), the largest ink a frame adds and the speed cap.
    private const double StrokeRadius = 0.03;
    private const double StrokeInkMax = 0.35;
    private const double StrokeInkPerTravel = 4;
    private const double MaxStrokeSpeed = 420;

    // A frame's stroke pushes the fluid with this share of the pointer's speed; strokes overlap from frame to frame, so
    // the full speed would pile up into a jet that throws the ink far past the pointer.
    private const double StrokePush = 0.4;

    // The ambient stir: a slow wandering blob.
    private const double AmbientRadius = 0.055;
    private const double AmbientInkRate = 2.4;
    private const double AmbientForce = 48;
    private const double AmbientPace = 0.18;

    // The still frame for reduced motion: 140 steps of the ambient stir from a fixed phase.
    private const int StillSteps = 140;
    private const double StillPhase = 3.2;

    private readonly InkFluidSolver _solver = new();
    private readonly LiveLoop _loop;
    private readonly DispatcherTimer _resize;
    private WriteableBitmap? _front;
    private WriteableBitmap? _back;
    private int[] _pixels = [];
    private Size _builtFor;
    private InkFluidQuality _builtQuality;
    private InkFluidQuality _quality = InkFluidQuality.Medium;
    private Color _inkColor = Colors.Black;
    private Color _accentColor = Colors.Black;
    private double _clock;
    private double _lastMove = double.NegativeInfinity;
    private double _ambientTime;
    private readonly double _ambientSeed = Random.Shared.NextDouble() * 100;
    private Point _pointer;
    private bool _hasPointer;
    private double _strokeX;
    private double _strokeY;
    private bool _stroked;

    public InkFluidCanvas()
    {
        IsHitTestVisible = false;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.HighQuality);
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Full };
        _resize = new DispatcherTimer { Interval = ResizeDebounce };
        _resize.Tick += (_, _) =>
        {
            _resize.Stop();
            Rebuild();
        };
    }

    internal InkFluidSolver Solver => _solver;

    internal LiveLoop Loop => _loop;

    /// <summary>The composed picture shown now (null before the first frame).</summary>
    internal Bitmap? Picture => _front;

    /// <summary>How dark the ink gets, 0 to 1.</summary>
    public double Intensity { get; set; } = 0.5;

    /// <summary>The share of fresh ink that takes the accent color, 0 to 1.</summary>
    public double Tint { get; set; } = 0.8;

    /// <summary>How hard the pointer stirs, 0 to 1.</summary>
    public double Force { get; set; } = 0.5;

    public bool Ambient { get; set; } = true;

    public bool Interactive { get; set; } = true;

    public InkFluidQuality Quality
    {
        get => _quality;
        set
        {
            if (_quality == value)
            {
                return;
            }
            _quality = value;
            Rebuild();
        }
    }

    /// <summary>Grid sizes for a quality: velocity cells and ink cells on the short side.</summary>
    internal static (int Cells, int InkCells) CellsFor(InkFluidQuality quality) => quality switch
    {
        InkFluidQuality.Low => (96, 192),
        InkFluidQuality.High => (192, 384),
        _ => (128, 256)
    };

    public void SetColors(Color ink, Color accent)
    {
        _inkColor = ink;
        _accentColor = accent;
        Recompose();
    }

    /// <summary>Settings changed: keep running, or redraw the still frame.</summary>
    public void Refresh()
    {
        if (ArcMotion.IsFull(this))
        {
            _loop.Start();
        }
        else
        {
            Recompose();
        }
    }

    /// <summary>The pointer moved to <paramref name="position"/> (in this control's coordinates).</summary>
    public void PointerMovedTo(Point position)
    {
        if (!Interactive || !ArcMotion.IsFull(this))
        {
            return;
        }
        if (_hasPointer)
        {
            _strokeX += position.X - _pointer.X;
            _strokeY += position.Y - _pointer.Y;
            _stroked = true;
        }
        _pointer = position;
        _hasPointer = true;
        _lastMove = _clock;
        _loop.Start();
    }

    /// <summary>The pointer left (or a finger lifted): the next move starts a new stroke.</summary>
    public void PointerGone() => _hasPointer = false;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _resize.Stop();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (_solver.Columns == 0)
        {
            Rebuild();
        }
        else
        {
            _resize.Stop();
            _resize.Start();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ArcMotion.LevelProperty)
        {
            Refresh();
        }
    }

    private void Rebuild()
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        if (_solver.Columns != 0 && size == _builtFor && _quality == _builtQuality)
        {
            return;
        }
        _builtFor = size;
        _builtQuality = _quality;
        var (cells, inkCells) = CellsFor(_quality);
        _solver.Resize(size.Width / size.Height, cells, inkCells);
        _pixels = new int[_solver.InkColumns * _solver.InkRows];
        _front?.Dispose();
        _back?.Dispose();
        _front = null;
        _back = null;
        _hasPointer = false;
        if (ArcMotion.IsFull(this))
        {
            Publish();
            _loop.Start();
        }
        else
        {
            Recompose();
        }
    }

    /// <summary>Redraws the picture from the current ink: the still frame when not in full motion.</summary>
    private void Recompose()
    {
        if (_solver.Columns == 0)
        {
            return;
        }
        if (!ArcMotion.IsFull(this))
        {
            ComposeStill();
            return;
        }
        Publish();
    }

    private void ComposeStill()
    {
        _solver.Clear();
        if (Ambient)
        {
            for (var i = 0; i < StillSteps; i++)
            {
                StirAmbient(StillPhase + i * (1.0 / 60) * 2.4, 1.0 / 60);
                _solver.Step(1.0 / 60);
            }
        }
        Publish();
    }

    private bool Step(double dt)
    {
        if (_solver.Columns == 0)
        {
            return true;
        }
        _clock += dt;
        if (_stroked)
        {
            Stroke(dt);
        }
        if (Ambient && _clock - _lastMove > AmbientDelay)
        {
            _ambientTime += dt;
            StirAmbient(_ambientSeed + _ambientTime, dt);
        }
        _solver.Step(dt);
        Publish();
        if (Ambient)
        {
            return true;
        }
        // Without the ambient stir the loop stops once the ink has faded: moving fluid with no ink in it draws nothing.
        return _solver.TotalInk() > 0.5;
    }

    private void Stroke(double dt)
    {
        var size = Bounds.Size;
        var shortSide = Math.Max(1, Math.Min(size.Width, size.Height));
        var travel = Math.Sqrt(_strokeX * _strokeX + _strokeY * _strokeY);
        var cellsPerPx = Math.Min(_solver.Columns, _solver.Rows) / shortSide;
        var gain = StrokePush * (0.5 + 1.5 * Math.Clamp(Force, 0, 1));
        var forceX = _strokeX * cellsPerPx / Math.Max(dt, 1e-3) * gain;
        var forceY = _strokeY * cellsPerPx / Math.Max(dt, 1e-3) * gain;
        var speed = Math.Sqrt(forceX * forceX + forceY * forceY);
        if (speed > MaxStrokeSpeed)
        {
            forceX *= MaxStrokeSpeed / speed;
            forceY *= MaxStrokeSpeed / speed;
        }
        // Ink scales with speed: slow moves leave faint trails, quick flicks bloom.
        var ink = Math.Min(StrokeInkMax, StrokeInkPerTravel * travel / shortSide);
        var tint = Math.Clamp(Tint, 0, 1);
        _solver.Splat(_pointer.X / size.Width, _pointer.Y / size.Height, forceX, forceY, ink * (1 - tint), ink * tint, StrokeRadius);
        _strokeX = 0;
        _strokeY = 0;
        _stroked = false;
    }

    private void StirAmbient(double time, double dt)
    {
        var t = time * AmbientPace;
        var x = 0.5 + 0.32 * Math.Sin(1.2 * t + 0.4) * Math.Cos(0.75 * t);
        var y = 0.5 + 0.26 * Math.Sin(0.95 * t + 1.4);
        var scale = Math.Min(_solver.Columns, _solver.Rows) / 128.0;
        var forceX = AmbientForce * scale * Math.Cos(1.3 * t);
        var forceY = AmbientForce * scale * Math.Sin(1.7 * t);
        var ink = AmbientInkRate * dt;
        var tint = Math.Clamp(Tint, 0, 1);
        _solver.Splat(x, y, forceX, forceY, ink * (1 - tint * 0.85), ink * tint * 0.85, AmbientRadius);
    }

    /// <summary>Composes the ink into the back bitmap and swaps it to the front.</summary>
    private void Publish()
    {
        if (_solver.Columns == 0)
        {
            return;
        }
        var pixelSize = new PixelSize(_solver.InkColumns, _solver.InkRows);
        _solver.Compose(_pixels, _inkColor, _accentColor, Strength());
        try
        {
            _back ??= new WriteableBitmap(pixelSize, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            using (var locked = _back.Lock())
            {
                var width = pixelSize.Width;
                if (locked.RowBytes == width * 4)
                {
                    Marshal.Copy(_pixels, 0, locked.Address, _pixels.Length);
                }
                else
                {
                    for (var y = 0; y < pixelSize.Height; y++)
                    {
                        Marshal.Copy(_pixels, y * width, locked.Address + y * locked.RowBytes, width);
                    }
                }
            }
        }
        catch (Exception)
        {
            // No pixel access on this platform: the plain background shows, as the original does without WebGL2.
            _back?.Dispose();
            _back = null;
            return;
        }
        (_front, _back) = (_back, _front);
        InvalidateVisual();
    }

    /// <summary>The ink's top coverage: capped low so text over it keeps its contrast, a little higher on dark themes.</summary>
    private double Strength()
    {
        var dark = Luminance(_inkColor) > 0.5;
        return Math.Min(1, Math.Clamp(Intensity, 0, 1) * (dark ? 0.42 : 0.34));
    }

    private static double Luminance(Color color) => (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255;

    public override void Render(DrawingContext context)
    {
        if (_front is not { } picture || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }
        var pixels = picture.PixelSize;
        context.DrawImage(picture, new Rect(0, 0, pixels.Width, pixels.Height), new Rect(Bounds.Size));
    }
}
