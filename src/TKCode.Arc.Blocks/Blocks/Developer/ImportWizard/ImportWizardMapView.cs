using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The Map step of <see cref="ImportWizard"/> ("Match fields"): the heading with "k of n columns mapped" (k rolls up as
/// columns resolve; ignored columns don't count), the bordered list (radius 22) with one row per source column (name and
/// two sample values, an arrow, the field select and the confidence cell), an alert under the list when a required field
/// has no column or a field is chosen twice, and Back / "Check rows". The first visit per file reveals the matcher's
/// results: every row says "Matching" (pulsing), the first resolves 270ms after the step opens and then one every 117ms;
/// the primary button waits for the last. A later visit shows them resolved. Reduced motion resolves every row at once
/// (the meters still fill on the spring's curve); Off shows the results.
/// </summary>
internal sealed class ImportWizardMapView : ImportWizardStepView
{
    private readonly TextBlock _title;
    private readonly ImportWizardCountText _count;
    private readonly ImportWizardMapList _list = new();
    private readonly Border _box;
    private readonly TextBlock _alert;
    private readonly ImportWizardButton _back;
    private readonly ImportWizardButton _next;
    private readonly ImportWizardFooter _footer;
    private readonly List<ImportWizardMapRow> _rows = new();
    private SelectOption[] _options = [];
    private LiveLoop? _pulse;
    private double _pulseTime;
    private CancellationTokenSource? _reveal;
    private MotionValue<double>? _alertFade;
    private string? _alertText;
    private bool _matching;
    private bool _subscribed;
    private bool _syncing;

    public ImportWizardMapView(ImportWizard owner)
        : base(owner)
    {
        _count = new ImportWizardCountText(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.TextSecondary)
        {
            // Arc's subtitle line is 19.6 tall; the strips and words are centered in it.
            MinHeight = 19.6,
        };
        var head = ImportWizardUi.Heading(Labels.MapTitle, _count);
        _title = (TextBlock)head.Children[0];
        SetHead(head);

        _box = ImportWizardUi.Box(_list, ArcDesignTokenKind.RadiusStep22);
        _alert = ImportWizardUi.Text(null, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Danger, lineHeight: 19.6);
        _alert.TextWrapping = TextWrapping.Wrap;
        _alert.IsVisible = false;
        var body = new StackPanel();
        ImportWizardUi.Bind(body, StackPanel.SpacingProperty, ImportWizardTokenKind.StepSpacing);
        body.Children.Add(_box);
        body.Children.Add(_alert);
        SetBody(body);

        _back = new ImportWizardButton(false, Labels.Back, ArcIcons.ArrowLeft) { HorizontalAlignment = HorizontalAlignment.Left };
        _back.Click += (_, e) =>
        {
            e.Handled = true;
            Owner.GoTo(ImportWizardStep.Upload);
        };
        _next = new ImportWizardButton(true, Labels.ToReview, null, ArcIcons.ArrowRight) { HorizontalAlignment = HorizontalAlignment.Right };
        _next.Click += (_, e) =>
        {
            e.Handled = true;
            if (CanContinue)
            {
                Owner.GoTo(ImportWizardStep.Review);
            }
        };
        _footer = new ImportWizardFooter(_next, _back) { IsCompact = IsCompact };
        SetFooter(_footer);

        BuildRows();
        _matching = AnyUnrevealed();
        _count.IsInstant = true;
        UpdateCount();
        UpdateValidity(animate: false);
    }

    public override ImportWizardStep Step => ImportWizardStep.Map;

    /// <summary>The rows (tests).</summary>
    internal IReadOnlyList<ImportWizardMapRow> Rows => _rows;

    /// <summary>The subtitle (tests).</summary>
    internal ImportWizardCountText Count => _count;

    /// <summary>The alert under the list (tests); hidden when there is nothing to say.</summary>
    internal TextBlock AlertText => _alert;

    /// <summary>The Back button (tests).</summary>
    internal ImportWizardButton BackButton => _back;

    /// <summary>The "Check rows" button (tests).</summary>
    internal ImportWizardButton NextButton => _next;

    /// <summary>True while the columns are being revealed.</summary>
    internal bool IsMatching => _matching;

    private bool CanContinue => !_matching && Session.IsMappingValid;

    // ---- Rows ----

    private void BuildRows()
    {
        _syncing = true;
        try
        {
            _options = BuildOptions();
            _rows.Clear();
            _list.Children.Clear();
            foreach (var column in Session.Columns)
            {
                var row = new ImportWizardMapRow(column, _options, Labels, IsCompact);
                row.SetDivider(_rows.Count > 0);
                row.Select.SelectionChanged += (_, _) => OnSelected(row);
                _rows.Add(row);
                _list.Children.Add(row);
                ShowResult(row, animate: false);
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private SelectOption[] BuildOptions()
    {
        var targets = Session.Targets;
        var options = new SelectOption[targets.Count + 1];
        options[0] = new SelectOption(-1, Labels.DoNotImport);
        for (var t = 0; t < targets.Count; t++)
        {
            var label = targets[t].IsRequired ? Labels.RequiredOption(targets[t].Label) : targets[t].Label;
            options[t + 1] = new SelectOption(t, label);
        }
        return options;
    }

    private void OnSelected(ImportWizardMapRow row)
    {
        if (_syncing)
        {
            return;
        }
        var index = row.Select.SelectedIndex;
        if (index < 0)
        {
            return;
        }
        Session.SetTarget(row.Column.Index, index - 1);
    }

    private (ImportMapResult Result, int Percent) ResultOf(ImportColumnState column)
    {
        if (!column.IsRevealed)
        {
            return (ImportMapResult.Matching, 0);
        }
        if (column.IsIgnored)
        {
            return (ImportMapResult.Ignored, 0);
        }
        if (Session.IsDuplicate(column.Index))
        {
            return (ImportMapResult.Duplicate, 0);
        }
        return column.IsChosen ? (ImportMapResult.Chosen, 100) : (ImportMapResult.Score, column.Proposal.Confidence);
    }

    private void ShowResult(ImportWizardMapRow row, bool animate)
    {
        var column = row.Column;
        row.SetIgnored(column.IsIgnored);
        if (row.Select.SelectedIndex != column.Target + 1)
        {
            _syncing = true;
            row.Select.SelectedIndex = column.Target + 1;
            _syncing = false;
        }
        var (result, percent) = ResultOf(column);
        row.Confidence.Show(result, percent, animate);
    }

    private bool AnyUnrevealed()
    {
        foreach (var column in Session.Columns)
        {
            if (!column.IsRevealed)
            {
                return true;
            }
        }
        return false;
    }

    private int RevealedMappedCount()
    {
        var count = 0;
        foreach (var column in Session.Columns)
        {
            if (column.IsRevealed && !column.IsIgnored)
            {
                count++;
            }
        }
        return count;
    }

    private void UpdateCount()
    {
        var mapped = RevealedMappedCount();
        _count.Set(Labels.MappedCount(mapped), mapped, Session.Columns.Count);
        _count.IsInstant = false;
    }

    // ---- Validity ----

    private string? AlertMessage()
    {
        var missing = Session.FirstMissingRequired();
        if (missing >= 0)
        {
            return Labels.MissingRequired(Session.Targets[missing].Label.ToLowerInvariant());
        }
        var duplicate = Session.FirstDuplicateTarget();
        return duplicate >= 0 ? Labels.DuplicateField(Session.Targets[duplicate].Label) : null;
    }

    private void UpdateValidity(bool animate)
    {
        _next.IsEnabled = CanContinue;
        // The alert waits for the matcher, like the button: it speaks about the finished mapping.
        var text = _matching ? null : AlertMessage();
        if (text is null)
        {
            _alertText = null;
            _alert.IsVisible = false;
            _alert.Text = null;
            return;
        }
        var appearing = !_alert.IsVisible;
        _alert.Text = text;
        _alert.IsVisible = true;
        if (text != _alertText)
        {
            _alertText = text;
            Owner.Alert(text);
        }
        if (!appearing)
        {
            return;
        }
        // The line fades in over 240ms (ease-out); the card's height spring makes room for it.
        if (!animate || ArcMotion.GetEffectiveLevel(Owner) == ArcMotionLevel.Off || !Owner.IsLoaded)
        {
            _alertFade?.Jump(1);
            _alert.Opacity = 1;
            return;
        }
        if (_alertFade is null)
        {
            _alertFade = new MotionValue<double>(_alert, 0, MotionVectors.Double);
            _alertFade.Changed += v => _alert.Opacity = v;
        }
        _alertFade.Jump(0);
        _ = _alertFade.AnimateTo(1, MotionSpec.Tween(ImportWizardMotion.Standard, ImportWizardMotion.EaseOut));
    }

    private void OnMappingChanged()
    {
        if (Session.Columns.Count != _rows.Count)
        {
            return;
        }
        var animate = Owner.IsLoaded;
        foreach (var row in _rows)
        {
            ShowResult(row, animate);
        }
        UpdateCount();
        UpdateValidity(animate);
    }

    // ---- Reveal ----

    public override void OnEntered(bool animate)
    {
        base.OnEntered(animate);
        if (!AnyUnrevealed())
        {
            _matching = false;
            UpdateValidity(animate: false);
            return;
        }
        CancelReveal(markRevealed: false);
        _matching = true;
        UpdateValidity(animate: false);
        var reveal = new CancellationTokenSource();
        _reveal = reveal;
        _ = RevealAsync(animate && Owner.IsLoaded, reveal.Token);
    }

    private async Task RevealAsync(bool animate, CancellationToken token)
    {
        var level = ArcMotion.GetEffectiveLevel(Owner);
        animate = animate && level != ArcMotionLevel.Off;
        // Full motion resolves one column at a time on a fixed schedule (270ms, then every 117ms, scaled by the wizard's
        // timing); reduced motion resolves them all at once.
        var staggered = animate && level == ArcMotionLevel.Full;
        if (staggered)
        {
            _pulse ??= new LiveLoop(_box, StepPulse);
            _pulseTime = 0;
            _pulse.Start();
        }
        var clock = Stopwatch.StartNew();
        try
        {
            for (var i = 0; i < Session.Columns.Count; i++)
            {
                if (Session.Columns[i].IsRevealed)
                {
                    continue;
                }
                if (staggered)
                {
                    var due = ImportWizardMotion.MatchFirst + ImportWizardMotion.MatchInterval * i;
                    var scale = Owner.TimingScale;
                    var elapsed = scale > 0 ? TimeSpan.FromTicks((long)(clock.Elapsed.Ticks / scale)) : due;
                    if (due > elapsed)
                    {
                        await Owner.Delay(due - elapsed, token);
                    }
                }
                if (token.IsCancellationRequested)
                {
                    return;
                }
                Resolve(i, animate);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (token.IsCancellationRequested)
        {
            return;
        }
        _reveal = null;
        _matching = false;
        UpdateValidity(animate);
        Owner.Announce(Labels.MappedAnnouncement(Session.MappedCount, Session.Columns.Count));
    }

    private void Resolve(int index, bool animate)
    {
        if (index >= Session.Columns.Count)
        {
            return;
        }
        Session.Columns[index].IsRevealed = true;
        if (index < _rows.Count)
        {
            ShowResult(_rows[index], animate);
        }
        UpdateCount();
    }

    /// <summary>One frame of the "Matching" pulse: opacity 1 → 0.4 → 1 every second, each half on the pulse curve.</summary>
    private bool StepPulse(double seconds)
    {
        _pulseTime += seconds;
        var period = ImportWizardMotion.PulsePeriod.TotalSeconds;
        var phase = _pulseTime % period / period;
        var opacity = phase < 0.5
            ? 1 - 0.6 * ImportWizardMotion.Pulse.Ease(phase * 2)
            : 0.4 + 0.6 * ImportWizardMotion.Pulse.Ease((phase - 0.5) * 2);
        var any = false;
        foreach (var row in _rows)
        {
            if (row.Confidence.IsMatchingVisible)
            {
                row.Confidence.SetPulse(opacity);
                any = true;
            }
        }
        return any;
    }

    private void CancelReveal(bool markRevealed)
    {
        _reveal?.Cancel();
        _reveal = null;
        _pulse?.Stop();
        if (markRevealed)
        {
            // Leaving (or leaving the window) mid-way finishes the matching: coming back shows the results.
            foreach (var column in Session.Columns)
            {
                column.IsRevealed = true;
            }
            _matching = false;
        }
    }

    public override void OnLeaving()
    {
        base.OnLeaving();
        CancelReveal(markRevealed: true);
        _pulse?.Dispose();
        _pulse = null;
        Unsubscribe();
    }

    // ---- Lifetime ----

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_subscribed)
        {
            _subscribed = true;
            Session.MappingChanged += OnMappingChanged;
        }
        // Attached again after a detach that finished the matching: show where things stand.
        if (_reveal is null && _rows.Count == Session.Columns.Count)
        {
            _matching = AnyUnrevealed() && _matching;
            foreach (var row in _rows)
            {
                ShowResult(row, animate: false);
            }
            UpdateCount();
            UpdateValidity(animate: false);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_reveal is not null)
        {
            CancelReveal(markRevealed: true);
        }
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (_subscribed)
        {
            _subscribed = false;
            Session.MappingChanged -= OnMappingChanged;
        }
    }

    protected override void OnCompactChanged(bool compact)
    {
        base.OnCompactChanged(compact);
        _footer.IsCompact = compact;
        foreach (var row in _rows)
        {
            row.SetCompact(compact);
        }
    }

    public override void Refresh()
    {
        base.Refresh();
        _title.Text = Labels.MapTitle;
        _back.Text = Labels.Back;
        _next.Text = Labels.ToReview;
        _options = BuildOptions();
        _syncing = true;
        try
        {
            foreach (var row in _rows)
            {
                row.SetLabels(Labels, _options);
            }
        }
        finally
        {
            _syncing = false;
        }
        UpdateCount();
        _alertText = null;
        UpdateValidity(animate: false);
    }
}
