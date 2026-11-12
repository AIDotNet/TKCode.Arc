using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The Review step of <see cref="ImportWizard"/> (Arc Import wizard, design reference, ADR 0045; spec section 3.4):
/// "Review" over "i rows to fix · r ready" (both rolling, read politely); the bar with the "To fix" / "All" filter and
/// "Clean up n values"; the rows in a bordered box that scrolls sideways (line, a column per mapped field, leave-out); the
/// footer with Back and "Import r contacts · i left out".
/// <list type="bullet">
/// <item>Clean up fixes one cell every 90ms (the first at once; all at once with reduced motion or Off). A fixed cell glows
/// (Success 10%, a check) for 900ms, then in "All" its tint fades back and in "To fix" its row leaves.</item>
/// <item>A cell opens its editor on click, Enter or Space; Enter or leaving the field saves, Escape cancels, and Enter and
/// Escape give the focus back to the cell. A save that fixes the cell glows like clean-up.</item>
/// <item>The x leaves a row out (it is not imported and counts as left out in the summary).</item>
/// </list>
/// The state lives in <see cref="ImportWizard.Session"/>; this view follows its events, so the counts and rows always show it.
/// </summary>
internal sealed class ImportWizardReviewView : ImportWizardStepView
{
    private readonly StackPanel _head;
    private readonly ImportWizardCountText _subtitle;
    private readonly ImportWizardReviewFilter _filter;
    private readonly ImportWizardReviewCleanUp _cleanUp;
    private readonly ImportWizardReviewBar _bar;
    private readonly ImportWizardReviewTable _table;
    private readonly ImportWizardReviewTableBox _box;
    private readonly ImportWizardButton _back;
    private readonly ImportWizardButton _import;
    private readonly ImportWizardCountText _importWords;
    private readonly ImportWizardCountText _skipWords;
    private readonly Dictionary<(ImportReviewRow Row, int Field), int> _glows = new();
    private CancellationTokenSource _work = new();
    private ImportWizardReviewEditor? _editor;
    private ImportWizardReviewRow? _editorRow;
    private ImportWizardReviewCell? _editorCell;
    private bool _subscribed;
    private bool _countsShown;
    private bool _cleaning;
    private int _glowSerial;

    public ImportWizardReviewView(ImportWizard owner)
        : base(owner)
    {
        _subtitle = new ImportWizardCountText(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.TextSecondary) { MinHeight = 19.6 };
        AutomationProperties.SetLiveSetting(_subtitle, AutomationLiveSetting.Polite);
        _head = ImportWizardUi.Heading(Labels.ReviewTitle, _subtitle);
        SetHead(_head);

        _filter = new ImportWizardReviewFilter();
        _filter.Changed += OnFilterChanged;
        _cleanUp = new ImportWizardReviewCleanUp();
        _cleanUp.Click += (_, _) => _ = CleanUpAsync();
        _bar = new ImportWizardReviewBar(_filter, _cleanUp);

        _table = new ImportWizardReviewTable(this);
        _box = new ImportWizardReviewTableBox(_table);

        var body = new StackPanel();
        ImportWizardUi.Bind(body, StackPanel.SpacingProperty, ImportWizardTokenKind.StepSpacing);
        body.Children.Add(_bar);
        body.Children.Add(_box);
        SetBody(body);

        _back = new ImportWizardButton(false, Labels.Back, ArcIcons.ArrowLeft) { HorizontalAlignment = HorizontalAlignment.Left };
        _back.Click += (_, _) => Owner.GoTo(ImportWizardStep.Map);
        _import = new ImportWizardButton(true, string.Empty);
        _importWords = new ImportWizardCountText(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.AccentForeground);
        _skipWords = new ImportWizardCountText(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.AccentForeground) { Opacity = 0.65 };
        var words = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        words.Children.Add(_importWords);
        words.Children.Add(_skipWords);
        _import.Row.Children.Remove(_import.Label);
        _import.Row.Children.Add(words);
        _import.Click += (_, _) => Owner.StartImport();
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
        Grid.SetColumn(_import, 1);
        footer.Children.Add(_back);
        footer.Children.Add(_import);
        SetFooter(footer);
        ApplyCompact(IsCompact);

        ApplyLabels();
        _table.Sync(ShownRows(), animate: false);
        UpdateCounts(instant: true);
    }

    public override ImportWizardStep Step => ImportWizardStep.Review;

    // ---- Parts (tests) ----

    internal ImportWizardCountText Subtitle => _subtitle;

    internal ImportWizardReviewFilter Filter => _filter;

    internal ImportWizardReviewCleanUp CleanUpButton => _cleanUp;

    internal ImportWizardReviewBar Bar => _bar;

    internal ImportWizardReviewTable Table => _table;

    internal ImportWizardReviewTableBox TableBox => _box;

    internal ImportWizardButton BackButton => _back;

    internal ImportWizardButton ImportButton => _import;

    /// <summary>"Import r contacts · i left out" as one sentence.</summary>
    internal string ImportText => _importWords.PlainText + _skipWords.PlainText;

    /// <summary>The open editor, or null.</summary>
    internal ImportWizardReviewEditor? Editor => _editorCell is null ? null : _editor;

    /// <summary>True while clean-up is fixing cells.</summary>
    internal bool IsCleaningUp => _cleaning;

    /// <summary>How many fixed cells still glow.</summary>
    internal int GlowCount => _glows.Count;

    // ---- Lifetime ----

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_work.IsCancellationRequested)
        {
            _work = new CancellationTokenSource();
        }
        Subscribe();
        // The session may have changed while the card was away.
        _table.Sync(ShownRows(), animate: false);
        UpdateCounts(instant: true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Stop();
    }

    public override void OnLeaving()
    {
        base.OnLeaving();
        CloseEditor(ImportReviewEditorClose.Blur, focusBack: false);
        Stop();
    }

    /// <summary>Stops the cascade and the glows (a glow that cannot end on time ends now) and lets go of the session.</summary>
    private void Stop()
    {
        _work.Cancel();
        _cleaning = false;
        foreach (var key in _glows.Keys)
        {
            key.Row.Fixed[key.Field] = false;
            _table.RowFor(key.Row)?.Cells[key.Field].EndGlow(animate: false);
        }
        _glows.Clear();
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }
        _subscribed = true;
        Session.CellChanged += OnCellChanged;
        Session.RowRemoved += OnRowRemoved;
        Session.CountsChanged += OnCountsChanged;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }
        _subscribed = false;
        Session.CellChanged -= OnCellChanged;
        Session.RowRemoved -= OnRowRemoved;
        Session.CountsChanged -= OnCountsChanged;
    }

    public override void Refresh()
    {
        base.Refresh();
        ApplyLabels();
        UpdateCounts(instant: true);
    }

    protected override void OnCompactChanged(bool compact) => ApplyCompact(compact);

    /// <summary>On phones the primary fills the footer next to Back (Arc's flex: 1).</summary>
    private void ApplyCompact(bool compact) =>
        _import.HorizontalAlignment = compact ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;

    private void ApplyLabels()
    {
        if (_head.Children.Count > 0 && _head.Children[0] is TextBlock title)
        {
            title.Text = Labels.ReviewTitle;
        }
        _filter.SetLabels(Labels.FilterIssues, Labels.FilterAll, Labels.FilterName);
        AutomationProperties.SetName(_box.Viewer, Labels.TableName);
        _back.Text = Labels.Back;
        _table.SetLabels(Labels.LineHeader, FieldLabels());
    }

    private List<string> FieldLabels()
    {
        var labels = new List<string>(Session.Fields.Count);
        foreach (var target in Session.Fields)
        {
            labels.Add(Session.Targets[target].Label);
        }
        return labels;
    }

    // ---- Rows and counts ----

    /// <summary>The rows the filter shows: all rows not left out, or those with a problem (and those still glowing).</summary>
    private List<ImportReviewRow> ShownRows()
    {
        var rows = new List<ImportReviewRow>();
        foreach (var row in Session.Rows)
        {
            if (row.IsRemoved)
            {
                continue;
            }
            if (_filter.ShowAll || row.HasProblems || IsGlowing(row))
            {
                rows.Add(row);
            }
        }
        return rows;
    }

    private bool IsGlowing(ImportReviewRow row)
    {
        foreach (var key in _glows.Keys)
        {
            if (key.Row == row)
            {
                return true;
            }
        }
        return false;
    }

    private void UpdateCounts(bool instant)
    {
        instant |= !_countsShown;
        _countsShown = true;
        var issues = Session.IssueRows;
        var ready = Session.ReadyRows;
        var total = Session.TotalRows;
        var fixable = Session.FixableCells;
        _subtitle.IsInstant = instant;
        _subtitle.Set(Labels.ReviewCount(issues), issues, ready);
        _filter.SetCounts(issues, total, instant);
        if (fixable > 0)
        {
            // The button keeps its last words while it leaves ("Clean up 1 value").
            _cleanUp.SetCount(Labels.CleanUp(fixable), fixable, instant || !_cleanUp.IsShown);
        }
        _cleanUp.SetShown(fixable > 0, animate: !instant);
        // "left out" counts what the summary's "Left out" will: rows with problems and rows removed by hand (Arc's
        // footer counts only the rows with problems; our two places share one word, so they share one number).
        var skipped = Session.SkippedRows;
        _importWords.IsInstant = instant;
        _importWords.Set(Owner.WithNoun(Labels.ImportButton(ready), ready), ready, skipped);
        _skipWords.IsInstant = instant;
        _skipWords.Set(Owner.WithNoun(Labels.ImportButtonSkip(skipped), skipped), ready, skipped);
        AutomationProperties.SetName(_import, ImportText);
    }

    private void OnCountsChanged() => UpdateCounts(instant: false);

    private void OnFilterChanged(bool all) => _table.Sync(ShownRows(), animate: true);

    private void OnRowRemoved(ImportReviewRow row)
    {
        if (_editorRow?.Row == row)
        {
            CloseEditor(ImportReviewEditorClose.Escape, focusBack: false);
        }
        foreach (var key in _glows.Keys.Where(k => k.Row == row).ToList())
        {
            _glows.Remove(key);
            row.Fixed[key.Field] = false;
        }
        _table.Sync(ShownRows(), animate: true);
    }

    private void OnCellChanged(ImportReviewRow row, int field, ImportCellChange change)
    {
        var cell = _table.RowFor(row)?.Cells[field];
        if (cell is null)
        {
            row.Fixed[field] = false;
            return;
        }
        if (cell.Sync(animate: true))
        {
            StartGlow(row, field);
        }
        else if (row.Problems[field] is not null && _glows.Remove((row, field)))
        {
            row.Fixed[field] = false;
        }
    }

    // ---- Glow ----

    private void StartGlow(ImportReviewRow row, int field)
    {
        var serial = ++_glowSerial;
        _glows[(row, field)] = serial;
        _ = EndGlowLater(row, field, serial, _work.Token);
    }

    private async Task EndGlowLater(ImportReviewRow row, int field, int serial, CancellationToken token)
    {
        try
        {
            await Owner.Delay(ImportWizardMotion.FixGlow, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (token.IsCancellationRequested || !_glows.TryGetValue((row, field), out var current) || current != serial)
        {
            return;
        }
        _glows.Remove((row, field));
        row.Fixed[field] = false;
        // "All": the tint fades back. "To fix": the row leaves (with its tint) once nothing of it glows.
        _table.RowFor(row)?.Cells[field].EndGlow(animate: true);
        if (!_filter.ShowAll)
        {
            _table.Sync(ShownRows(), animate: true);
        }
    }

    // ---- Clean up ----

    /// <summary>Fixes every cell that has a safe fix: one every 90ms (the first at once), or all at once without full motion.</summary>
    internal async Task CleanUpAsync()
    {
        if (_cleaning)
        {
            return;
        }
        CloseEditor(ImportReviewEditorClose.Blur, focusBack: false);
        var cells = Session.FixableCellList();
        if (cells.Count == 0)
        {
            return;
        }
        _cleaning = true;
        var token = _work.Token;
        var paced = IsMotionFull;
        try
        {
            for (var i = 0; i < cells.Count; i++)
            {
                if (i > 0 && paced)
                {
                    await Owner.Delay(ImportWizardMotion.FixInterval, token);
                }
                if (token.IsCancellationRequested)
                {
                    return;
                }
                Session.CleanUp(cells[i].Row, cells[i].Field);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _cleaning = false;
        }
    }

    // ---- Leave out ----

    /// <summary>The x of a row: the row is left out of the import and leaves the table.</summary>
    internal void OnLeaveOutInvoked(ImportReviewRow row) => Session.Remove(row);

    /// <summary>Puts the focus on the table's region (when the last row with the focus left).</summary>
    internal void FocusTableRegion(NavigationMethod method) => _box.Viewer.Focus(method);

    // ---- Edit ----

    /// <summary>A cell was clicked or Enter / Space pressed on it: it opens its editor.</summary>
    internal void OnCellInvoked(ImportWizardReviewCell cell) => OpenEditor(cell);

    /// <summary>Opens the editor over <paramref name="cell"/> with the caret at the end of its value.</summary>
    internal void OpenEditor(ImportWizardReviewCell cell)
    {
        if (cell.IsEditing || _table.RowFor(cell.Row) is not { } row)
        {
            return;
        }
        CloseEditor(ImportReviewEditorClose.Blur, focusBack: false);
        if (_editor is null)
        {
            _editor = new ImportWizardReviewEditor();
            _editor.Closed += how => CloseEditor(how, focusBack: how != ImportReviewEditorClose.Blur);
        }
        var editor = _editor;
        _editorRow = row;
        _editorCell = cell;
        AutomationProperties.SetName(editor, Labels.EditorName(cell.FieldLabel, cell.Row.LineNumber));
        row.OpenEditor(editor, cell);
        editor.ApplyTemplate();
        editor.Begin(cell.Value);
        editor.Focus(NavigationMethod.Unspecified);
        // Focusing may have moved the caret; it belongs at the end (no select-all).
        editor.Begin(cell.Value);
        // A value wider than the cell scrolls to the caret once the field is laid out (like a browser input).
        Dispatcher.UIThread.Post(() =>
        {
            if (_editorCell == cell && editor.Text is { Length: > 0 } text)
            {
                editor.CaretIndex = 0;
                editor.CaretIndex = text.Length;
            }
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Closes the editor: Enter and Blur save, Escape discards; <paramref name="focusBack"/> focuses the cell again.</summary>
    private void CloseEditor(ImportReviewEditorClose how, bool focusBack)
    {
        if (_editor is not { } editor || _editorCell is not { } cell || _editorRow is not { } row)
        {
            return;
        }
        _editorCell = null;
        _editorRow = null;
        // Mark the edit closed before taking the focused editor out of the tree (that raises its LostFocus).
        editor.Close(how);
        var text = editor.Text ?? string.Empty;
        row.CloseEditor();
        if (focusBack)
        {
            cell.Focus(NavigationMethod.Tab);
        }
        if (how != ImportReviewEditorClose.Escape && text != cell.Row.Values[cell.Field] && !cell.Row.IsRemoved)
        {
            Session.Edit(cell.Row, cell.Field, text);
        }
    }
}
