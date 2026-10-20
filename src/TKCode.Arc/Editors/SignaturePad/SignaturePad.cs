using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Export formats of <see cref="SignaturePad"/>.</summary>
public enum SignatureFileKind
{
    /// <summary>A transparent PNG cropped to the ink, at three times pad resolution.</summary>
    Png,

    /// <summary>A standalone SVG cropped to the ink.</summary>
    Svg
}

/// <summary>Arguments of <see cref="SignaturePad.StrokesChanged"/>.</summary>
public sealed class SignatureStrokesChangedEventArgs : RoutedEventArgs
{
    internal SignatureStrokesChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<InkStroke> strokes) : base(routedEvent)
    {
        Strokes = strokes;
    }

    /// <summary>The strokes after the change (drawing, undo, redo or clear).</summary>
    public IReadOnlyList<InkStroke> Strokes { get; }
}

/// <summary>
/// Arc's signature field with real ink: width follows pen pressure, or speed for a mouse or finger, and the ends taper
/// like a nib lifting off. It keeps undo and redo, clears with a wipe across the pad, replays the signature in its
/// original rhythm, and exports a cropped PNG or SVG. A "Sign here" hint rests on the baseline until the first stroke.
/// </summary>
/// <remarks>Keyboard on the pad: Mod+Z undoes, Shift+Mod+Z or Mod+Y redoes, Delete or Backspace clears, Escape stops a replay.</remarks>
[TemplatePart(PadPart, typeof(SignaturePadCanvas))]
[TemplatePart(ColorsPart, typeof(SignaturePadChoice))]
[TemplatePart(WidthsPart, typeof(SignaturePadChoice))]
[TemplatePart(UndoPart, typeof(SignaturePadIconButton))]
[TemplatePart(RedoPart, typeof(SignaturePadIconButton))]
[TemplatePart(ReplayPart, typeof(SignaturePadIconButton))]
[TemplatePart(ClearPart, typeof(SignaturePadIconButton))]
[TemplatePart(PngPart, typeof(Button))]
[TemplatePart(SvgPart, typeof(Button))]
[TemplatePart(StatusPart, typeof(EditorsLiveRegion))]
[PseudoClasses(EmptyPseudoClass, ReplayingPseudoClass)]
public class SignaturePad : TemplatedControl
{
    public const string PadPart = "PART_Pad";
    public const string ColorsPart = "PART_Colors";
    public const string WidthsPart = "PART_Widths";
    public const string UndoPart = "PART_Undo";
    public const string RedoPart = "PART_Redo";
    public const string ReplayPart = "PART_Replay";
    public const string ClearPart = "PART_Clear";
    public const string PngPart = "PART_Png";
    public const string SvgPart = "PART_Svg";
    public const string StatusPart = "PART_Status";
    public const string EmptyPseudoClass = ":empty";
    public const string ReplayingPseudoClass = ":replaying";

    private const int HistoryLimit = 60;

    /// <summary>Printed under the baseline, such as the signer's name.</summary>
    public static readonly StyledProperty<string?> SignerProperty = AvaloniaProperty.Register<SignaturePad, string?>(nameof(Signer));

    /// <summary>The resting hint on the baseline before the first stroke (default "Sign here").</summary>
    public static readonly StyledProperty<string> HintProperty = AvaloniaProperty.Register<SignaturePad, string>(nameof(Hint), "Sign here");

    public static readonly StyledProperty<SignatureInkColor> InkColorProperty =
        AvaloniaProperty.Register<SignaturePad, SignatureInkColor>(nameof(InkColor), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<SignatureInkWidth> InkWidthProperty =
        AvaloniaProperty.Register<SignaturePad, SignatureInkWidth>(nameof(InkWidth), SignatureInkWidth.Medium, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>File name for exports, without extension (default "signature").</summary>
    public static readonly StyledProperty<string> FileNameProperty = AvaloniaProperty.Register<SignaturePad, string>(nameof(FileName), "signature");

    public static readonly DirectProperty<SignaturePad, IReadOnlyList<InkStroke>> StrokesProperty =
        AvaloniaProperty.RegisterDirect<SignaturePad, IReadOnlyList<InkStroke>>(nameof(Strokes), o => o.Strokes);

    public static readonly DirectProperty<SignaturePad, bool> IsEmptyProperty =
        AvaloniaProperty.RegisterDirect<SignaturePad, bool>(nameof(IsEmpty), o => o.IsEmpty);

    public static readonly DirectProperty<SignaturePad, bool> CanUndoProperty =
        AvaloniaProperty.RegisterDirect<SignaturePad, bool>(nameof(CanUndo), o => o.CanUndo);

    public static readonly DirectProperty<SignaturePad, bool> CanRedoProperty =
        AvaloniaProperty.RegisterDirect<SignaturePad, bool>(nameof(CanRedo), o => o.CanRedo);

    public static readonly DirectProperty<SignaturePad, bool> IsReplayingProperty =
        AvaloniaProperty.RegisterDirect<SignaturePad, bool>(nameof(IsReplaying), o => o.IsReplaying);

    /// <summary>Raised after every change: a stroke drawn, undo, redo or clear (Arc's <c>onChange</c>).</summary>
    public static readonly RoutedEvent<SignatureStrokesChangedEventArgs> StrokesChangedEvent =
        RoutedEvent.Register<SignaturePad, SignatureStrokesChangedEventArgs>(nameof(StrokesChanged), RoutingStrategies.Bubble);

    private readonly List<IReadOnlyList<InkStroke>> _past = new();
    private readonly List<IReadOnlyList<InkStroke>> _future = new();
    private IReadOnlyList<InkStroke> _strokes = Array.Empty<InkStroke>();
    private bool _isEmpty = true;
    private bool _canUndo;
    private bool _canRedo;
    private bool _isReplaying;
    private SignaturePadCanvas? _pad;
    private SignaturePadChoice? _colors;
    private SignaturePadChoice? _widths;
    private SignaturePadIconButton? _undo;
    private SignaturePadIconButton? _redo;
    private SignaturePadIconButton? _replay;
    private SignaturePadIconButton? _clear;
    private ArcContentSwap? _replayIcon;
    private Button? _png;
    private Button? _svg;
    private ArcContentSwap? _pngContent;
    private ArcContentSwap? _svgContent;
    private EditorsLiveRegion? _status;
    private LiveStroke? _live;
    private int _counter;
    private MotionValue<double>? _wipe;
    private MotionValue<double>? _replayClock;
    private bool _wiping;
    private DispatcherTimer? _savedTimer;
    private static int s_instances;
    private readonly string _uid = "sig" + Interlocked.Increment(ref s_instances).ToString(CultureInfo.InvariantCulture);

    private sealed class LiveStroke(IPointer pointer, ulong stamp, string id, SignatureInkColor color, SignatureInkWidth width, double at)
    {
        public IPointer Pointer { get; } = pointer;
        public ulong Stamp { get; } = stamp;
        public string Id { get; } = id;
        public SignatureInkColor Color { get; } = color;
        public SignatureInkWidth Width { get; } = width;
        public double At { get; } = at;
        public List<InkPoint> Points { get; } = new();
    }

    static SignaturePad()
    {
        AutomationProperties.NameProperty.OverrideDefaultValue<SignaturePad>("Signature pad");
    }

    public SignaturePad()
    {
        PseudoClasses.Set(EmptyPseudoClass, true);
    }

    /// <inheritdoc cref="SignerProperty"/>
    public string? Signer { get => GetValue(SignerProperty); set => SetValue(SignerProperty, value); }

    /// <inheritdoc cref="HintProperty"/>
    public string Hint { get => GetValue(HintProperty); set => SetValue(HintProperty, value); }

    /// <summary>The ink for new strokes (default black). Each stroke keeps the ink it was drawn with.</summary>
    public SignatureInkColor InkColor { get => GetValue(InkColorProperty); set => SetValue(InkColorProperty, value); }

    /// <summary>The nib for new strokes (default medium).</summary>
    public SignatureInkWidth InkWidth { get => GetValue(InkWidthProperty); set => SetValue(InkWidthProperty, value); }

    /// <inheritdoc cref="FileNameProperty"/>
    public string FileName { get => GetValue(FileNameProperty); set => SetValue(FileNameProperty, value); }

    /// <summary>The strokes of the signature, in pad units (600 × 260).</summary>
    public IReadOnlyList<InkStroke> Strokes { get => _strokes; private set => SetAndRaise(StrokesProperty, ref _strokes, value); }

    /// <summary>True before the first stroke and after a clear.</summary>
    public bool IsEmpty { get => _isEmpty; private set => SetAndRaise(IsEmptyProperty, ref _isEmpty, value); }

    /// <summary>True when a change can be undone (the last 60 are kept).</summary>
    public bool CanUndo { get => _canUndo; private set => SetAndRaise(CanUndoProperty, ref _canUndo, value); }

    /// <summary>True when an undone change can be redone.</summary>
    public bool CanRedo { get => _canRedo; private set => SetAndRaise(CanRedoProperty, ref _canRedo, value); }

    /// <summary>True while the signature is being replayed.</summary>
    public bool IsReplaying { get => _isReplaying; private set => SetAndRaise(IsReplayingProperty, ref _isReplaying, value); }

    /// <summary>Raised after every change (Arc's <c>onChange</c>).</summary>
    public event EventHandler<SignatureStrokesChangedEventArgs>? StrokesChanged
    {
        add => AddHandler(StrokesChangedEvent, value);
        remove => RemoveHandler(StrokesChangedEvent, value);
    }

    internal SignaturePadCanvas? Pad => _pad;

    internal EditorsLiveRegion? Status => _status;

    internal bool IsWiping => _wiping;

    internal bool IsDrawing => _live is not null;

    internal string AccessibleDescription
    {
        get
        {
            var label = AutomationProperties.GetName(this) ?? "Signature pad";
            var count = _strokes.Count;
            var state = count == 0 ? "Empty." : $"{count} {(count == 1 ? "stroke" : "strokes")}.";
            return $"{label}. {state} Draw with a pointer, pen, or finger.";
        }
    }

    internal string KeyboardHelp => "Press Control or Command Z to undo, add Shift to redo, and Delete to clear.";

    /* ----------------------------------------------------------------------------------------------------------
     * Public API
     * -------------------------------------------------------------------------------------------------------- */

    /// <summary>Undoes the last change.</summary>
    public void Undo()
    {
        if (_past.Count == 0 || _live is not null)
        {
            return;
        }
        SettleWipe();
        StopReplayCore();
        _future.Insert(0, _strokes);
        var previous = _past[^1];
        _past.RemoveAt(_past.Count - 1);
        Present(previous, fresh: null, wiped: false);
        Announce("Undone");
    }

    /// <summary>Redoes the last undone change.</summary>
    public void Redo()
    {
        if (_future.Count == 0 || _live is not null)
        {
            return;
        }
        SettleWipe();
        StopReplayCore();
        _past.Add(_strokes);
        var next = _future[0];
        _future.RemoveAt(0);
        Present(next, fresh: null, wiped: false);
        Announce("Redone");
    }

    /// <summary>Clears the signature with a wipe across the pad; undo brings it back.</summary>
    public void Clear()
    {
        if (_strokes.Count == 0 || _wiping || _live is not null)
        {
            return;
        }
        StopReplayCore();
        if (_pad is null || !ArcMotion.IsFull(this))
        {
            FinishClear();
            return;
        }
        _wiping = true;
        UpdateButtons();
        _wipe ??= CreateWipe();
        _wipe.Jump(0);
        RunWipe();
    }

    /// <summary>Replaces the strokes without a wipe and forgets the history (Arc remounts the component for this).</summary>
    public void Reset(IEnumerable<InkStroke>? strokes = null)
    {
        SettleWipe();
        StopReplayCore();
        _live = null;
        _pad?.Ink.SetLive(null, InkColor);
        _past.Clear();
        _future.Clear();
        Present((strokes ?? Array.Empty<InkStroke>()).ToArray(), fresh: null, wiped: true);
    }

    /// <summary>Replays the signature in its own rhythm (pauses between strokes are capped, the whole replay fits in about 3.2s).</summary>
    public void Replay()
    {
        if (_isReplaying || _strokes.Count == 0 || _live is not null)
        {
            return;
        }
        SettleWipe();
        var offsets = new double[_strokes.Count];
        double cursor = 0;
        for (var i = 0; i < _strokes.Count; i++)
        {
            var stroke = _strokes[i];
            if (i > 0)
            {
                var previous = _strokes[i - 1];
                var gap = stroke.At - (previous.At + LastTime(previous));
                cursor += Math.Min(260, Math.Max(90, gap));
            }
            offsets[i] = cursor;
            cursor += LastTime(stroke);
        }
        var speed = Math.Max(1, cursor / 3200);
        var total = cursor + 120;
        if (_pad is null)
        {
            Announce("Replay finished");
            return;
        }
        IsReplaying = true;
        PseudoClasses.Set(ReplayingPseudoClass, true);
        UpdateButtons();
        Announce("Replaying signature");
        _replayClock ??= CreateReplayClock();
        var ink = _pad.Ink;
        ink.NibSize = SignatureExport.NibSize(InkWidth);
        ink.Replay = (0, offsets);
        _replayClock.Jump(0);
        RunReplay(offsets, total, speed);
    }

    /// <summary>Stops a replay in progress.</summary>
    public void StopReplay()
    {
        if (!_isReplaying)
        {
            return;
        }
        StopReplayCore();
        Announce("Replay stopped");
    }

    /// <summary>The signature as a standalone SVG cropped to the ink (empty without strokes).</summary>
    public string ExportSvg() => SignatureExport.ToSvg(_strokes);

    /// <summary>The signature as PNG bytes cropped to the ink, at <paramref name="scale"/> times pad resolution.</summary>
    /// <exception cref="InvalidOperationException">There are no strokes.</exception>
    public byte[] ExportPng(double scale = 3) => SignatureExport.ToPng(_strokes, scale);

    /// <summary>The signature as a bitmap cropped to the ink, at <paramref name="scale"/> times pad resolution.</summary>
    /// <exception cref="InvalidOperationException">There are no strokes.</exception>
    public Avalonia.Media.Imaging.RenderTargetBitmap ExportBitmap(double scale = 3) => SignatureExport.ToBitmap(_strokes, scale);

    /// <summary>Asks where to save <see cref="FileName"/> as PNG or SVG and writes it; false when cancelled or failed.</summary>
    public async Task<bool> SaveAsync(SignatureFileKind kind)
    {
        if (_strokes.Count == 0)
        {
            return false;
        }
        var extension = kind == SignatureFileKind.Svg ? "svg" : "png";
        var name = $"{FileName}.{extension}";
        var ok = false;
        try
        {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage is { CanSave: true })
            {
                var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    SuggestedFileName = name,
                    DefaultExtension = extension,
                    FileTypeChoices = new[]
                    {
                        kind == SignatureFileKind.Svg
                            ? new FilePickerFileType("SVG image") { Patterns = new[] { "*.svg" }, MimeTypes = new[] { "image/svg+xml" } }
                            : FilePickerFileTypes.ImagePng
                    }
                });
                if (file is null)
                {
                    return false;
                }
                await using var stream = await file.OpenWriteAsync();
                var bytes = kind == SignatureFileKind.Svg ? System.Text.Encoding.UTF8.GetBytes(ExportSvg()) : ExportPng();
                await stream.WriteAsync(bytes);
                ok = true;
            }
        }
        catch (Exception)
        {
            ok = false;
        }
        ShowSaved(ok ? kind : null);
        Announce(ok ? $"Saved {name}" : "Export failed. Try again.");
        return ok;
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Template
     * -------------------------------------------------------------------------------------------------------- */

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_pad is not null)
        {
            _pad.Owner = null;
        }
        _pad = e.NameScope.Find<SignaturePadCanvas>(PadPart);
        _colors = e.NameScope.Find<SignaturePadChoice>(ColorsPart);
        _widths = e.NameScope.Find<SignaturePadChoice>(WidthsPart);
        _undo = e.NameScope.Find<SignaturePadIconButton>(UndoPart);
        _redo = e.NameScope.Find<SignaturePadIconButton>(RedoPart);
        _replay = e.NameScope.Find<SignaturePadIconButton>(ReplayPart);
        _clear = e.NameScope.Find<SignaturePadIconButton>(ClearPart);
        _png = e.NameScope.Find<Button>(PngPart);
        _svg = e.NameScope.Find<Button>(SvgPart);
        _status = e.NameScope.Find<EditorsLiveRegion>(StatusPart);
        if (_pad is not null)
        {
            _pad.Owner = this;
            _pad.SetTexts(Signer, Hint);
            _pad.Ink.SetStrokes(_strokes, null, wiped: true);
            _pad.SetHintShown(_strokes.Count == 0, animate: false);
        }
        if (_colors is not null)
        {
            _colors.SetOptions(new[] { SignatureInkColor.Black, SignatureInkColor.Blue, SignatureInkColor.Violet }
                .Select(color => ((Control)Swatch(color), InkLabel(color))));
            _colors.Select((int)InkColor, animate: false);
            _colors.Chosen += index => InkColor = (SignatureInkColor)index;
        }
        if (_widths is not null)
        {
            _widths.SetOptions(new[] { SignatureInkWidth.Fine, SignatureInkWidth.Medium, SignatureInkWidth.Bold }
                .Select(width =>
                {
                    var size = SignatureExport.NibSize(width) + 2;
                    var dot = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size / 2) };
                    if (_pad is not null)
                    {
                        dot.Bind(Border.BackgroundProperty, _pad.GetObservable(SignaturePadCanvas.InkBlackProperty));
                    }
                    return ((Control)dot, WidthLabel(width));
                }));
            _widths.Select((int)InkWidth, animate: false);
            _widths.Chosen += index => InkWidth = (SignatureInkWidth)index;
        }
        if (_undo is not null)
        {
            _undo.Click += (_, _) => Undo();
        }
        if (_redo is not null)
        {
            _redo.Click += (_, _) => Redo();
        }
        if (_replay is not null)
        {
            _replayIcon = new ArcContentSwap { Mode = ContentSwapMode.Pop, Width = 18, Height = 18, Content = ReplayGlyph(false) };
            _replay.Content = _replayIcon;
            _replay.Click += (_, _) =>
            {
                if (_isReplaying)
                {
                    StopReplay();
                }
                else
                {
                    Replay();
                }
            };
        }
        if (_clear is not null)
        {
            _clear.Click += (_, _) => Clear();
        }
        if (_png is not null)
        {
            _pngContent = new ArcContentSwap { Mode = ContentSwapMode.BlurSwap, Content = ExportLabel("PNG", false) };
            _png.Content = _pngContent;
            AutomationProperties.SetName(_png, "Download PNG");
            _png.Click += async (_, _) => await SaveAsync(SignatureFileKind.Png);
        }
        if (_svg is not null)
        {
            _svgContent = new ArcContentSwap { Mode = ContentSwapMode.BlurSwap, Content = ExportLabel("SVG", false) };
            _svg.Content = _svgContent;
            AutomationProperties.SetName(_svg, "Download SVG");
            _svg.Click += async (_, _) => await SaveAsync(SignatureFileKind.Svg);
        }
        UpdateButtons();
    }

    /// <summary>A 16px swatch in the screen ink (it follows the theme with the pad's ink brushes).</summary>
    private Border Swatch(SignatureInkColor color)
    {
        var swatch = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(8) };
        if (_pad is not null)
        {
            var property = color switch
            {
                SignatureInkColor.Blue => SignaturePadCanvas.InkBlueProperty,
                SignatureInkColor.Violet => SignaturePadCanvas.InkVioletProperty,
                _ => SignaturePadCanvas.InkBlackProperty
            };
            swatch.Bind(Border.BackgroundProperty, _pad.GetObservable(property));
        }
        return swatch;
    }

    private static string InkLabel(SignatureInkColor color) => color switch
    {
        SignatureInkColor.Blue => "Blue ink",
        SignatureInkColor.Violet => "Violet ink",
        _ => "Black ink"
    };

    private static string WidthLabel(SignatureInkWidth width) => width switch
    {
        SignatureInkWidth.Fine => "Fine",
        SignatureInkWidth.Bold => "Bold",
        _ => "Medium"
    };

    private static Control ReplayGlyph(bool replaying) => replaying
        ? new ArcGlyph { Data = ArcIcons.Square, Size = 15, StrokeThickness = 1.75 }
        : new ArcGlyph { Data = ArcIcons.Play, Size = 17, StrokeThickness = 1.75 };

    private static Control ExportLabel(string kind, bool saved) => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Spacing = 6,
        HorizontalAlignment = HorizontalAlignment.Center,
        Children =
        {
            new ArcGlyph { Data = saved ? ArcIcons.Check : ArcIcons.Download, Size = 15, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center },
            new TextBlock { Text = saved ? "Saved" : kind, VerticalAlignment = VerticalAlignment.Center }
        }
    };

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SignerProperty || change.Property == HintProperty)
        {
            _pad?.SetTexts(Signer, Hint);
        }
        else if (change.Property == InkColorProperty)
        {
            _colors?.Select((int)InkColor, animate: true);
            if (_pad is not null)
            {
                Announce($"{InkLabel(InkColor)} selected");
            }
        }
        else if (change.Property == InkWidthProperty)
        {
            _widths?.Select((int)InkWidth, animate: true);
            if (_pad is not null)
            {
                Announce($"{WidthLabel(InkWidth)} nib selected");
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _savedTimer?.Stop();
        StopReplayCore();
        SettleWipe();
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Drawing
     * -------------------------------------------------------------------------------------------------------- */

    internal void OnPadPointerPressed(PointerPressedEventArgs e)
    {
        var pad = _pad;
        if (pad is null || _live is not null)
        {
            return;
        }
        var point = e.GetCurrentPoint(pad);
        if (e.Pointer.Type == PointerType.Mouse && !point.Properties.IsLeftButtonPressed)
        {
            return;
        }
        e.Handled = true;
        // Touch and pen draw instead of scrolling the page (Arc's touch-action: none).
        e.PreventGestureRecognition();
        SettleWipe();
        if (_isReplaying)
        {
            StopReplayCore();
        }
        pad.Focus(NavigationMethod.Pointer);
        e.Pointer.Capture(pad);
        _counter++;
        var at = pad.ToPad(point.Position);
        var live = new LiveStroke(e.Pointer, e.Timestamp, $"{_uid}-{_counter}", InkColor, InkWidth, e.Timestamp);
        live.Points.Add(new InkPoint(at.X, at.Y, Pressure(e.Pointer, point), 0));
        _live = live;
        pad.SetDrawing(true);
        pad.SetHintShown(false, animate: true);
        PaintLive();
    }

    private static double Pressure(IPointer pointer, PointerPoint point) =>
        pointer.Type == PointerType.Pen && point.Properties.Pressure > 0 ? point.Properties.Pressure : -1;

    internal void OnPadPointerMoved(PointerEventArgs e)
    {
        var live = _live;
        var pad = _pad;
        if (live is null || pad is null || !ReferenceEquals(live.Pointer, e.Pointer))
        {
            return;
        }
        e.PreventGestureRecognition();
        // Coalesced samples between frames (Arc's getCoalescedEvents); they share the event's time, spread evenly.
        var samples = e.GetIntermediatePoints(pad);
        var lastTime = live.Points[^1].Time;
        var now = Math.Max(lastTime, (double)(e.Timestamp - live.Stamp));
        for (var i = 0; i < samples.Count; i++)
        {
            var sample = samples[i];
            var at = pad.ToPad(sample.Position);
            var time = samples.Count > 1 ? lastTime + (now - lastTime) * (i + 1) / samples.Count : now;
            live.Points.Add(new InkPoint(at.X, at.Y, Pressure(e.Pointer, sample), Math.Max(live.Points[^1].Time, time)));
        }
        PaintLive();
    }

    internal void OnPadPointerReleased(IPointer pointer)
    {
        var live = _live;
        if (live is null || !ReferenceEquals(live.Pointer, pointer))
        {
            return;
        }
        _live = null;
        if (ReferenceEquals(pointer.Captured, _pad))
        {
            pointer.Capture(null);
        }
        _pad?.SetDrawing(false);
        var stroke = new InkStroke(live.Id, live.Points.ToArray(), live.Color, live.Width, live.At);
        Commit(_strokes.Append(stroke).ToArray(), stroke);
        _pad?.Ink.SetLive(null, live.Color);
        Announce($"Stroke added. {_strokes.Count} in the signature.");
    }

    private void PaintLive()
    {
        if (_live is not { } live || _pad is null)
        {
            return;
        }
        var outline = SignatureExport.GetStrokeOutline(live.Points, SignatureExport.OptionsFor(live.Width, live.Points, last: false));
        _pad.Ink.SetLive(SignatureExport.ToGeometry(outline), live.Color);
    }

    private void Commit(IReadOnlyList<InkStroke> next, InkStroke? fresh)
    {
        _past.Add(_strokes);
        if (_past.Count > HistoryLimit)
        {
            _past.RemoveAt(0);
        }
        _future.Clear();
        Present(next, fresh, wiped: false);
    }

    private void Present(IReadOnlyList<InkStroke> strokes, InkStroke? fresh, bool wiped)
    {
        Strokes = strokes;
        IsEmpty = strokes.Count == 0;
        PseudoClasses.Set(EmptyPseudoClass, IsEmpty);
        CanUndo = _past.Count > 0;
        CanRedo = _future.Count > 0;
        _pad?.Ink.SetStrokes(strokes, fresh, wiped);
        _pad?.SetHintShown(IsEmpty && _live is null && !_wiping, animate: true);
        UpdateButtons();
        RaiseEvent(new SignatureStrokesChangedEventArgs(StrokesChangedEvent, strokes));
        _pad?.InvalidateVisual();
    }

    internal void OnPadKeyDown(KeyEventArgs e)
    {
        var mod = EditorsHotkeys.IsMod(e.KeyModifiers);
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        if (mod && e.Key == Key.Z)
        {
            if (shift)
            {
                Redo();
            }
            else
            {
                Undo();
            }
            e.Handled = true;
        }
        else if (mod && e.Key == Key.Y)
        {
            Redo();
            e.Handled = true;
        }
        else if (!mod && e.Key is Key.Delete or Key.Back)
        {
            Clear();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _isReplaying)
        {
            StopReplay();
            e.Handled = true;
        }
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Wipe and replay
     * -------------------------------------------------------------------------------------------------------- */

    private MotionValue<double> CreateWipe()
    {
        var wipe = new MotionValue<double>(this, 0, MotionVectors.Double);
        wipe.Changed += value =>
        {
            if (_pad is not null && _wiping)
            {
                _pad.Ink.Wipe = value;
                _pad.Ink.InvalidateVisual();
            }
        };
        return wipe;
    }

    /// <summary>The ink is cut away behind a squeegee edge that sweeps across the pad in 0.56s (code driven: the clip and the edge follow one timeline).</summary>
    private async void RunWipe()
    {
        var finished = await _wipe!.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(560), ArcEasings.InOut));
        if (_wiping && finished)
        {
            FinishClear();
        }
    }

    /// <summary>A wipe in flight completes at once, so whatever comes next starts from a clear pad.</summary>
    private void SettleWipe()
    {
        if (!_wiping)
        {
            return;
        }
        _wipe?.Jump(1);
        FinishClear();
    }

    private void FinishClear()
    {
        _wiping = false;
        if (_pad is not null)
        {
            _pad.Ink.Wipe = null;
        }
        if (_strokes.Count == 0)
        {
            UpdateButtons();
            return;
        }
        Commit(Array.Empty<InkStroke>(), null);
        _pad?.Ink.SetStrokes(Array.Empty<InkStroke>(), null, wiped: true);
        Announce("Signature cleared. Undo brings it back.");
    }

    private static double LastTime(InkStroke stroke) => stroke.Points.Count > 0 ? stroke.Points[^1].Time : 0;

    private MotionValue<double> CreateReplayClock()
    {
        var clock = new MotionValue<double>(this, 0, MotionVectors.Double);
        clock.Changed += value =>
        {
            if (_pad is not null && _isReplaying && _pad.Ink.Replay is { } replay)
            {
                _pad.Ink.Replay = (value, replay.Offsets);
                _pad.Ink.InvalidateVisual();
            }
        };
        return clock;
    }

    private async void RunReplay(double[] offsets, double total, double speed)
    {
        var duration = TimeSpan.FromMilliseconds(total / speed);
        var finished = await _replayClock!.AnimateTo(total, MotionSpec.Tween(duration, new Avalonia.Animation.Easings.LinearEasing()));
        if (finished && _isReplaying)
        {
            StopReplayCore();
            Announce("Replay finished");
        }
    }

    private void StopReplayCore()
    {
        if (!_isReplaying)
        {
            return;
        }
        IsReplaying = false;
        PseudoClasses.Set(ReplayingPseudoClass, false);
        _replayClock?.Stop();
        if (_pad is not null)
        {
            _pad.Ink.Replay = null;
            _pad.Ink.InvalidateVisual();
        }
        UpdateButtons();
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Chrome
     * -------------------------------------------------------------------------------------------------------- */

    private void UpdateButtons()
    {
        var empty = _strokes.Count == 0;
        if (_undo is not null)
        {
            _undo.IsEnabled = _past.Count > 0 && !_wiping;
        }
        if (_redo is not null)
        {
            _redo.IsEnabled = _future.Count > 0 && !_wiping;
        }
        if (_clear is not null)
        {
            _clear.IsEnabled = !empty && !_wiping;
        }
        if (_replay is not null)
        {
            _replay.IsEnabled = !empty && !_wiping;
            _replay.IsChecked = _isReplaying;
            var label = _isReplaying ? "Stop replay" : "Replay signature";
            AutomationProperties.SetName(_replay, label);
            Tooltip.SetTip(_replay, label);
            if (_replayIcon is not null && (_replayIcon.Content as ArcGlyph)?.Data != (_isReplaying ? ArcIcons.Square : ArcIcons.Play))
            {
                _replayIcon.Content = ReplayGlyph(_isReplaying);
            }
        }
        if (_png is not null)
        {
            _png.IsEnabled = !empty;
        }
        if (_svg is not null)
        {
            _svg.IsEnabled = !empty;
        }
    }

    private void ShowSaved(SignatureFileKind? kind)
    {
        if (_pngContent is not null)
        {
            _pngContent.Content = ExportLabel("PNG", kind == SignatureFileKind.Png);
        }
        if (_svgContent is not null)
        {
            _svgContent.Content = ExportLabel("SVG", kind == SignatureFileKind.Svg);
        }
        _savedTimer?.Stop();
        if (kind is null)
        {
            return;
        }
        _savedTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1800) };
        _savedTimer.Tick -= OnSavedElapsed;
        _savedTimer.Tick += OnSavedElapsed;
        _savedTimer.Start();
    }

    private void OnSavedElapsed(object? sender, EventArgs e)
    {
        _savedTimer?.Stop();
        ShowSaved(null);
    }

    private void Announce(string message) => _status?.Announce(message);
}
