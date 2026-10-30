using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class CodeSuggestion
{
    /// <summary>A new line opens to its row while it fades in (120ms).</summary>
    internal static readonly TimeSpan LineOpenDuration = TimeSpan.FromMilliseconds(120);

    /// <summary>A hunk's removed lines tint red, and a decided hunk's tint fades (240ms).</summary>
    internal static readonly TimeSpan TintDuration = TimeSpan.FromMilliseconds(240);

    /// <summary>Review bars open on a critically damped spring (0.24s, no bounce).</summary>
    internal static readonly SpringSpec BarOpenSpring = SpringSpec.FromVisual(0.24, 0);

    /// <summary>Accept and Reject fold lines and bars on the settle spring (0.26s, no bounce).</summary>
    internal static SpringSpec FoldSpring => AiGlyphMotion.Settle;

    private enum StatusIconKind
    {
        None,
        Spinner,
        Dot,
        Check,
        Cross,
        Alert
    }

    private readonly Dictionary<string, CodeSuggestionRow> _rowsByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CodeSuggestionHunkState> _states = new(StringComparer.Ordinal);
    private List<CodeSuggestionSpan> _spans = [];
    private string[] _lines = [""];
    private ArcGlyph? _mark;
    private TextBlock? _title;
    private TextBlock? _model;
    private ContentPresenter? _fileMark;
    private CodeSuggestionFileMark? _monogram;
    private TextBlock? _fileName;
    private TextBlock? _folder;
    private TextBlock? _addedCount;
    private TextBlock? _removedCount;
    private Control? _counts;
    private CodeSuggestionCodeArea? _codeArea;
    private ScrollViewer? _scroll;
    private CodeSuggestionView? _view;
    private ArcContentSwap? _statusIcon;
    private ArcContentSwap? _statusLabel;
    private ArcContentSwap? _actions;
    private ExplainerAnnouncer? _announcer;
    private StatusIconKind _iconKind;
    private string? _labelText;
    private bool _labelShimmers;
    private bool _labelIsError;
    private int _countAdded = -1;
    private int _countRemoved = -1;

    /// <summary>The drawn code (once the template is applied).</summary>
    internal CodeSuggestionView? View => _view;

    /// <summary>The focusable code area (once the template is applied).</summary>
    internal CodeSuggestionCodeArea? CodeArea => _codeArea;

    /// <summary>The live region (once the template is applied).</summary>
    internal ExplainerAnnouncer? Announcer => _announcer;

    /// <summary>The footer's current button set.</summary>
    internal CodeSuggestionActions? Actions => _actions?.Content as CodeSuggestionActions;

    /// <summary>The footer's status text.</summary>
    internal string? StatusText => _labelText;

    /// <summary>The hunks on screen in file order.</summary>
    internal IReadOnlyList<CodeSuggestionHunkState> HunkStates => _view?.Hunks ?? [];

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_codeArea is not null)
        {
            _codeArea.KeyDown -= OnCodeAreaKeyDown;
        }
        if (_scroll is not null)
        {
            _scroll.PropertyChanged -= OnScrollChanged;
        }
        if (_view is not null)
        {
            _view.DarkChanged -= OnDarkChanged;
            foreach (var state in _states.Values)
            {
                _view.RemoveHunk(state);
            }
        }
        _states.Clear();
        _rowsByKey.Clear();
        _mark = e.NameScope.Find<ArcGlyph>(MarkPart);
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _model = e.NameScope.Find<TextBlock>(ModelPart);
        _fileMark = e.NameScope.Find<ContentPresenter>(FileMarkPart);
        _monogram = e.NameScope.Find<CodeSuggestionFileMark>(MonogramPart);
        _fileName = e.NameScope.Find<TextBlock>(FileNamePart);
        _folder = e.NameScope.Find<TextBlock>(FolderPart);
        _addedCount = e.NameScope.Find<TextBlock>(AddedCountPart);
        _removedCount = e.NameScope.Find<TextBlock>(RemovedCountPart);
        _counts = e.NameScope.Find<Control>(CountsPart);
        _codeArea = e.NameScope.Find<CodeSuggestionCodeArea>(CodeAreaPart);
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollPart);
        _view = e.NameScope.Find<CodeSuggestionView>(ViewPart);
        _statusIcon = e.NameScope.Find<ArcContentSwap>(StatusIconPart);
        _statusLabel = e.NameScope.Find<ArcContentSwap>(StatusLabelPart);
        _actions = e.NameScope.Find<ArcContentSwap>(ActionsPart);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        if (_codeArea is not null)
        {
            _codeArea.KeyDown += OnCodeAreaKeyDown;
        }
        if (_scroll is not null)
        {
            _scroll.PropertyChanged += OnScrollChanged;
        }
        if (_view is not null)
        {
            _view.Rows.Clear();
            _view.DarkChanged += OnDarkChanged;
        }
        _iconKind = StatusIconKind.None;
        _labelText = null;
        _countAdded = -1;
        _countRemoved = -1;
        if (_statusIcon is not null)
        {
            _statusIcon.Content = null;
        }
        if (_statusLabel is not null)
        {
            _statusLabel.Content = null;
        }
        if (_actions is not null)
        {
            _actions.Content = null;
        }
        PseudoClasses.Set(ErrorPseudoClass, EffectiveStatus == CodeSuggestionStatus.Error);
        UpdateResponsive();
        UpdateTexts();
        Sync(animate: false);
    }

    private void OnScrollChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_scroll is null || _view is null)
        {
            return;
        }
        if (e.Property == ScrollViewer.OffsetProperty || e.Property == ScrollViewer.ViewportProperty || e.Property == BoundsProperty)
        {
            _view.SetViewport(_scroll.Offset.X, _scroll.Viewport.Width > 0 ? _scroll.Viewport.Width : _scroll.Bounds.Width);
        }
    }

    private void OnDarkChanged() => Sync(animate: false);

    /// <summary>The header, the tab and the names: everything that follows the properties rather than the review.</summary>
    private void UpdateTexts()
    {
        var labels = Labels;
        AutomationProperties.SetName(this, labels.CardName(Instruction));
        if (_title is not null)
        {
            _title.Text = Instruction;
        }
        var model = Model;
        if (_model is not null)
        {
            _model.Text = model?.Name;
            _model.IsVisible = model is not null && !string.IsNullOrEmpty(model.Name);
        }
        if (_mark is not null)
        {
            _mark.Data = model?.Mark ?? ArcIcons.Sparkles;
            if (model?.MarkBrush is { } brush)
            {
                _mark.Foreground = brush;
            }
            else
            {
                ArcTokens.Bind(_mark, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Foreground);
            }
        }
        if (_fileName is not null)
        {
            _fileName.Text = FileName;
        }
        if (_folder is not null)
        {
            var path = EffectiveFilePath;
            _folder.Text = path;
            _folder.IsVisible = !string.IsNullOrEmpty(path) && !IsCompact;
        }
        var monogram = CodeSuggestionDiff.MonogramOf(FileName);
        if (_fileMark is not null)
        {
            _fileMark.Content = ShowsFileMark ? FileMark : null;
            _fileMark.IsVisible = ShowsFileMark && FileMark is not null;
        }
        if (_monogram is not null)
        {
            _monogram.Text = monogram;
            _monogram.IsVisible = ShowsFileMark && FileMark is null && monogram.Length > 0;
        }
        if (_codeArea is not null)
        {
            AutomationProperties.SetName(_codeArea, labels.CodeName(FileName));
        }
    }

    /// <summary>
    /// Brings the rows, hunks and bars in line with the code, hunks, status and decisions. Rows are kept by line (an
    /// original line keeps its row when a hunk claims it, a new line by hunk and position), so what moves is only what
    /// changed: a new line opens, a claimed line tints, a decided hunk folds. <paramref name="animate"/> false jumps.
    /// </summary>
    private void Sync(bool animate)
    {
        var status = EffectiveStatus;
        PseudoClasses.Set(ErrorPseudoClass, status == CodeSuggestionStatus.Error);
        _lines = CodeSuggestionDiff.SplitLines(EffectiveCode);
        var showsHunks = status is CodeSuggestionStatus.Streaming or CodeSuggestionStatus.Ready;
        _spans = showsHunks ? CodeSuggestionDiff.Normalize(EffectiveHunks, _lines.Length) : [];
        var pending = 0;
        foreach (var span in _spans)
        {
            if (DecisionOf(span.Id) == CodeSuggestionDecision.Pending)
            {
                pending++;
            }
        }
        PendingCount = status == CodeSuggestionStatus.Ready ? pending : 0;
        if (_view is null)
        {
            return;
        }
        var view = _view;
        // A new run (thinking) starts from the plain file at once.
        animate = animate && IsLoaded && ArcMotion.IsFull(this) && status != CodeSuggestionStatus.Thinking;
        var streaming = status == CodeSuggestionStatus.Streaming;

        var firstInserted = new int[_spans.Count];
        var proposed = CodeSuggestionDiff.ApplyLines(_lines, _spans, _ => true, firstInserted);
        var language = CodeSuggestionDiff.LanguageOf(FileName);
        view.Original.Set(string.Join('\n', _lines), language, view.IsDark);
        view.Proposed.Set(string.Join('\n', proposed), language, view.IsDark);

        var rows = view.Rows;
        rows.Clear();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var state in _states.Values)
        {
            state.IsShown = false;
        }
        var cursor = 0;
        for (var s = 0; s < _spans.Count; s++)
        {
            var span = _spans[s];
            for (; cursor < span.Start; cursor++)
            {
                rows.Add(OriginalRow(cursor, CodeSuggestionRowKind.Context, null, animate, seen));
            }
            var state = StateOf(span, s + 1);
            for (var r = span.Start; r < span.End; r++)
            {
                rows.Add(OriginalRow(r, CodeSuggestionRowKind.Removed, state, animate, seen));
            }
            for (var k = 0; k < span.Insert.Count; k++)
            {
                rows.Add(AddedRow(span, state, k, firstInserted[s] + k, animate && streaming, seen));
            }
            state.BarAfter = rows.Count;
            cursor = span.End;
        }
        for (; cursor < _lines.Length; cursor++)
        {
            rows.Add(OriginalRow(cursor, CodeSuggestionRowKind.Context, null, animate, seen));
        }

        // Rows and hunks that left go at once (a new status or a new file, never a decision).
        if (_rowsByKey.Count > seen.Count)
        {
            foreach (var key in _rowsByKey.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                _rowsByKey.Remove(key);
            }
        }
        foreach (var state in _states.Values.Where(s => !s.IsShown).ToList())
        {
            _states.Remove(state.Id);
            view.RemoveHunk(state);
        }
        view.SortHunks();

        NumberRows();
        foreach (var row in rows)
        {
            ApplyRowTargets(row, animate);
        }
        foreach (var state in _states.Values)
        {
            var target = status == CodeSuggestionStatus.Ready && state.Decision == CodeSuggestionDecision.Pending ? 1.0 : 0.0;
            if (target != state.BarTarget)
            {
                state.BarTarget = target;
                if (animate)
                {
                    _ = state.BarOpen.AnimateTo(target, MotionSpec.Spring(target > 0 ? BarOpenSpring : FoldSpring));
                }
                else
                {
                    state.BarOpen.Jump(target);
                }
            }
            state.Bar.IsHitTestVisible = target > 0;
        }
        UpdateBars();
        UpdateCounts();
        UpdateAccessibleText();
        UpdateFooter();
        view.Refresh();
    }

    private CodeSuggestionRow OriginalRow(int line, CodeSuggestionRowKind kind, CodeSuggestionHunkState? state, bool animate, HashSet<string> seen)
    {
        var key = "o" + line;
        seen.Add(key);
        if (!_rowsByKey.TryGetValue(key, out var row))
        {
            row = NewRow(key);
            row.Open.Jump(1);
            row.Fade.Jump(1);
            row.Tint.Jump(0);
            row.OpenTarget = 1;
            row.TintTarget = 0;
            _rowsByKey[key] = row;
        }
        row.Kind = kind;
        row.Text = _lines[line];
        row.OriginalLine = line;
        row.ProposedLine = -1;
        row.Hunk = state;
        return row;
    }

    private CodeSuggestionRow AddedRow(CodeSuggestionSpan span, CodeSuggestionHunkState state, int index, int proposedLine, bool streams, HashSet<string> seen)
    {
        var key = "a" + span.Id + "\u0001" + index;
        seen.Add(key);
        if (!_rowsByKey.TryGetValue(key, out var row))
        {
            row = NewRow(key);
            row.Tint.Jump(1);
            row.TintTarget = 1;
            row.OpenTarget = 1;
            if (streams)
            {
                // A streamed line opens to its row while it fades in.
                row.Open.Jump(0);
                row.Fade.Jump(0);
                _ = row.Open.AnimateTo(1, MotionSpec.Tween(LineOpenDuration, ArcEasings.Standard));
                _ = row.Fade.AnimateTo(1, MotionSpec.Tween(LineOpenDuration, ArcEasings.Standard));
            }
            else
            {
                row.Open.Jump(1);
                row.Fade.Jump(1);
            }
            _rowsByKey[key] = row;
        }
        row.Kind = CodeSuggestionRowKind.Added;
        row.Text = span.Insert[index] ?? string.Empty;
        row.OriginalLine = -1;
        row.ProposedLine = proposedLine;
        row.Hunk = state;
        return row;
    }

    private CodeSuggestionRow NewRow(string key)
    {
        var view = _view!;
        var open = new MotionValue<double>(view, 1, MotionVectors.Double);
        var fade = new MotionValue<double>(view, 1, MotionVectors.Double);
        var tint = new MotionValue<double>(view, 0, MotionVectors.Double);
        open.Changed += _ => view.OnHeightMoved();
        fade.Changed += _ => view.InvalidateVisual();
        tint.Changed += _ => view.InvalidateVisual();
        return new CodeSuggestionRow(key, open, fade, tint);
    }

    private CodeSuggestionHunkState StateOf(CodeSuggestionSpan span, int number)
    {
        if (!_states.TryGetValue(span.Id, out var state))
        {
            var view = _view!;
            var bar = new CodeSuggestionReviewBar();
            var barOpen = new MotionValue<double>(view, 0, MotionVectors.Double);
            barOpen.Changed += _ => view.OnHeightMoved();
            state = new CodeSuggestionHunkState(span.Id, bar, barOpen);
            var id = span.Id;
            bar.AcceptPressed += (_, _) => DecideFromBar(id, CodeSuggestionDecision.Accepted);
            bar.RejectPressed += (_, _) => DecideFromBar(id, CodeSuggestionDecision.Rejected);
            _states[span.Id] = state;
            view.AddHunk(state);
        }
        state.Span = span;
        state.Number = number;
        state.Decision = DecisionOf(span.Id);
        state.IsShown = true;
        return state;
    }

    /// <summary>
    /// Where each row is headed: context rows are plain; a pending hunk shows its removed lines red and its new lines green;
    /// accepting folds the removed lines and lets the green fade, rejecting folds the new lines and lets the red fade.
    /// </summary>
    internal static (double Open, double Tint) TargetOf(CodeSuggestionRowKind kind, CodeSuggestionDecision decision) => kind switch
    {
        CodeSuggestionRowKind.Removed => decision switch
        {
            CodeSuggestionDecision.Accepted => (0, 1),
            CodeSuggestionDecision.Rejected => (1, 0),
            _ => (1, 1)
        },
        CodeSuggestionRowKind.Added => decision switch
        {
            CodeSuggestionDecision.Accepted => (1, 0),
            CodeSuggestionDecision.Rejected => (0, 1),
            _ => (1, 1)
        },
        _ => (1, 0)
    };

    private static void ApplyRowTargets(CodeSuggestionRow row, bool animate)
    {
        var (open, tint) = TargetOf(row.Kind, row.Hunk?.Decision ?? CodeSuggestionDecision.Pending);
        if (open != row.OpenTarget)
        {
            row.OpenTarget = open;
            if (animate)
            {
                _ = row.Open.AnimateTo(open, MotionSpec.Spring(FoldSpring));
            }
            else
            {
                row.Open.Jump(open);
                row.Fade.Jump(1);
            }
        }
        else if (!animate && !row.Open.IsAnimating && Math.Abs(row.Open.Value - open) > 0.001)
        {
            row.Open.Jump(open);
        }
        if (tint != row.TintTarget)
        {
            row.TintTarget = tint;
            if (animate)
            {
                _ = row.Tint.AnimateTo(tint, MotionSpec.Tween(TintDuration, ArcEasings.Standard));
            }
            else
            {
                row.Tint.Jump(tint);
            }
        }
    }

    /// <summary>
    /// Line numbers of the file as it now stands: context lines, pending removed lines (still in it), accepted new lines
    /// and rejected-hunk originals are numbered; pending new lines have none yet; a line folding away keeps its number.
    /// </summary>
    private void NumberRows()
    {
        var number = 1;
        foreach (var row in _view!.Rows)
        {
            var decision = row.Hunk?.Decision ?? CodeSuggestionDecision.Pending;
            var inFile = row.Kind switch
            {
                CodeSuggestionRowKind.Removed => decision != CodeSuggestionDecision.Accepted,
                CodeSuggestionRowKind.Added => decision == CodeSuggestionDecision.Accepted,
                _ => true
            };
            if (inFile)
            {
                row.Number = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
                number++;
            }
            else if (row.Kind == CodeSuggestionRowKind.Added && decision == CodeSuggestionDecision.Pending)
            {
                row.Number = null;
            }
        }
    }

    /// <summary>The bars' words, names and compact state.</summary>
    private void UpdateBars()
    {
        var labels = Labels;
        var compact = IsNarrow;
        foreach (var state in _states.Values)
        {
            var summary = SummaryOf(state);
            var bar = state.Bar;
            bar.Summary = summary;
            bar.AcceptText = labels.Accept;
            bar.RejectText = labels.Reject;
            bar.IsCompact = compact;
            bar.SetNames(labels.AcceptHunk(summary), labels.RejectHunk(summary));
        }
    }

    private string SummaryOf(CodeSuggestionHunkState state) =>
        string.IsNullOrWhiteSpace(state.Span.Hunk.Summary) ? Labels.Change(state.Number) : state.Span.Hunk.Summary!;

    /// <summary>The tab's counts: the lines the suggestion adds and removes, leaving out rejected hunks; zero turns muted.</summary>
    private void UpdateCounts()
    {
        var (added, removed) = CodeSuggestionDiff.Count(_spans, s => DecisionOf(s.Id) != CodeSuggestionDecision.Rejected);
        if (_counts is not null)
        {
            AutomationProperties.SetName(_counts, Labels.Counts(added, removed));
        }
        if (added != _countAdded && _addedCount is not null)
        {
            _addedCount.Text = "+" + added;
            ArcTokens.Bind(_addedCount, TextBlock.ForegroundProperty, added > 0 ? ArcDesignTokenKind.Success : ArcDesignTokenKind.TextMuted);
        }
        if (removed != _countRemoved && _removedCount is not null)
        {
            _removedCount.Text = "−" + removed;
            ArcTokens.Bind(_removedCount, TextBlock.ForegroundProperty, removed > 0 ? ArcDesignTokenKind.Danger : ArcDesignTokenKind.TextMuted);
        }
        _countAdded = added;
        _countRemoved = removed;
    }

    /// <summary>
    /// The code area's help text: the code as read aloud, each pending removed or added line starting with "Removed:" or
    /// "Added:", so the diff never relies on the tints.
    /// </summary>
    private void UpdateAccessibleText()
    {
        if (_codeArea is null || _view is null)
        {
            return;
        }
        var text = AccessibleCode();
        AutomationProperties.SetHelpText(_codeArea, text);
    }

    /// <summary>The code as a screen reader gets it (see <see cref="UpdateAccessibleText"/>).</summary>
    internal string AccessibleCode()
    {
        var labels = Labels;
        var builder = new StringBuilder();
        foreach (var row in _view?.Rows ?? [])
        {
            if (row.OpenTarget <= 0)
            {
                continue;
            }
            var pending = row.Hunk?.Decision is null or CodeSuggestionDecision.Pending;
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }
            if (pending && row.Kind == CodeSuggestionRowKind.Removed)
            {
                builder.Append(labels.Removed).Append(' ');
            }
            else if (pending && row.Kind == CodeSuggestionRowKind.Added)
            {
                builder.Append(labels.Added).Append(' ');
            }
            builder.Append(row.Text);
        }
        return builder.ToString();
    }

    /// <summary>The footer: the status icon (swapped only when its kind changes), its label and the button set.</summary>
    private void UpdateFooter()
    {
        var status = EffectiveStatus;
        var labels = Labels;
        var total = _spans.Count;
        var accepted = 0;
        var pending = 0;
        foreach (var span in _spans)
        {
            var decision = DecisionOf(span.Id);
            if (decision == CodeSuggestionDecision.Accepted)
            {
                accepted++;
            }
            else if (decision == CodeSuggestionDecision.Pending)
            {
                pending++;
            }
        }
        var (kind, text) = status switch
        {
            CodeSuggestionStatus.Thinking => (StatusIconKind.Spinner, labels.Reading(FileName)),
            CodeSuggestionStatus.Streaming => (StatusIconKind.Spinner, labels.Writing),
            CodeSuggestionStatus.Error => (StatusIconKind.Alert, ErrorText),
            _ when pending > 0 => (StatusIconKind.Dot, labels.ToReview(pending)),
            _ when total == 0 => (StatusIconKind.Check, labels.NoChanges),
            _ when accepted > 0 => (StatusIconKind.Check, labels.Applied(accepted, total)),
            _ => (StatusIconKind.Cross, labels.RejectedAll(total))
        };
        SetStatusIcon(kind);
        SetStatusLabel(text, kind == StatusIconKind.Spinner, status == CodeSuggestionStatus.Error);

        var sample = UsesSample;
        var actions = status switch
        {
            CodeSuggestionStatus.Thinking or CodeSuggestionStatus.Streaming =>
                AllowsStop || sample ? CodeSuggestionActionsKind.Stop : CodeSuggestionActionsKind.None,
            CodeSuggestionStatus.Error => AllowsRegenerate || sample ? CodeSuggestionActionsKind.Retry : CodeSuggestionActionsKind.None,
            _ when pending > 0 => CodeSuggestionActionsKind.Review,
            _ => AllowsRegenerate || sample ? CodeSuggestionActionsKind.Regenerate : CodeSuggestionActionsKind.None
        };
        SetActions(actions);
    }

    private void SetStatusIcon(StatusIconKind kind)
    {
        if (_statusIcon is null || kind == _iconKind)
        {
            return;
        }
        _iconKind = kind;
        Control icon;
        switch (kind)
        {
            case StatusIconKind.Spinner:
            {
                var spinner = new ArcSpinner { Width = 14, Height = 14 };
                ArcTokens.Bind(spinner, ArcSpinner.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
                icon = spinner;
                break;
            }
            case StatusIconKind.Dot:
            {
                var dot = new Ellipse { Width = 7, Height = 7, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                ArcTokens.Bind(dot, Shape.FillProperty, ArcDesignTokenKind.Accent);
                icon = new Panel { Width = 14, Height = 14, Children = { dot } };
                break;
            }
            default:
            {
                var glyph = new ArcGlyph
                {
                    Size = 14,
                    Data = kind switch
                    {
                        StatusIconKind.Check => ArcIcons.Check,
                        StatusIconKind.Cross => ArcIcons.X,
                        _ => ArcIcons.CircleAlert
                    }
                };
                ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, kind switch
                {
                    StatusIconKind.Check => ArcDesignTokenKind.Success,
                    StatusIconKind.Cross => ArcDesignTokenKind.TextMuted,
                    _ => ArcDesignTokenKind.Danger
                });
                icon = glyph;
                break;
            }
        }
        _statusIcon.Content = icon;
    }

    private void SetStatusLabel(string text, bool shimmers, bool isError)
    {
        if (_statusLabel is null)
        {
            return;
        }
        if (text == _labelText && isError == _labelIsError)
        {
            if (shimmers != _labelShimmers && _statusLabel.Content is ToolCallText current)
            {
                current.IsShimmering = shimmers;
            }
            _labelShimmers = shimmers;
            return;
        }
        _labelText = text;
        _labelShimmers = shimmers;
        _labelIsError = isError;
        var label = new ToolCallText { Text = text, IsShimmering = shimmers };
        ArcTokens.Bind(label, ToolCallText.ForegroundProperty, isError ? ArcDesignTokenKind.Danger : ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(label, ToolCallText.MutedForegroundProperty, ArcDesignTokenKind.TextMuted);
        _statusLabel.Content = label;
        AutomationProperties.SetName(_statusLabel, text);
    }

    private void SetActions(CodeSuggestionActionsKind kind)
    {
        if (_actions is null)
        {
            return;
        }
        var labels = Labels;
        if (_actions.Content is CodeSuggestionActions current && current.Kind == kind)
        {
            Fill(current);
            return;
        }
        var hadFocus = _actions.Content is CodeSuggestionActions old && old.IsKeyboardFocusWithin;
        if (_actions.Content is CodeSuggestionActions leaving)
        {
            leaving.Pressed -= OnActionPressed;
            leaving.IsHitTestVisible = false;
        }
        if (kind == CodeSuggestionActionsKind.None)
        {
            _actions.Content = null;
            if (hadFocus)
            {
                MoveFocus(_codeArea);
            }
            return;
        }
        var actions = new CodeSuggestionActions(kind);
        Fill(actions);
        actions.Pressed += OnActionPressed;
        _actions.Content = actions;
        if (hadFocus)
        {
            // The pressed button left with its state: focus the new set's first button once it has a template.
            Dispatcher.UIThread.Post(() =>
            {
                if (ReferenceEquals(_actions?.Content, actions))
                {
                    MoveFocus((IInputElement?)actions.FirstButton ?? _codeArea);
                }
            }, DispatcherPriority.Loaded);
        }

        void Fill(CodeSuggestionActions set)
        {
            set.IsStacked = IsNarrow;
            set.StopText = labels.Stop;
            set.RejectAllText = labels.RejectAll;
            set.AcceptAllText = labels.AcceptAll;
            set.HintText = OperatingSystem.IsMacOS() ? labels.AcceptAllHintMac : labels.AcceptAllHint;
            set.ShowsHint = AreShortcutsEnabled;
            set.RegenerateText = labels.Regenerate;
            set.RetryText = labels.Retry;
        }
    }

    private void OnActionPressed(object? sender, string part)
    {
        switch (part)
        {
            case CodeSuggestionActions.AcceptAllPart:
                AcceptAll();
                break;
            case CodeSuggestionActions.RejectAllPart:
                RejectAll();
                break;
            case CodeSuggestionActions.StopPart:
                PressStop();
                break;
            case CodeSuggestionActions.RegeneratePart:
            case CodeSuggestionActions.RetryPart:
                PressRegenerate();
                break;
        }
    }
}
