using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class DocumentExtraction
{
    /// <summary>The extra space above every group but the first (measured from Arc's form: about 11px).</summary>
    internal const double GroupGap = 11;

    private ExtractionStore? _store;
    private ExtractionSchema? _storeSchema;
    private ExtractionDocument? _shownDocument;
    private readonly Dictionary<string, ExtractionFieldRow> _rows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ExtractionTableGrid> _tables = new(StringComparer.Ordinal);
    private readonly List<Control?> _fieldViews = new();
    private ActionKind _actionKind = (ActionKind)(-1);
    private string? _actionKey;
    private string? _acceptKey;
    private readonly TokenBinding _flagIconColor = new();
    private readonly TokenBinding _flagTextColor = new();
    private readonly TokenBinding _jsonIconColor = new();

    /// <summary>What the form pane's button does now.</summary>
    private enum ActionKind
    {
        Extract,
        Stop,
        Again,
        Retry
    }

    /// <summary>A token bound to one property of one element, rebound only when the token changes.</summary>
    private sealed class TokenBinding
    {
        private IDisposable? _binding;
        private StyledElement? _target;
        private ArcDesignTokenKind _kind;

        public void Set(StyledElement target, AvaloniaProperty property, ArcDesignTokenKind kind)
        {
            if (ReferenceEquals(target, _target) && kind == _kind)
            {
                return;
            }
            _binding?.Dispose();
            _binding = ArcTokens.Bind(target, property, kind);
            _target = target;
            _kind = kind;
        }
    }

    /// <summary>The values (tests).</summary>
    internal ExtractionStore? Store => _store;

    #region Sources

    /// <summary>
    /// A new document or schema: a run that is going stops, the values are cleared (a new schema makes a new form) and the
    /// block is idle again.
    /// </summary>
    private void OnSourceChanged()
    {
        var document = EffectiveDocument;
        var schema = EffectiveSchema;
        SyncSources();
        var schemaChanged = !ReferenceEquals(schema, _storeSchema);
        if (!schemaChanged && ReferenceEquals(document, _shownDocument))
        {
            return;
        }
        if (_run is not null)
        {
            EndRun(ExtractionState.Stopped, null, announce: false);
        }
        _cascade = null;
        _shownDocument = document;
        if (schemaChanged)
        {
            CreateStore(schema);
        }
        ResetValues();
        _hasRun = false;
        ErrorMessage = null;
        State = ExtractionState.Idle;
        _hoverPath = null;
        SetCurrentValue(SelectedPathProperty, null);
        RefreshSchemaText();
        RefreshAll();
    }

    private void CreateStore(ExtractionSchema? schema)
    {
        if (_store is not null)
        {
            _store.Changed -= OnStoreChanged;
        }
        _storeSchema = schema;
        _store = schema is null ? null : new ExtractionStore(schema, () => ReviewThreshold) { Labels = Labels };
        if (_store is not null)
        {
            _store.Changed += OnStoreChanged;
        }
        _values.Clear();
        if (_store is not null)
        {
            _values.AddRange(_store.Values);
        }
        _pathSpan.Clear();
        _spanPaths.Clear();
        BuildForm();
        if (_json is not null)
        {
            _json.Tones = _store is null ? null : _store.Info.ToneOf;
        }
    }

    #endregion

    #region Form

    /// <summary>
    /// The Fields panel (spec 3.4): a section per group (its dot and title, 10 8 4 inside; a group that starts with a table
    /// has no title of its own, the table's header carries the dot) or per field without a group, 11px apart, holding the
    /// rows and the tables in schema order. Each section is an automation group named after its title.
    /// </summary>
    private void BuildForm()
    {
        _rows.Clear();
        _tables.Clear();
        _fieldViews.Clear();
        if (_form is null)
        {
            return;
        }
        _form.Children.Clear();
        if (_store is not { } store)
        {
            return;
        }
        var info = store.Info;
        var views = new Control?[info.Fields.Count];
        foreach (var section in info.Sections)
        {
            var tone = ExtractionSchemaInfo.ToneOfSection(section.Index);
            var group = new StackPanel();
            if (_form.Children.Count > 0)
            {
                group.Margin = new Thickness(0, GroupGap, 0, 0);
            }
            if (section.Group is { } title)
            {
                AutomationProperties.SetName(group, title.Title);
                AutomationProperties.SetControlTypeOverride(group, AutomationControlType.Group);
                if (section.Fields.Count > 0 && section.Fields[0] is not ExtractionTableField)
                {
                    group.Children.Add(GroupTitle(title.Title, tone));
                }
            }
            foreach (var field in section.Fields)
            {
                Control view = field is ExtractionTableField table ? CreateTable(store, table, tone) : CreateRow(store, field, tone);
                group.Children.Add(view);
                var index = info.IndexOf(field);
                if (index >= 0 && views[index] is null)
                {
                    views[index] = view;
                }
            }
            _form.Children.Add(group);
        }
        _fieldViews.AddRange(views);
        ApplyFormLabels();
    }

    /// <summary>A group's title: a 7px dot in the group color, 8px, the title (12 / 16.8 medium, TextSecondary), 10 8 4 inside.</summary>
    private static Control GroupTitle(string title, int tone)
    {
        var dot = new Ellipse { Width = 7, Height = 7, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(dot, Shape.FillProperty, SeriesKind(tone));
        var text = new TextBlock
        {
            Text = title,
            FontWeight = FontWeight.Medium,
            LineHeight = 16.8,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        ArcTokens.Bind(text, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(8, 10, 8, 4),
            Children = { dot, text }
        };
        // The section is named after the title already (Arc: the visible title is aria-hidden).
        AutomationProperties.SetAccessibilityView(panel, AccessibilityView.Raw);
        AutomationProperties.SetAccessibilityView(text, AccessibilityView.Raw);
        return panel;
    }

    internal static ArcDesignTokenKind SeriesKind(int tone) => tone switch
    {
        1 => ArcDesignTokenKind.Series1,
        2 => ArcDesignTokenKind.Series2,
        3 => ArcDesignTokenKind.Series3,
        _ => ArcDesignTokenKind.Series4
    };

    private ExtractionFieldRow CreateRow(ExtractionStore store, ExtractionField field, int tone)
    {
        var row = new ExtractionFieldRow
        {
            Value = store.FieldValue(field.Key),
            Label = field.Label,
            Tone = tone,
            FieldType = field.Type,
            AutomationName = field.Label,
            IsNarrow = _isNarrow
        };
        _rows.TryAdd(field.Key, row);
        return row;
    }

    private ExtractionTableGrid CreateTable(ExtractionStore store, ExtractionTableField field, int tone)
    {
        var grid = new ExtractionTableGrid
        {
            Label = field.Label,
            Tone = tone,
            FieldKey = field.Key,
            Columns = field.Columns,
            Rows = store.TableRows(field.Key),
            SummaryValue = store.FieldValue(field.Key),
            IsNarrow = _isNarrow
        };
        _tables.TryAdd(field.Key, grid);
        return grid;
    }

    /// <summary>The words of every row and table.</summary>
    private void ApplyFormLabels()
    {
        if (_store is not { } store)
        {
            return;
        }
        var labels = Labels;
        foreach (var field in store.Info.Fields)
        {
            if (_rows.TryGetValue(field.Key, out var row))
            {
                row.TypeName = labels.TypeName(field.Type);
                row.EditedText = labels.Edited;
                row.LooksRightText = labels.LooksRight;
            }
            else if (_tables.TryGetValue(field.Key, out var table))
            {
                table.TableText = labels.Table;
                table.EmptyText = labels.NoRows;
                table.PendingText = labels.RowsPending;
                table.EditedText = labels.Edited;
                table.CellAutomationName = labels.CellName;
            }
        }
    }

    #endregion

    #region Values

    /// <summary>
    /// The values changed (once per dispatcher turn, see <see cref="ExtractionStore.Changed"/>): the counts, the output, the
    /// tints of the cited text and every readout follow.
    /// </summary>
    private void OnStoreChanged(ExtractionStoreChange change)
    {
        if (_store is not { } store)
        {
            return;
        }
        _values.Clear();
        _values.AddRange(store.Values);
        FoundCount = store.FoundCount;
        FieldCount = store.FieldCount;
        CheckCount = store.CheckCount;
        AverageConfidence = store.AverageConfidence;
        Output = store.BuildOutput();
        if (change.Cleared)
        {
            _pathSpan.Clear();
            _spanPaths.Clear();
            _spanPaints.Clear();
            _viewer?.ClearSpans();
        }
        UpdateSpans(change.Paths);
        if (State == ExtractionState.Accepted && _cascade is null && !store.AllAccepted)
        {
            // An accepted value was edited: only it went back, and Accept all is offered again (spec 4.1).
            State = _stateBeforeAccept;
        }
        RefreshAll();
        if (LinkedPath is { } linked && change.Paths.Contains(linked))
        {
            UpdateLink();
        }
    }

    /// <summary>Every readout from the state and the values.</summary>
    private void RefreshAll()
    {
        RefreshFieldTexts();
        RefreshStatus();
        RefreshSummary();
        RefreshBars();
        RefreshJson();
        RefreshButtons();
        RefreshMeta();
    }

    /// <summary>The rows' and tables' spoken confidence, row counts and running state.</summary>
    private void RefreshFieldTexts()
    {
        var labels = Labels;
        var running = IsRunning;
        foreach (var row in _rows.Values)
        {
            row.IsRunning = running;
            row.ConfidenceAutomationText = row.Value is { } value ? ConfidenceName(labels, value) : null;
        }
        foreach (var table in _tables.Values)
        {
            table.IsRunning = running;
            var count = table.Rows?.Count ?? 0;
            table.RowsText = count > 0 ? labels.Rows(count) : null;
            table.SummaryAutomationText = table.SummaryValue is { } summary ? ConfidenceName(labels, summary) : null;
        }
    }

    private static string ConfidenceName(DocumentExtractionLabels labels, ExtractionValue value) =>
        labels.ConfidenceName(value.Status, value.NeedsCheck, (int)Math.Round(value.Confidence * 100));

    /// <summary>The status line (spec 3.4, 4.1): what the run is doing, or how it ended, in the form's own words.</summary>
    private void RefreshStatus()
    {
        if (_status is null)
        {
            return;
        }
        var labels = Labels;
        var found = _store?.FoundFieldCount ?? 0;
        var total = _store?.TotalFieldCount ?? 0;
        var check = _store?.CheckCount ?? 0;
        string? count = null;
        string? detail = null;
        AiRunStatusKind kind;
        string text;
        switch (State)
        {
            case ExtractionState.Reading:
                kind = AiRunStatusKind.Running;
                text = labels.Reading;
                count = labels.Count(found, total);
                break;
            case ExtractionState.Extracting:
                kind = AiRunStatusKind.Running;
                text = labels.Extracting;
                count = labels.Count(found, total);
                break;
            case ExtractionState.Stopped:
                kind = AiRunStatusKind.Stopped;
                text = labels.Stopped(found, total);
                detail = labels.StoppedNote;
                break;
            case ExtractionState.Failed:
                kind = AiRunStatusKind.Failed;
                text = found > 0 ? labels.FailedAfter(found, total) : labels.Failed;
                detail = string.IsNullOrEmpty(ErrorMessage) ? null : ErrorMessage;
                break;
            case ExtractionState.Accepted:
                kind = AiRunStatusKind.Accepted;
                text = labels.AcceptedStatus;
                break;
            case ExtractionState.Accepting:
                kind = AiRunStatusKind.Done;
                text = labels.Found(found, total, labels.Seconds(Elapsed));
                break;
            case ExtractionState.Done when check > 0:
                kind = AiRunStatusKind.NeedsCheck;
                text = labels.NeedsCheck(check);
                break;
            case ExtractionState.Done:
                kind = AiRunStatusKind.Done;
                text = labels.Found(found, total, labels.Seconds(Elapsed));
                break;
            default:
                kind = AiRunStatusKind.Idle;
                text = _shownDocument?.EffectiveKind == DocumentKind.Email ? labels.FieldsToFillFromEmail(total) : labels.FieldsToFill(total);
                break;
        }
        _status.Kind = kind;
        _status.Text = text;
        _status.CountText = count;
        _status.DetailText = detail;
    }

    /// <summary>
    /// The footer summary (spec 3.6, 4.1): "No values yet" / "Reading" before any value; then the average and a flag:
    /// "⚠ n to check", "✓ Ready" (never while running or after a failure), "Accepting" or "✓ All accepted".
    /// </summary>
    private void RefreshSummary()
    {
        var labels = Labels;
        var found = _store?.FoundCount ?? 0;
        var check = _store?.CheckCount ?? 0;
        if (_summaryNote is not null)
        {
            _summaryNote.IsVisible = found == 0;
            _summaryNote.Text = State is ExtractionState.Reading or ExtractionState.Extracting ? labels.ReadingShort : labels.NoValues;
        }
        var average = _store?.AverageConfidence ?? 0;
        if (_averageGroup is not null)
        {
            _averageGroup.IsVisible = found > 0 && average > 0;
        }
        if (found > 0 && average > 0)
        {
            RollAverage(average);
        }
        if (_flag is null || _flagText is null || _flagIcon is null)
        {
            return;
        }
        (Geometry? icon, ArcDesignTokenKind color, string? text) = State switch
        {
            _ when found == 0 => (null, ArcDesignTokenKind.Foreground, null),
            ExtractionState.Accepting => (null, ArcDesignTokenKind.TextMuted, labels.Accepting),
            ExtractionState.Accepted => (ArcIcons.Check, ArcDesignTokenKind.Success, labels.AllAccepted),
            _ when check > 0 => (ArcIcons.TriangleAlert, ArcDesignTokenKind.Warning, labels.ToCheck(check)),
            ExtractionState.Reading or ExtractionState.Extracting or ExtractionState.Failed => (null, ArcDesignTokenKind.Foreground, null),
            _ => (ArcIcons.Check, ArcDesignTokenKind.Success, labels.Ready)
        };
        _flag.IsVisible = text is not null;
        _flagText.Text = text;
        _flagIcon.IsVisible = icon is not null;
        _flagIcon.Data = icon;
        _flagIconColor.Set(_flagIcon, ArcGlyph.ForegroundProperty, color);
        _flagTextColor.Set(_flagText, TextBlock.ForegroundProperty,
            State == ExtractionState.Accepting ? ArcDesignTokenKind.TextMuted : ArcDesignTokenKind.Foreground);
    }

    /// <summary>One bar per field (spec 3.6): pending, sure, to check, accepted (the cascade reaches them one by one).</summary>
    private void RefreshBars()
    {
        if (_bars is null)
        {
            return;
        }
        if (_store is not { } store)
        {
            _bars.Items = null;
            return;
        }
        var values = store.FieldValues;
        var items = new ConfidenceBarItem[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            items[i] = BarOf(values[i], _cascade is { } cascade && i < cascade.Reached);
        }
        _bars.Items = items;
    }

    private static ConfidenceBarItem BarOf(ExtractionValue value, bool accepted)
    {
        if (value.Status == ExtractionValueStatus.Pending)
        {
            return new ConfidenceBarItem(null, ConfidenceBarState.Pending);
        }
        // A value the person typed has no confidence: a full bar.
        double? confidence = value.Confidence > 0 ? value.Confidence : null;
        var state = accepted || value.Status == ExtractionValueStatus.Accepted ? ConfidenceBarState.Accepted
            : value.NeedsCheck ? ConfidenceBarState.Check
            : ConfidenceBarState.Ok;
        return new ConfidenceBarItem(confidence, state);
    }

    /// <summary>The JSON panel: the output, its note (valid, or how many values were set to null) and the copied text.</summary>
    private void RefreshJson()
    {
        if (_json is not null)
        {
            _json.Output = Output;
            if (_jsonCopy is not null)
            {
                _jsonCopy.Value = _json.Text;
            }
        }
        var invalid = _store?.InvalidValues.Count ?? 0;
        if (_jsonNote is not null)
        {
            _jsonNote.Text = invalid == 0 ? Labels.JsonValid : Labels.JsonInvalid(invalid);
        }
        if (_jsonNoteIcon is not null)
        {
            _jsonNoteIcon.Data = invalid == 0 ? ArcIcons.CircleCheck : ArcIcons.TriangleAlert;
            _jsonIconColor.Set(_jsonNoteIcon, ArcGlyph.ForegroundProperty, invalid == 0 ? ArcDesignTokenKind.Success : ArcDesignTokenKind.Warning);
        }
    }

    /// <summary>The Schema panel: the JSON schema sent to the model and the note naming the model.</summary>
    private void RefreshSchemaText()
    {
        var text = _storeSchema?.ToJsonSchema(Model?.Id);
        if (_schema is not null)
        {
            _schema.SchemaText = text;
        }
        if (_schemaCopy is not null)
        {
            _schemaCopy.Value = text;
        }
        if (_schemaNote is not null)
        {
            var id = Model?.Id;
            _schemaNote.Text = string.IsNullOrEmpty(id) ? null : Labels.SchemaNote(id);
        }
    }

    /// <summary>The footer's model, cost and time.</summary>
    private void RefreshMeta()
    {
        if (_meta is null)
        {
            return;
        }
        _meta.ModelName = Model?.Name;
        if (_costShown is null || !_costShown.IsAnimating)
        {
            ShowCost(Cost);
        }
        RefreshElapsedText();
    }

    #endregion

    #region Buttons

    /// <summary>
    /// The form pane's button (spec 3.4): Extract (primary, scan-text), Stop (secondary, a filled square), Extract again
    /// (secondary, rotate-ccw; narrow: "Extract" alone) or Retry (primary, rotate-ccw); a new label crossfades (the
    /// button's own label morph). Accept all: primary; spinning while saving; "✓ Accepted" (secondary, disabled) after.
    /// </summary>
    private void RefreshButtons()
    {
        var labels = Labels;
        if (_action is not null)
        {
            var kind = State switch
            {
                ExtractionState.Reading or ExtractionState.Extracting => ActionKind.Stop,
                ExtractionState.Failed => ActionKind.Retry,
                ExtractionState.Idle => ActionKind.Extract,
                _ => ActionKind.Again
            };
            var text = kind switch
            {
                ActionKind.Stop => labels.Stop,
                ActionKind.Retry => labels.Retry,
                ActionKind.Again => _isNarrow || _actionShort ? labels.ExtractAgainShort : labels.ExtractAgain,
                _ => labels.Extract
            };
            var key = $"{kind}|{_isNarrow || _actionShort}|{text}";
            if (key != _actionKey)
            {
                _actionKey = key;
                _actionKind = kind;
                _action.Content = ActionContent(kind, text);
                // The short "Extract" keeps its full name for screen readers (the visible word is part of it).
                AutomationProperties.SetName(_action, kind == ActionKind.Again ? labels.ExtractAgain : text);
            }
            _action.Variant = kind is ActionKind.Extract or ActionKind.Retry ? ButtonVariant.Primary : ButtonVariant.Secondary;
            _action.IsEnabled = kind == ActionKind.Stop || (Extractor is not null && EffectiveDocument is not null && _store is not null);
        }
        if (_acceptAll is not null)
        {
            var accepted = State == ExtractionState.Accepted;
            var text = accepted ? labels.Accepted : labels.AcceptAll;
            var key = $"{accepted}|{text}";
            if (key != _acceptKey)
            {
                _acceptKey = key;
                _acceptAll.Content = accepted ? IconLabel(ArcIcons.Check, text, 15) : text;
                AutomationProperties.SetName(_acceptAll, text);
            }
            _acceptAll.Variant = accepted ? ButtonVariant.Secondary : ButtonVariant.Primary;
            _acceptAll.IsLoading = State == ExtractionState.Accepting;
            _acceptAll.IsEnabled = State == ExtractionState.Accepting || CanAccept;
        }
    }

    /// <summary>Whether Accept all can run: values are found and no run, save or failure is in the way.</summary>
    private bool CanAccept =>
        _store is { FoundCount: > 0 } && State is not (ExtractionState.Reading or ExtractionState.Extracting or ExtractionState.Failed
            or ExtractionState.Accepting or ExtractionState.Accepted);

    private object ActionContent(ActionKind kind, string text) => kind switch
    {
        ActionKind.Extract => IconLabel(ArcIcons.ScanText, text, 15),
        ActionKind.Stop => StopLabel(text),
        ActionKind.Again when _isNarrow || _actionShort => text,
        _ => IconLabel(ArcIcons.RotateCcw, text, 15)
    };

    /// <summary>An icon (stroke 1.75) and a label, 8 apart.</summary>
    private static Control IconLabel(Geometry icon, string text, double size) => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Spacing = 8,
        Children =
        {
            new ArcGlyph { Data = icon, Size = size, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center },
            new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center }
        }
    };

    /// <summary>Stop: an 11px filled square in the text color, 8px, the label.</summary>
    private Control StopLabel(string text)
    {
        var square = new Rectangle { Width = 11, Height = 11, RadiusX = 1.5, RadiusY = 1.5, VerticalAlignment = VerticalAlignment.Center };
        if (_action is not null)
        {
            square.Bind(Shape.FillProperty, _action.GetObservable(ForegroundProperty));
        }
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { square, new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center } }
        };
    }

    private void OnActionClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_actionKind == ActionKind.Stop)
        {
            Stop();
            return;
        }
        _ = StartAsync();
        if (ExtractCommand is { } command && command.CanExecute(EffectiveDocument))
        {
            command.Execute(EffectiveDocument);
        }
    }

    #endregion
}
