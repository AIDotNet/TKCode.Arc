using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The import step. While the batches run: the big count "done of total" (52px display digits that roll), "Sending
/// contacts", the 360×6 bar on the smooth spring, "Batch k of m" and Cancel (back to the review). A failed batch shows its
/// message with "Try again". When every batch is done the progress fades out, blurs 4px and shrinks to 0.98 while the
/// summary fades in from 6px blur rising 12 (smooth spring): the success mark whose check draws itself, "Imported n
/// contacts", New / Updated / Left out (counting up once from zero) and the actions "Download left-out rows" and "Start a
/// new import". The step has no footer.
/// </summary>
internal sealed class ImportWizardImportView : ImportWizardStepView
{
    private readonly ImportWizardSwap _swap;
    private Control? _progress;
    private ImportWizardBigCount? _count;
    private TextBlock? _importing;
    private ImportWizardProgressBar? _bar;
    private TextBlock? _batch;
    private TextBlock? _error;
    private ImportWizardButton? _cancel;
    private ImportWizardButton? _retry;
    private Control? _summary;

    public ImportWizardImportView(ImportWizard owner)
        : base(owner)
    {
        _swap = new ImportWizardSwap();
        SetBody(_swap);
        Update(animate: false);
    }

    public override ImportWizardStep Step => ImportWizardStep.Import;

    /// <summary>The progress bar while the import runs (tests).</summary>
    internal ImportWizardProgressBar? Bar => _bar;

    /// <summary>The big count while the import runs (tests).</summary>
    internal ImportWizardBigCount? Count => _count;

    /// <summary>The batch line (tests).</summary>
    internal string? BatchText => _batch?.Text;

    /// <summary>The summary once it shows (tests).</summary>
    internal ImportWizardSummary? Summary => _summary as ImportWizardSummary;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Owner.RunChanged += OnRunChanged;
        Update(animate: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Owner.RunChanged -= OnRunChanged;
    }

    public override void Refresh() => Update(animate: false);

    private void OnRunChanged() => Update(animate: Owner.IsLoaded);

    private void Update(bool animate)
    {
        if (Owner.RunPhase == ImportRunPhase.Done)
        {
            if (_summary is null)
            {
                _summary = new ImportWizardSummary(this, animate);
                _swap.Show(_summary,
                    exit: new ImportSwapMotion(0.98, 0, ImportWizardMotion.StepBlur, ImportWizardMotion.Smooth),
                    enter: new ImportSwapMotion(1, 12, 6, ImportWizardMotion.Smooth),
                    animate);
                if (_progress?.IsKeyboardFocusWithin == true)
                {
                    ((ImportWizardSummary)_summary).FocusRestart();
                }
            }
            return;
        }
        _progress ??= BuildProgress();
        if (_swap.Current != _progress)
        {
            _swap.Show(_progress, default, default, animate: false);
        }
        var labels = Labels;
        var total = Owner.RunRecords.Count;
        _count!.Set(labels.Progress, Owner.RunImported, total, instant: !animate);
        _importing!.Text = Owner.WithNoun(labels.Importing);
        _bar!.SetValue(total == 0 ? 0 : Owner.RunImported / (double)total, Owner.RunImported, total, animate);
        _batch!.Text = labels.BatchLine(Math.Min(Owner.RunBatch, Owner.RunBatchCount), Owner.RunBatchCount);
        var failed = Owner.RunPhase == ImportRunPhase.Failed;
        _error!.IsVisible = failed;
        _error.Text = Owner.RunError;
        _retry!.IsVisible = failed;
        _retry.Text = labels.Retry;
        _cancel!.Text = labels.Cancel;
    }

    private Control BuildProgress()
    {
        var labels = Labels;
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 40, 0, 16), HorizontalAlignment = HorizontalAlignment.Stretch };
        _count = new ImportWizardBigCount();
        AutomationProperties.SetLiveSetting(_count, AutomationLiveSetting.Polite);
        _count.HorizontalAlignment = HorizontalAlignment.Center;
        _importing = ImportWizardUi.Text(null, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.TextSecondary, lineHeight: 19.6);
        _importing.HorizontalAlignment = HorizontalAlignment.Center;
        _bar = new ImportWizardProgressBar { Margin = new Thickness(0, 16, 0, 8) };
        AutomationProperties.SetName(_bar, labels.ProgressName);
        _batch = ImportWizardUi.Text(null, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, lineHeight: 16.8, tabular: true);
        _batch.HorizontalAlignment = HorizontalAlignment.Center;
        _batch.Margin = new Thickness(0, 0, 0, 16);
        _error = ImportWizardUi.Text(null, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Danger, lineHeight: 19.6);
        _error.HorizontalAlignment = HorizontalAlignment.Center;
        _error.TextAlignment = TextAlignment.Center;
        _error.TextWrapping = TextWrapping.Wrap;
        _error.IsVisible = false;
        _cancel = new ImportWizardButton(false, labels.Cancel);
        _cancel.Click += (_, e) =>
        {
            e.Handled = true;
            Owner.CancelImport();
        };
        _retry = new ImportWizardButton(true, labels.Retry, iconStart: ArcIcons.RotateCcw) { IsVisible = false };
        _retry.Click += (_, e) =>
        {
            e.Handled = true;
            Owner.RetryImport();
        };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        actions.Children.Add(_cancel);
        actions.Children.Add(_retry);
        panel.Children.Add(_count);
        panel.Children.Add(_importing);
        panel.Children.Add(_bar);
        panel.Children.Add(_batch);
        panel.Children.Add(_error);
        panel.Children.Add(actions);
        return panel;
    }

    /// <summary>"Start a new import".</summary>
    internal void Restart() => Owner.Reset();

    /// <summary>"Download left-out rows".</summary>
    internal Task<bool> SaveSkippedAsync() => Owner.SaveSkippedRowsAsync();
}

/// <summary>
/// The big "done of total" line: the done count in 52px display digits (medium, −1.56 tracking, rolling on the quick
/// spring) followed closely by "of total" in 22px muted text on the same baseline. The words around "{0}" come from
/// <see cref="ImportWizardLabels.Progress"/>; the accessible name is the plain sentence.
/// </summary>
internal sealed class ImportWizardBigCount : StackPanel
{
    private readonly RollingDigits _done;
    private readonly TextBlock _before;
    private readonly TextBlock _after;

    public ImportWizardBigCount()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 2;
        _before = Small();
        _after = Small();
        _done = new RollingDigits
        {
            FontWeight = FontWeight.Medium,
            Falloff = RollingDigitsFalloff.None,
            WheelMotion = MotionSpec.Spring(ImportWizardMotion.Quick),
            GlyphPitch = 1.4,
            IsArmed = false,
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        ArcTokens.Bind(_done, RollingDigits.FontSizeProperty, ArcDesignTokenKind.Type4XL);
        ArcTokens.Bind(_done, RollingDigits.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_done, RollingDigits.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        Children.Add(_before);
        Children.Add(_done);
        Children.Add(_after);
    }

    /// <summary>The plain sentence (screen readers, tests).</summary>
    public string PlainText { get; private set; } = string.Empty;

    /// <summary>The done count shown (tests).</summary>
    public double Done => _done.Value;

    private static TextBlock Small()
    {
        // 22px muted, sitting on the big digits' baseline.
        var block = ImportWizardUi.Text(null, ArcDesignTokenKind.TypeXL, ArcDesignTokenKind.TextMuted);
        block.VerticalAlignment = VerticalAlignment.Bottom;
        block.Margin = new Thickness(0, 0, 0, 11);
        return block;
    }

    public void Set(string format, int done, int total, bool instant)
    {
        var at = format.IndexOf("{0}", StringComparison.Ordinal);
        var before = at < 0 ? string.Empty : format[..at];
        var after = at < 0 ? format : format[(at + 3)..];
        var totalText = ImportWizardUi.Number(total);
        _before.Text = before.Replace("{1}", totalText, StringComparison.Ordinal).Trim();
        _before.IsVisible = _before.Text.Length > 0;
        _after.Text = after.Replace("{1}", totalText, StringComparison.Ordinal).Trim();
        _done.IsInstant = instant;
        _done.Value = done;
        PlainText = DeveloperText.Format(format, ImportWizardUi.Number(done), totalText);
        AutomationProperties.SetName(this, PlainText);
    }
}

/// <summary>The import's 360×6 bar: SurfaceMuted track, Accent fill from the left on the smooth spring; a progressbar for screen readers.</summary>
internal sealed class ImportWizardProgressBar : Control
{
    private MotionValue<double>? _fill;
    private double _value;

    public ImportWizardProgressBar()
    {
        // 360 wide, narrower when the card is.
        ImportWizardUi.Bind(this, MaxWidthProperty, ImportWizardTokenKind.ProgressWidth);
        Height = 6;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    /// <summary>The drawn fill, 0–1 (tests).</summary>
    public double Fill => _value;

    public void SetValue(double fraction, int done, int total, bool animate)
    {
        AutomationProperties.SetHelpText(this, $"{done} / {total}");
        AutomationProperties.SetItemStatus(this, $"{done} of {total}");
        fraction = Math.Clamp(fraction, 0, 1);
        if (!animate || !ArcMotion.IsFull(this) || !IsLoaded)
        {
            _value = fraction;
            _fill?.Jump(fraction);
            InvalidateVisual();
            return;
        }
        if (_fill is null)
        {
            _fill = new MotionValue<double>(this, _value, MotionVectors.Double);
            _fill.Changed += v =>
            {
                _value = v;
                InvalidateVisual();
            };
        }
        _ = _fill.AnimateTo(fraction, MotionSpec.Spring(ImportWizardMotion.Smooth));
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var radius = bounds.Height / 2;
        context.DrawRectangle(new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.SurfaceMuted)), null, bounds, radius, radius);
        if (_value > 0.0005)
        {
            using (context.PushClip(new RoundedRect(bounds, radius)))
            {
                context.DrawRectangle(new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.Accent)), null,
                    new Rect(0, 0, bounds.Width * _value, bounds.Height), radius, radius);
            }
        }
    }
}

/// <summary>
/// The summary of a finished import: a 48px Success 12% disc whose 24px check draws itself (150ms after it shows, 350ms),
/// "Imported n contacts" (28 / 30.8, medium), three cells New / Updated / Left out in a 420-wide bordered box (radius 20)
/// whose numbers count up once from zero, and the actions.
/// </summary>
internal sealed class ImportWizardSummary : StackPanel
{
    private readonly ImportWizardImportView _view;
    private readonly ImportWizardButton _download;
    private readonly ImportWizardButton _restart;
    private readonly ImportWizardCheckMark _mark;
    private readonly ImportWizardCountText _title;
    private readonly ImportWizardCountText[] _numbers = new ImportWizardCountText[3];
    private bool _saved;

    public ImportWizardSummary(ImportWizardImportView view, bool animate)
    {
        _view = view;
        var owner = view.Owner;
        var labels = owner.Labels;
        var totals = owner.RunTotals;
        var imported = totals.Created + totals.Updated;
        Spacing = 8;
        Margin = new Thickness(0, 40, 0, 16);

        _mark = new ImportWizardCheckMark { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
        Children.Add(_mark);

        _title = new ImportWizardCountText(ArcDesignTokenKind.Type2XL, ArcDesignTokenKind.Foreground, FontWeight.Medium)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        AutomationProperties.SetHeadingLevel(_title, 2);
        Children.Add(_title);

        var cells = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*") };
        string[] names = [labels.Created, labels.Updated, labels.Skipped];
        int[] values = [totals.Created, totals.Updated, totals.Skipped];
        for (var i = 0; i < 3; i++)
        {
            var cell = new StackPanel { Spacing = 2, Margin = new Thickness(12) };
            cell.Children.Add(Centered(ImportWizardUi.Text(names[i], ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, lineHeight: 16.8)));
            _numbers[i] = new ImportWizardCountText(ArcDesignTokenKind.TypeXL, ArcDesignTokenKind.Foreground, FontWeight.Medium)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            cell.Children.Add(_numbers[i]);
            var frame = new Border { Child = cell };
            if (i > 0)
            {
                ArcTokens.Bind(frame, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
                ArcTokens.Bind(frame, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessLeft);
            }
            Grid.SetColumn(frame, i);
            cells.Children.Add(frame);
        }
        var box = ImportWizardUi.Box(cells, ArcDesignTokenKind.RadiusStep20);
        // 420 wide, narrower when the card is (Arc: max-width 100%).
        ImportWizardUi.Bind(box, MaxWidthProperty, ImportWizardTokenKind.SummaryWidth);
        box.HorizontalAlignment = HorizontalAlignment.Stretch;
        box.Margin = new Thickness(0, 20);
        Children.Add(box);

        _download = new ImportWizardButton(false, labels.DownloadSkipped, iconStart: ArcIcons.Download);
        _download.Click += async (_, e) =>
        {
            e.Handled = true;
            if (await _view.SaveSkippedAsync() && !_saved)
            {
                _saved = true;
                _download.Text = labels.Downloaded;
                _download.SetIcons(ArcIcons.Check, null);
            }
        };
        _download.IsVisible = totals.Skipped > 0;
        _restart = new ImportWizardButton(true, labels.StartOver, iconStart: ArcIcons.RotateCcw);
        _restart.Click += (_, e) =>
        {
            e.Handled = true;
            _view.Restart();
        };
        var actions = new ImportWizardCenteredWrap();
        actions.Children.Add(_download);
        actions.Children.Add(_restart);
        Children.Add(actions);

        var countUp = animate && ArcMotion.IsFull(owner);
        _title.IsInstant = true;
        _title.Set(owner.WithNoun(labels.Done(imported), imported), countUp ? 0 : imported);
        for (var i = 0; i < 3; i++)
        {
            _numbers[i].IsInstant = true;
            _numbers[i].Set("{0}", countUp ? 0 : values[i]);
        }
        if (countUp)
        {
            CountUp(imported, values);
            _mark.Draw(ImportWizardMotion.CheckDelay);
        }
        else
        {
            _mark.Progress = 1;
        }
    }

    /// <summary>The numbers shown (tests).</summary>
    internal IReadOnlyList<ImportWizardCountText> Numbers => _numbers;

    /// <summary>The title (tests).</summary>
    internal ImportWizardCountText Title => _title;

    /// <summary>The restart button (tests).</summary>
    internal ImportWizardButton RestartButton => _restart;

    /// <summary>The download button (tests).</summary>
    internal ImportWizardButton DownloadButton => _download;

    public void FocusRestart() => _restart.Focus(NavigationMethod.Tab);

    private static Control Centered(Control control)
    {
        control.HorizontalAlignment = HorizontalAlignment.Center;
        return control;
    }

    private void CountUp(int imported, int[] values)
    {
        // Every number counts up once from zero (about 650ms, S-shaped); the digits ride the quick spring behind it.
        var progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        progress.Changed += p =>
        {
            _title.IsInstant = false;
            _title.Set(_title.Format, Math.Round(imported * p));
            for (var i = 0; i < 3; i++)
            {
                _numbers[i].IsInstant = false;
                _numbers[i].Set("{0}", Math.Round(values[i] * p));
            }
        };
        _ = progress.AnimateTo(1, MotionSpec.Tween(ImportWizardMotion.CountUp, ImportWizardMotion.CountUpEasing));
    }
}

/// <summary>A 48px Success 12% disc with a 24px Success check (stroke 2, round caps) that draws itself.</summary>
internal sealed class ImportWizardCheckMark : Control
{
    private double _progress;
    private MotionValue<double>? _draw;

    public ImportWizardCheckMark()
    {
        Width = 48;
        Height = 48;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    /// <summary>How much of the check is drawn, 0–1.</summary>
    public double Progress
    {
        get => _progress;
        set
        {
            _progress = Math.Clamp(value, 0, 1);
            InvalidateVisual();
        }
    }

    public void Draw(TimeSpan delay)
    {
        _draw ??= new MotionValue<double>(this, 0, MotionVectors.Double);
        _draw.Changed += v => Progress = v;
        _ = _draw.AnimateTo(1, MotionSpec.Tween(ImportWizardMotion.CheckDuration, ImportWizardMotion.CheckDraw, delay));
    }

    public override void Render(DrawingContext context)
    {
        var success = ArcTokens.GetColor(this, ArcDesignTokenKind.Success);
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        context.DrawEllipse(new ImmutableSolidColorBrush(Oklch.WithAlpha(success, 0.12)), null, center, 24, 24);
        if (_progress <= 0.001)
        {
            return;
        }
        // Lucide "check" (M20 6 9 17l-5-5) drawn from its start, 24 units at 24px, centered.
        var points = new[] { new Point(4, 12), new Point(9, 17), new Point(20, 6) };
        var first = Point.Distance(points[0], points[1]);
        var second = Point.Distance(points[1], points[2]);
        var length = (first + second) * _progress;
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(points[0], false);
            if (length <= first)
            {
                g.LineTo(points[0] + (points[1] - points[0]) * (length / first));
            }
            else
            {
                g.LineTo(points[1]);
                g.LineTo(points[1] + (points[2] - points[1]) * ((length - first) / second));
            }
            g.EndFigure(false);
        }
        var pen = new Pen(new ImmutableSolidColorBrush(success), 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        using (context.PushTransform(Matrix.CreateTranslation(center.X - 12, center.Y - 12)))
        {
            context.DrawGeometry(null, pen, geometry);
        }
    }
}
