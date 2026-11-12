using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Platform.Storage;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The upload step: the drop zone (or the file card once a file is read), an error line under it when the file could
/// not be used, and the footer ("Try a sample file" and the primary "Match fields"). The drop zone and the file card swap
/// in place: the zone leaves on the quick spring (fade, scale 0.98) while the card rises 8 on the smooth spring; removing
/// the file plays it back the other way (the card rises out 8). The card shows "Reading file" with a 2px line for 420ms
/// before the facts and the preview; the five preview rows rise 6 one after another, 40ms apart.
/// </summary>
internal sealed class ImportWizardUploadView : ImportWizardStepView
{
    private readonly ImportWizardSwap _swap;
    private readonly TextBlock _error;
    private readonly ImportWizardFooter _footer;
    private readonly ImportWizardButton _next;
    private ImportWizardButton? _sample;
    private ImportWizardDropZone? _zone;
    private ImportWizardFileCard? _card;
    private readonly TextBlock _subtitle;
    private MotionValue<double>? _errorFade;

    public ImportWizardUploadView(ImportWizard owner)
        : base(owner)
    {
        _subtitle = ImportWizardUi.Subtitle(null);
        SetHead(ImportWizardUi.Heading(Labels.UploadTitle, _subtitle));

        _swap = new ImportWizardSwap();
        _error = ImportWizardUi.Text(null, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Danger, lineHeight: 19.6);
        _error.TextWrapping = TextWrapping.Wrap;
        _error.IsVisible = false;
        var body = new StackPanel();
        ImportWizardUi.Bind(body, StackPanel.SpacingProperty, ImportWizardTokenKind.StepSpacing);
        body.Children.Add(_swap);
        body.Children.Add(_error);
        SetBody(body);

        _next = new ImportWizardButton(true, Labels.ToMapping, iconEnd: ArcIcons.ArrowRight);
        _next.Click += (_, e) =>
        {
            e.Handled = true;
            if (Session.HasFile)
            {
                Owner.GoTo(ImportWizardStep.Map);
            }
        };
        _footer = new ImportWizardFooter(_next) { IsCompact = IsCompact };
        SetFooter(_footer);

        Refresh();
        Apply(animate: false);
    }

    public override ImportWizardStep Step => ImportWizardStep.Upload;

    /// <summary>The drop zone while it is shown (tests).</summary>
    internal ImportWizardDropZone? DropZone => _zone;

    /// <summary>The file card while it is shown (tests).</summary>
    internal ImportWizardFileCard? FileCard => _card;

    /// <summary>The primary button (tests).</summary>
    internal ImportWizardButton NextButton => _next;

    /// <summary>The sample button, or null (tests).</summary>
    internal ImportWizardButton? SampleButton => _sample;

    /// <summary>The error line (tests).</summary>
    internal TextBlock ErrorLine => _error;

    public override void Refresh()
    {
        _subtitle.Text = DeveloperText.Format(Labels.UploadHint, ImportWizardUi.Number(Owner.MaxRows));
        _next.Text = Labels.ToMapping;
        _zone?.Refresh(Labels);
        UpdateSample();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Owner.UploadChanged += OnUploadChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Owner.UploadChanged -= OnUploadChanged;
    }

    private void OnUploadChanged() => Apply(animate: IsMotionAllowed);

    protected override void OnCompactChanged(bool compact) => _footer.IsCompact = compact;

    private bool IsMotionAllowed => Owner.IsLoaded && ArcMotion.GetEffectiveLevel(Owner) != ArcMotionLevel.Off;

    private void Apply(bool animate)
    {
        var phase = Owner.UploadPhase;
        var hasCard = phase is ImportUploadPhase.Reading or ImportUploadPhase.Loaded;
        if (hasCard)
        {
            if (_card is null)
            {
                _card = new ImportWizardFileCard(this);
                _swap.Show(_card,
                    exit: new ImportSwapMotion(0.98, 0, 0, ImportWizardMotion.Quick),
                    enter: new ImportSwapMotion(1, 8, 0, ImportWizardMotion.Smooth),
                    animate);
                _zone = null;
            }
            _card.Update(animate);
        }
        else if (_zone is null || _swap.Current != _zone)
        {
            _zone = new ImportWizardDropZone(this);
            _zone.Refresh(Labels);
            _swap.Show(_zone,
                exit: new ImportSwapMotion(1, -8, 0, ImportWizardMotion.Smooth),
                enter: new ImportSwapMotion(0.98, 0, 0, ImportWizardMotion.Smooth),
                animate && _card is not null);
            _card = null;
        }
        var error = phase == ImportUploadPhase.Error ? Owner.UploadError : null;
        SetError(error, animate);
        _next.IsEnabled = phase == ImportUploadPhase.Loaded;
        UpdateSample();
    }

    private void SetError(string? error, bool animate)
    {
        var show = !string.IsNullOrEmpty(error);
        _error.Text = error;
        if (show && !_error.IsVisible)
        {
            _error.IsVisible = true;
            AutomationProperties.SetLiveSetting(_error, AutomationLiveSetting.Assertive);
            if (animate && ArcMotion.GetEffectiveLevel(Owner) != ArcMotionLevel.Off)
            {
                _errorFade ??= CreateErrorFade();
                _errorFade.Jump(0);
                _ = _errorFade.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(200), ImportWizardMotion.EaseOut));
            }
            else
            {
                _error.Opacity = 1;
            }
        }
        else if (!show)
        {
            _error.IsVisible = false;
        }
    }

    private MotionValue<double> CreateErrorFade()
    {
        var fade = new MotionValue<double>(_error, 1, MotionVectors.Double);
        fade.Changed += v => _error.Opacity = v;
        return fade;
    }

    private void UpdateSample()
    {
        var show = Owner.SampleCsv is not null && Owner.UploadPhase is ImportUploadPhase.Empty or ImportUploadPhase.Error;
        if (show && _sample is null)
        {
            _sample = new ImportWizardButton(false, Labels.UseSample, iconStart: ArcIcons.FileSpreadsheet);
            _sample.Click += (_, e) =>
            {
                e.Handled = true;
                Owner.LoadSample();
            };
            _footer.Left = _sample;
        }
        else if (!show && _sample is not null)
        {
            // Arc removes it at once, with no exit.
            if (_sample.IsFocused)
            {
                _next.Focus(NavigationMethod.Tab);
            }
            _footer.Left = null;
            _sample = null;
        }
        if (_sample is not null)
        {
            _sample.Text = Labels.UseSample;
        }
    }

    /// <summary>The drop zone was activated (click, Enter, Space).</summary>
    internal void OpenPicker() => _ = Owner.OpenPickerAsync();

    /// <summary>Files were dropped on the zone.</summary>
    internal void Drop(IReadOnlyList<IStorageItem> items)
    {
        foreach (var item in items)
        {
            if (item is IStorageFile file)
            {
                _ = Owner.LoadFileAsync(file);
                return;
            }
        }
    }

    /// <summary>The remove button of the file card.</summary>
    internal void RemoveFile()
    {
        Owner.RemoveFile();
        // Keep the keyboard on the card: the drop zone takes the focus the remove button had.
        _zone?.Focus(NavigationMethod.Tab);
    }
}

/// <summary>
/// The drop zone: 180 tall, 1px dashed BorderStrong, radius 22, SurfaceMuted, the upload icon, "Drag a CSV here" and
/// "or browse files". Hover turns the dashes TextMuted (160ms). A dragged file turns the edge solid Accent and the fill
/// Accent 13% (10% light), grows the zone to 1.01 and lifts the icon 4px in the accent on the smooth spring, and the title
/// reads "Release to upload". Click, Enter or Space opens the file picker; keyboard focus shows a 2px ring 2 outside
/// (Arc shows none). The fill and edge are drawn here (Avalonia borders cannot dash); the content comes from the
/// ControlTheme's presenter.
/// </summary>
[PseudoClasses(DraggingPseudoClass)]
internal sealed class ImportWizardDropZone : AvaloniaButton
{
    public const string DraggingPseudoClass = ":dragging";

    private readonly ImportWizardUploadView _view;
    private readonly ArcGlyph _icon;
    private readonly TextBlock _title;
    private readonly TextBlock _or;
    private readonly TextBlock _browse;
    private readonly TranslateTransform _lift = new();
    private ImportWizardLabels _labels = new();
    private MotionValue<double>? _drag;
    private MotionValue<double>? _hover;
    private double _dragValue;
    private double _hoverValue;
    private bool _dragging;

    static ImportWizardDropZone()
    {
        AffectsRender<ImportWizardDropZone>(IsPointerOverProperty);
        FocusableProperty.OverrideDefaultValue<ImportWizardDropZone>(true);
    }

    public ImportWizardDropZone(ImportWizardUploadView view)
    {
        _view = view;
        ClipToBounds = false;
        ImportWizardUi.Bind(this, HeightProperty, ImportWizardTokenKind.DropZoneHeight);
        RenderTransformOrigin = RelativePoint.Center;
        _icon = ImportWizardUi.Glyph(ArcIcons.Upload, 20, ArcDesignTokenKind.TextSecondary);
        _icon.HorizontalAlignment = HorizontalAlignment.Center;
        _icon.Margin = new Thickness(0, 0, 0, 8);
        _icon.RenderTransform = _lift;
        _title = ImportWizardUi.Text(null, ArcDesignTokenKind.TypeBase, ArcDesignTokenKind.Foreground, FontWeight.Medium, 22.4);
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        _or = ImportWizardUi.Text(null, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.TextSecondary, lineHeight: 19.6);
        _browse = ImportWizardUi.Text(null, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground, lineHeight: 18.6);
        // The link's 1px underline 3 below the baseline, in BorderStrong.
        var underline = new Border { Child = _browse, VerticalAlignment = VerticalAlignment.Top };
        ArcTokens.Bind(underline, Border.BorderBrushProperty, ArcDesignTokenKind.BorderStrong);
        ArcTokens.Bind(underline, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessBottom);
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
        line.Children.Add(_or);
        line.Children.Add(underline);
        var content = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        content.Children.Add(_icon);
        content.Children.Add(_title);
        content.Children.Add(line);
        Content = content;

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
        Click += (_, e) =>
        {
            e.Handled = true;
            _view.OpenPicker();
        };
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    /// <summary>True while a file is dragged over the zone (tests).</summary>
    public bool IsDragging => _dragging;

    /// <summary>The title text (tests).</summary>
    public string? Title => _title.Text;

    public void Refresh(ImportWizardLabels labels)
    {
        _labels = labels;
        _title.Text = _dragging ? labels.DropRelease : labels.DropTitle;
        _or.Text = labels.DropOr;
        _browse.Text = labels.DropBrowse;
        AutomationProperties.SetName(this, labels.DropName);
    }

    protected override Type StyleKeyOverride => typeof(ImportWizardDropZone);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPointerOverProperty)
        {
            var target = IsPointerOver ? 1.0 : 0.0;
            if (ArcMotion.IsFull(this) && IsLoaded)
            {
                _hover ??= Motion(v => _hoverValue = v, _hoverValue);
                _ = _hover.AnimateTo(target, MotionSpec.Tween(ImportWizardMotion.Fast, ImportWizardMotion.EaseOut));
            }
            else
            {
                _hoverValue = target;
                _hover?.Jump(target);
                InvalidateVisual();
            }
        }
    }

    private MotionValue<double> Motion(Action<double> set, double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += v =>
        {
            set(v);
            ApplyDrag();
            InvalidateVisual();
        };
        return value;
    }

    private void SetDragging(bool dragging)
    {
        if (_dragging == dragging)
        {
            return;
        }
        _dragging = dragging;
        PseudoClasses.Set(DraggingPseudoClass, dragging);
        _title.Text = dragging ? _labels.DropRelease : _labels.DropTitle;
        ArcTokens.Bind(_icon, ArcGlyph.ForegroundProperty, dragging ? ArcDesignTokenKind.Accent : ArcDesignTokenKind.TextSecondary);
        var target = dragging ? 1.0 : 0.0;
        if (ArcMotion.IsFull(this) && IsLoaded)
        {
            _drag ??= Motion(v => _dragValue = v, _dragValue);
            _ = _drag.AnimateTo(target, MotionSpec.Spring(ImportWizardMotion.Smooth));
        }
        else
        {
            _dragValue = target;
            _drag?.Jump(target);
            ApplyDrag();
            InvalidateVisual();
        }
    }

    private void ApplyDrag()
    {
        var full = ArcMotion.IsFull(this);
        var scale = full ? 1 + 0.01 * _dragValue : 1;
        RenderTransform = scale == 1 ? null : new ScaleTransform(scale, scale);
        _lift.Y = full ? -4 * _dragValue : 0;
    }

    private static bool CarriesFiles(DragEventArgs e) => e.DataTransfer.Contains(DataFormat.File);

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (!CarriesFiles(e))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }
        e.DragEffects = DragDropEffects.Copy;
        SetDragging(true);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = CarriesFiles(e) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void OnDragLeave(object? sender, DragEventArgs e) => SetDragging(false);

    private void OnDrop(object? sender, DragEventArgs e)
    {
        SetDragging(false);
        if (e.DataTransfer.TryGetFiles() is { Length: > 0 } files)
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
            _view.Drop(files);
        }
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var radius = ArcTokens.GetCornerRadius(this, ArcDesignTokenKind.RadiusStep22, new CornerRadius(22)).TopLeft;
        var dark = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        var muted = ArcTokens.GetColor(this, ArcDesignTokenKind.SurfaceMuted);
        var accent = ArcTokens.GetColor(this, ArcDesignTokenKind.Accent);
        var fill = Lerp(muted, Oklch.WithAlpha(accent, dark ? 0.13 : 0.10), _dragValue);
        var strong = ArcTokens.GetColor(this, ArcDesignTokenKind.BorderStrong);
        var hover = ArcTokens.GetColor(this, ArcDesignTokenKind.TextMuted);
        var edgeColor = Lerp(Lerp(strong, hover, _hoverValue), accent, _dragValue);
        var edge = new Pen(new ImmutableSolidColorBrush(edgeColor), 1, _dragging ? null : new ImmutableDashStyle([3, 3], 0));
        var rect = bounds.Deflate(0.5);
        context.DrawRectangle(new ImmutableSolidColorBrush(fill), edge, rect, radius - 0.5, radius - 0.5);
        if (Classes.Contains(":focus-visible"))
        {
            var ring = new Pen(new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.FocusRing)), 2);
            var ringRect = bounds.Inflate(3);
            context.DrawRectangle(null, ring, ringRect, radius + 3, radius + 3);
        }
    }

    private static Color Lerp(Color a, Color b, double t)
    {
        if (t <= 0)
        {
            return a;
        }
        if (t >= 1)
        {
            return b;
        }
        byte L(byte x, byte y) => (byte)Math.Round(x + (y - x) * t);
        return Color.FromArgb(L(a.A, b.A), L(a.R, b.R), L(a.G, b.G), L(a.B, b.B));
    }
}

/// <summary>
/// The file card: the file row (spreadsheet icon in Success, name, facts or "Reading file", the remove button), the 2px
/// reading line while the file is read, then the preview table of the first five rows and its footer line.
/// </summary>
internal sealed class ImportWizardFileCard : Border
{
    private readonly ImportWizardUploadView _view;
    private readonly StackPanel _stack;
    private readonly TextBlock _name;
    private readonly TextBlock _facts;
    private readonly ImportWizardIconButton _remove;
    private Border? _readingTrack;
    private Border? _readingFill;
    private Control? _preview;
    private MotionValue<double>? _reading;
    private bool _previewShown;

    public ImportWizardFileCard(ImportWizardUploadView view)
    {
        _view = view;
        ClipToBounds = true;
        this.BindCornerRadius(ArcDesignTokenKind.RadiusStep22);
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(this, BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);

        var icon = ImportWizardUi.Glyph(ArcIcons.FileSpreadsheet, 20, ArcDesignTokenKind.Success);
        _name = ImportWizardUi.Text(null, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground, FontWeight.Medium, 19.6);
        _name.TextTrimming = TextTrimming.CharacterEllipsis;
        _facts = ImportWizardUi.Text(null, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, lineHeight: 16.8, tabular: true);
        _facts.TextTrimming = TextTrimming.CharacterEllipsis;
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(_name);
        text.Children.Add(_facts);
        _remove = new ImportWizardIconButton(ArcIcons.X, 16);
        _remove.Click += (_, e) =>
        {
            e.Handled = true;
            _view.RemoveFile();
        };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12, Margin = new Thickness(16, 12, 12, 12) };
        Grid.SetColumn(text, 1);
        Grid.SetColumn(_remove, 2);
        row.Children.Add(icon);
        row.Children.Add(text);
        row.Children.Add(_remove);
        _stack = new StackPanel();
        _stack.Children.Add(row);
        Child = _stack;
    }

    /// <summary>The remove button (tests).</summary>
    internal ImportWizardIconButton RemoveButton => _remove;

    /// <summary>The facts line (tests).</summary>
    internal string? Facts => _facts.Text;

    /// <summary>The preview (tests).</summary>
    internal Control? Preview => _preview;

    public void Update(bool animate)
    {
        var owner = _view.Owner;
        var labels = owner.Labels;
        AutomationProperties.SetName(_remove, labels.RemoveFile);
        if (owner.UploadPhase == ImportUploadPhase.Reading)
        {
            _name.Text = owner.PendingFileName;
            _facts.Text = labels.Reading;
            ShowReading(animate);
            return;
        }
        var session = _view.Session;
        _name.Text = session.FileName;
        _facts.Text = labels.FileFacts(session.Table.Rows.Count, session.Table.Headers.Count, session.FileSize / 1024.0);
        HideReading();
        if (!_previewShown)
        {
            _previewShown = true;
            _preview = BuildPreview(animate);
            _stack.Children.Add(_preview);
        }
    }

    private void ShowReading(bool animate)
    {
        if (_readingTrack is not null)
        {
            return;
        }
        _readingFill = new Border { RenderTransformOrigin = new RelativePoint(0, 0.5, RelativeUnit.Relative), RenderTransform = new ScaleTransform(0, 1) };
        ArcTokens.Bind(_readingFill, BackgroundProperty, ArcDesignTokenKind.Accent);
        _readingTrack = new Border { Height = 2, Child = _readingFill };
        ArcTokens.Bind(_readingTrack, BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        _stack.Children.Add(_readingTrack);
        if (!animate || !ArcMotion.IsFull(this))
        {
            ((ScaleTransform)_readingFill.RenderTransform!).ScaleX = 1;
            return;
        }
        _reading = new MotionValue<double>(this, 0, MotionVectors.Double);
        _reading.Changed += v =>
        {
            if (_readingFill?.RenderTransform is ScaleTransform scale)
            {
                scale.ScaleX = v;
            }
        };
        _ = _reading.AnimateTo(1, MotionSpec.Tween(ImportWizardMotion.Reading, ImportWizardMotion.EaseOut));
    }

    private void HideReading()
    {
        if (_readingTrack is null)
        {
            return;
        }
        _stack.Children.Remove(_readingTrack);
        _readingTrack = null;
        _readingFill = null;
    }

    private Control BuildPreview(bool animate)
    {
        var session = _view.Session;
        var labels = _view.Owner.Labels;
        var table = new ImportWizardTable();
        var header = new ImportWizardTableRow(ImportTableLine.Header);
        foreach (var name in session.Table.Headers)
        {
            header.Add(HeaderCell(name));
        }
        table.Children.Add(header);
        var shown = Math.Min(PreviewRowsCount, session.Table.Rows.Count);
        var rows = new List<ImportWizardTableRow>();
        for (var r = 0; r < shown; r++)
        {
            var row = new ImportWizardTableRow(r == shown - 1 ? ImportTableLine.None : ImportTableLine.Row);
            foreach (var value in session.Table.Rows[r])
            {
                row.Add(BodyCell(value, labels.Blank));
            }
            table.Children.Add(row);
            rows.Add(row);
        }
        var scroll = new ScrollViewer
        {
            Content = table,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Focusable = true,
        };
        AutomationProperties.SetName(scroll, labels.PreviewName);
        scroll.SizeChanged += (_, e) => table.FillWidth = e.NewSize.Width;
        var scrollFrame = new Border { Child = scroll };
        ImportWizardUi.TopLine(scrollFrame, ArcDesignTokenKind.Border);
        var footer = ImportWizardUi.Text(labels.PreviewFooter(shown, session.Table.Rows.Count), ArcDesignTokenKind.TypeXS,
            ArcDesignTokenKind.TextMuted, lineHeight: 16.8, tabular: true);
        footer.Padding = new Thickness(16, 8);
        var footerFrame = new Border { Child = footer };
        ImportWizardUi.TopLine(footerFrame);
        var preview = new StackPanel();
        preview.Children.Add(scrollFrame);
        preview.Children.Add(footerFrame);
        if (animate && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off)
        {
            Reveal(preview, rows);
        }
        return preview;
    }

    private const int PreviewRowsCount = 5;

    private void Reveal(Control preview, List<ImportWizardTableRow> rows)
    {
        // The preview fades in (about 250ms); its rows rise 6 one after another, 40ms apart, on the smooth spring.
        var full = ArcMotion.IsFull(this);
        var fade = new MotionValue<double>(preview, 0, MotionVectors.Double);
        fade.Changed += v => preview.Opacity = v;
        preview.Opacity = 0;
        _ = fade.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(250), ImportWizardMotion.EaseOut));
        if (!full)
        {
            return;
        }
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var lift = new TranslateTransform(0, 6);
            row.RenderTransform = lift;
            row.Opacity = 0;
            var value = new MotionValue<double>(row, 0, MotionVectors.Double);
            value.Changed += v =>
            {
                row.Opacity = v;
                lift.Y = 6 * (1 - v);
            };
            var delay = TimeSpan.FromMilliseconds(40) + ImportWizardMotion.PreviewStagger * i;
            _ = value.AnimateTo(1, MotionSpec.Spring(ImportWizardMotion.Smooth, delay));
        }
    }

    private static Control HeaderCell(string text)
    {
        var block = ImportWizardUi.Text(text, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, FontWeight.Medium, 16.8);
        block.Padding = new Thickness(12, 8);
        return block;
    }

    private static Control BodyCell(string value, string blank)
    {
        var empty = string.IsNullOrWhiteSpace(value);
        var block = ImportWizardUi.Text(empty ? blank : value.Replace('\n', ' ').Replace('\r', ' '), ArcDesignTokenKind.TypeSM,
            empty ? ArcDesignTokenKind.TextMuted : ArcDesignTokenKind.Foreground, lineHeight: 19.6, tabular: true);
        block.Padding = new Thickness(12, 8);
        return block;
    }
}

/// <summary>
/// A round icon button (the file card's remove, 30 across with a 16px x): transparent, TextMuted; hover SurfaceMuted
/// with the text color (160ms). Styled by its ControlTheme in <c>ImportWizardTheme.axaml</c>.
/// </summary>
internal sealed class ImportWizardIconButton : AvaloniaButton
{
    public ImportWizardIconButton(Geometry icon, double iconSize)
    {
        var glyph = new ArcGlyph { Data = icon, Size = iconSize, StrokeThickness = 2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        glyph.Bind(ArcGlyph.ForegroundProperty, this.GetObservable(ForegroundProperty));
        Content = glyph;
    }

    protected override Type StyleKeyOverride => typeof(ImportWizardIconButton);
}
