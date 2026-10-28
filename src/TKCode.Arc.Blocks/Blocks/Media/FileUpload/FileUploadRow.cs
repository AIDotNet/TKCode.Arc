// Ported from Arc's free file-upload block (MIT, Copyright (c) 2026 Elia Kuratli): file-upload.tsx (FileRow).
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One file in the list: its icon, name, size and upload phase, the validation error, the progress bar, the status slot
/// (check or Retry) and Remove. One smooth spring drives the bar and the counted percentage, so both always agree; a
/// finished upload keeps its bar until the count reads 100%, then the phase turns to "Uploaded", the bar folds away after
/// 240ms and the check pops in and draws. Everything shown when the row is created is at rest (Arc's <c>initial={false}</c>).
/// </summary>
[TemplatePart(FileIconPart, typeof(ArcGlyph))]
[TemplatePart(NamePart, typeof(TextBlock))]
[TemplatePart(SizePart, typeof(TextBlock))]
[TemplatePart(DotPart, typeof(TextBlock))]
[TemplatePart(PhasePart, typeof(FileUploadSwap))]
[TemplatePart(ErrorPart, typeof(TextBlock))]
[TemplatePart(BarFramePart, typeof(FileUploadCollapse))]
[TemplatePart(BarFillPart, typeof(Border))]
[TemplatePart(StatusSlotPart, typeof(Panel))]
[TemplatePart(RemovePart, typeof(FileUploadIconButton))]
internal sealed class FileUploadRow : TemplatedControl
{
    public const string FileIconPart = "PART_FileIcon";
    public const string NamePart = "PART_Name";
    public const string SizePart = "PART_Size";
    public const string DotPart = "PART_Dot";
    public const string PhasePart = "PART_Phase";
    public const string ErrorPart = "PART_Error";
    public const string BarFramePart = "PART_BarFrame";
    public const string BarFillPart = "PART_BarFill";
    public const string StatusSlotPart = "PART_StatusSlot";
    public const string RemovePart = "PART_Remove";

    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    private readonly FileUpload _owner;
    private readonly TranslateTransform _fillShift = new();
    private ArcGlyph? _icon;
    private TextBlock? _name;
    private TextBlock? _size;
    private TextBlock? _dot;
    private FileUploadSwap? _phase;
    private TextBlock? _error;
    private FileUploadCollapse? _barFrame;
    private Border? _barFill;
    private Panel? _slot;
    private FileUploadIconButton? _remove;
    private TextBlock? _percent;
    private FileUploadSlotItem? _slotItem;
    private string? _slotKey;
    private MotionValue<double>? _progress;
    private double _progressValue;
    private bool _filled;
    private FileUploadStatus _seen;
    private FileUploadStatus? _shownPhase;
    private (FileUploadStatus Status, double Target)? _progressGoal;
    private bool _ready;

    public FileUploadRow(FileUpload owner, FileUploadItem item)
    {
        _owner = owner;
        Item = item;
        _seen = item.Status;
        // A finished upload keeps its bar only while it is still filling; a row created after the fact starts filled.
        _filled = item.Status != FileUploadStatus.Uploading;
        _progressValue = item.Status == FileUploadStatus.Done ? 100 : item.Progress;
        item.PropertyChanged += OnItemPropertyChanged;
    }

    public FileUploadItem Item { get; }

    /// <summary>The Remove button (focus moves here after a neighbor is removed).</summary>
    public FileUploadIconButton? RemoveButton => _remove;

    /// <summary>The phase the row shows: <see cref="FileUploadStatus.Uploading"/> until a finished upload's count reads 100%.</summary>
    public FileUploadStatus ShownStatus => ComputeShown();

    /// <summary>The counted percentage as displayed.</summary>
    public string PercentText => FormatPercent(_progressValue);

    /// <summary>"ready", "retry" or null: what the status slot holds.</summary>
    internal string? SlotKey => _slotKey;

    internal FileUploadCollapse? BarFrame => _barFrame;

    internal void Detach() => Item.PropertyChanged -= OnItemPropertyChanged;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_remove is not null)
        {
            _remove.Click -= OnRemoveClick;
        }
        _icon = e.NameScope.Find<ArcGlyph>(FileIconPart);
        _name = e.NameScope.Find<TextBlock>(NamePart);
        _size = e.NameScope.Find<TextBlock>(SizePart);
        _dot = e.NameScope.Find<TextBlock>(DotPart);
        _phase = e.NameScope.Find<FileUploadSwap>(PhasePart);
        _error = e.NameScope.Find<TextBlock>(ErrorPart);
        _barFrame = e.NameScope.Find<FileUploadCollapse>(BarFramePart);
        _barFill = e.NameScope.Find<Border>(BarFillPart);
        _slot = e.NameScope.Find<Panel>(StatusSlotPart);
        _remove = e.NameScope.Find<FileUploadIconButton>(RemovePart);
        if (_barFill is not null)
        {
            _barFill.RenderTransform = _fillShift;
            _barFill.SizeChanged += (_, _) => UpdateFill();
        }
        if (_remove is not null)
        {
            _remove.Click += OnRemoveClick;
            AutomationProperties.SetName(_remove, $"Remove {Item.Name}");
        }
        if (_icon is not null)
        {
            _icon.Data = FileUploadTypes.IconOf(Item.Name);
        }
        if (_name is not null)
        {
            _name.Text = Item.Name;
            ToolTip.SetTip(_name, Item.Name);
        }
        if (_size is not null)
        {
            _size.Text = FileUploadTypes.FormatSize(Item.Size);
            _size.FontFeatures = s_tabular;
        }
        _slot?.Children.Clear();
        _slotItem = null;
        _slotKey = null;
        _shownPhase = null;
        _ready = false;
        Apply(animate: false);
        _ready = true;
        UpdateFill();
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e) => _owner.Remove(Item);

    private void OnRetryClick(object? sender, RoutedEventArgs e) => _owner.Retry(Item);

    private void OnItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == FileUploadItem.StatusProperty || e.Property == FileUploadItem.ProgressProperty
            || e.Property == FileUploadItem.ErrorProperty)
        {
            if (Item.Status != _seen)
            {
                _seen = Item.Status;
                if (Item.Status == FileUploadStatus.Uploading)
                {
                    _filled = false;
                }
            }
            Apply(animate: _ready);
        }
    }

    private bool Reduce => ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full;

    private FileUploadStatus ComputeShown()
    {
        var status = Item.Status;
        return status == FileUploadStatus.Done && !_filled && !Reduce ? FileUploadStatus.Uploading : status;
    }

    private void Apply(bool animate)
    {
        DriveProgress(animate);
        var shown = ComputeShown();
        var hasStatus = shown != FileUploadStatus.None;
        if (_dot is not null)
        {
            _dot.IsVisible = hasStatus;
        }
        if (_phase is not null)
        {
            _phase.IsVisible = hasStatus;
            if (hasStatus && _shownPhase != shown)
            {
                _phase.Content = CreatePhase(shown);
            }
        }
        _shownPhase = hasStatus ? shown : null;
        if (_error is not null)
        {
            _error.Text = Item.Error;
            _error.IsVisible = !string.IsNullOrEmpty(Item.Error);
        }
        if (_barFrame is not null)
        {
            _barFrame.IsOpen = shown == FileUploadStatus.Uploading;
        }
        var phase = !string.IsNullOrEmpty(Item.Error) ? "invalid"
            : shown switch
            {
                FileUploadStatus.None => "ready",
                FileUploadStatus.Done => "done",
                FileUploadStatus.Failed => "failed",
                _ => "uploading"
            };
        var key = phase is "ready" or "done" ? "ready" : phase == "failed" ? "retry" : null;
        SetSlot(key, phase, animate);
    }

    private Control CreatePhase(FileUploadStatus shown)
    {
        if (shown == FileUploadStatus.Uploading)
        {
            _percent = new TextBlock
            {
                Text = FormatPercent(_progressValue),
                FontFeatures = s_tabular,
                MinWidth = PercentMinWidth(),
                LineHeight = 16.8
            };
            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { new TextBlock { Text = "Uploading ", LineHeight = 16.8 }, _percent }
            };
        }
        var text = new TextBlock { Text = shown == FileUploadStatus.Done ? "Uploaded" : "Upload failed", LineHeight = 16.8 };
        if (shown == FileUploadStatus.Failed)
        {
            ArcTokens.Bind(text, TextBlock.ForegroundProperty, ArcDesignTokenKind.Danger);
        }
        return text;
    }

    private double PercentMinWidth()
    {
        // CSS min-width: 4ch, the width of four zeros in the meta line's font.
        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight.Normal);
        var zero = new FormattedText("0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 12, null);
        return Math.Ceiling(zero.WidthIncludingTrailingWhitespace * 4);
    }

    private void SetSlot(string? key, string phase, bool animate)
    {
        if (_slot is null)
        {
            return;
        }
        if (_slotItem is not null)
        {
            AutomationProperties.SetName(_slotItem, phase == "done" ? "Uploaded" : phase == "ready" ? "Ready" : null);
        }
        if (key == _slotKey)
        {
            return;
        }
        if (_slotItem is { } old)
        {
            var slot = _slot;
            old.Leave(() => slot.Children.Remove(old));
        }
        _slotKey = key;
        _slotItem = null;
        if (key is null)
        {
            return;
        }
        var item = key == "ready" ? CreateCheck(phase, animate) : CreateRetry();
        _slotItem = item;
        _slot.Children.Add(item);
        item.Enter(animate);
    }

    private FileUploadSlotItem CreateCheck(string phase, bool animate)
    {
        var glyph = new StrokeDrawGlyph
        {
            Data = s_check,
            Size = 14,
            StrokeThickness = 2,
            Progress = animate && !Reduce ? 0 : 1
        };
        ArcTokens.Bind(glyph, StrokeDrawGlyph.ForegroundProperty, ArcDesignTokenKind.Success);
        var item = new FileUploadSlotItem
        {
            Width = 22,
            Height = 22,
            EnterScale = 0.8,
            ExitScale = 1,
            ExitDuration = ArcMotionPresets.Instant,
            Child = glyph
        };
        AutomationProperties.SetName(item, phase == "done" ? "Uploaded" : "Ready");
        if (animate && !Reduce)
        {
            glyph.AttachedToVisualTree += (_, _) => _ = glyph.Play(ArcMotionPresets.Standard, ArcEasings.Enter, TimeSpan.FromMilliseconds(80));
        }
        return item;
    }

    private FileUploadSlotItem CreateRetry()
    {
        var button = new FileUploadIconButton
        {
            Glyph = ArcIcons.RotateCw,
            GlyphSize = 14,
            GlyphStroke = 1.8
        };
        button.Classes.Add("retry");
        AutomationProperties.SetName(button, $"Retry {Item.Name}");
        button.Click += OnRetryClick;
        return new FileUploadSlotItem
        {
            EnterScale = 0.6,
            EnterBlur = 2,
            ExitScale = 0.6,
            ExitBlur = 2,
            ExitDuration = ArcMotionPresets.Fast,
            Child = button
        };
    }

    // Arc draws its own check, not Lucide's: M4 12.5l5 5L20 6.5.
    private static readonly Geometry s_check = StreamGeometry.Parse("M4 12.5l5 5L20 6.5");

    private void DriveProgress(bool animate)
    {
        var status = Item.Status;
        var target = status == FileUploadStatus.Done ? 100 : Item.Progress;
        if (_progressGoal is { } goal && goal.Status == status && goal.Target == target)
        {
            return;
        }
        _progressGoal = (status, target);
        _progress ??= CreateProgress();
        // Every upload starts from an empty bar, even a retry that failed halfway.
        if (!animate || Reduce || (status == FileUploadStatus.Uploading && target == 0))
        {
            _progress.Jump(target);
            SetProgress(target);
            return;
        }
        var done = status == FileUploadStatus.Done;
        _ = Run();

        async Task Run()
        {
            var reached = await _progress.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
            if (reached && done && !_filled)
            {
                _filled = true;
                Apply(animate: true);
            }
        }
    }

    private MotionValue<double> CreateProgress()
    {
        var value = new MotionValue<double>(this, _progressValue, MotionVectors.Double);
        value.Changed += v =>
        {
            SetProgress(v);
            // The label turns as soon as the count reads 100%, without waiting out the spring's last fraction of a pixel.
            if (Item.Status == FileUploadStatus.Done && v >= 99.5 && !_filled)
            {
                _filled = true;
                Apply(animate: true);
            }
        };
        return value;
    }

    private void SetProgress(double value)
    {
        _progressValue = value;
        if (_percent is not null)
        {
            _percent.Text = FormatPercent(value);
        }
        UpdateFill();
    }

    private void UpdateFill()
    {
        if (_barFill is null)
        {
            return;
        }
        // The fill slides in from the left instead of scaling, so its rounded end keeps its shape at every value.
        var clamped = Math.Clamp(_progressValue, 0, 100);
        _fillShift.X = (clamped - 100) / 100 * _barFill.Bounds.Width;
    }

    private static string FormatPercent(double value) =>
        $"{Math.Round(Math.Clamp(value, 0, 100), MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}%";
}
