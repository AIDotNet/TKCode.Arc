using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using Avalonia.Media.Immutable;
using TKCode.Arc.Primitives;
using Avalonia.Automation.Provider;

namespace TKCode.Arc;

/// <summary>A file of the review and what the reviewer did to it.</summary>
internal sealed class CodeReviewFileState
{
    public CodeReviewFileState(CodeReviewFile file)
    {
        File = file;
        Lines = file.Lines.ToList();
    }

    public CodeReviewFile File { get; }

    /// <summary>The working diff (suggestions applied).</summary>
    public List<CodeReviewDiffLine> Lines { get; set; }

    public bool IsViewed { get; set; }

    public string Id => File.Id;
}

/// <summary>
/// The diff of one file: drawn line rows with the thread bands in the flow under their lines, the line numbers pinned
/// while the code scrolls sideways (wheel with Shift, a horizontal wheel or the bar under it), one line number per file
/// in the tab order (Up / Down / Home / End move between lines). Rows and bands are kept by line and thread id, so a
/// rewrite of the file only adds and removes what changed: replaced lines fold out while new ones grow in.
/// </summary>
internal sealed class CodeReviewDiffView : Grid
{
    private readonly ICodeReviewHost _host;
    private readonly CodeReviewFileState _file;
    private readonly Func<IEnumerable<CodeReviewThread>> _threads;
    private readonly StackPanel _lines = new();
    private readonly ScrollBar _bar;
    private readonly Dictionary<string, CodeReviewLineRow> _rows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CodeReviewBand> _threadBands = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CodeReviewBand> _composers = new(StringComparer.Ordinal);
    private readonly HashSet<Control> _leaving = new();
    private readonly CodeReviewHighlighter _highlighter;
    private List<CodeReviewRow> _model = new();
    private string? _tabStopId;
    private double _scrollX;
    private bool _compact;

    public CodeReviewDiffView(ICodeReviewHost host, CodeReviewFileState file, Func<IEnumerable<CodeReviewThread>> threads)
    {
        _host = host;
        _file = file;
        _threads = threads;
        RowDefinitions = new RowDefinitions("Auto,Auto");
        ClipToBounds = true;
        _bar = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Minimum = 0,
            IsVisible = false,
            AllowAutoHide = true,
            Margin = new Thickness(0, 0, 0, 0)
        };
        _bar.Scroll += (_, e) => SetScroll(e.NewValue);
        SetRow(_bar, 1);
        Children.Add(_lines);
        Children.Add(_bar);
        _highlighter = new CodeReviewHighlighter(ApplyTokens);
        Rebuild(animate: false);
    }

    public CodeReviewFileState File => _file;

    /// <summary>Completes when the lines have their colors.</summary>
    public Task WhenHighlighted => _highlighter.WhenHighlighted;

    /// <summary>The rows in display order (tests).</summary>
    internal IEnumerable<CodeReviewLineRow> Rows => _lines.Children.OfType<CodeReviewLineRow>().Where(r => !_leaving.Contains(r));

    /// <summary>The open thread and box bands' content (tests).</summary>
    internal IEnumerable<CodeReviewThreadView> ThreadViews =>
        _lines.Children.OfType<CodeReviewBand>().Where(b => !_leaving.Contains(b)).Select(b => b.Child).OfType<CodeReviewThreadView>();

    public double ScrollX => _scrollX;

    public bool IsCompact
    {
        get => _compact;
        set
        {
            if (_compact == value)
            {
                return;
            }
            _compact = value;
            foreach (var row in _rows.Values)
            {
                row.IsCompact = value;
            }
            foreach (var view in ThreadViews)
            {
                view.Refresh();
            }
            UpdateExtent();
        }
    }

    /// <summary>Re-reads the diff and the threads: new rows and bands are inserted, gone ones removed.</summary>
    public void Rebuild(bool animate, IReadOnlyCollection<string>? flash = null)
    {
        _model = CodeReviewDiff.Rows(_file.Lines);
        var desired = new List<Control>(_model.Count + 4);
        var seenRows = new HashSet<string>(StringComparer.Ordinal);
        var bandsByLine = new Dictionary<string, List<CodeReviewBand>>(StringComparer.Ordinal);
        var liveThreads = new HashSet<string>(StringComparer.Ordinal);

        foreach (var thread in _threads())
        {
            liveThreads.Add(thread.Id);
            if (!_threadBands.TryGetValue(thread.Id, out var band))
            {
                band = new CodeReviewBand { Child = CodeReviewThreadView.ForThread(_host, thread) };
                _threadBands[thread.Id] = band;
            }
            Add(bandsByLine, thread.AnchorLineId, band);
        }
        foreach (var gone in _threadBands.Keys.Where(k => !liveThreads.Contains(k)).ToList())
        {
            _threadBands.Remove(gone);
        }
        foreach (var (lineId, band) in _composers)
        {
            Add(bandsByLine, lineId, band);
        }

        var codeIndex = 0;
        foreach (var row in _model)
        {
            var id = row.Line.Id;
            seenRows.Add(id);
            var tokens = row.Kind == CodeReviewLineKind.Hunk ? null : _highlighter.TokensFor(codeIndex++, row.Line.Text);
            if (!_rows.TryGetValue(id, out var control))
            {
                control = new CodeReviewLineRow(_host.Palette, row, _host.Labels.LineName) { IsCompact = _compact, ScrollX = _scrollX };
                control.Update(row, tokens, _host.Labels.LineName);
                control.Invoked += OnInvoked;
                control.Navigate += OnNavigate;
                _rows[id] = control;
                if (animate)
                {
                    control.Enter();
                }
            }
            else
            {
                control.Update(row, tokens, _host.Labels.LineName);
            }
            if (flash is not null && flash.Contains(id))
            {
                control.Flash();
            }
            desired.Add(control);
            if (bandsByLine.TryGetValue(id, out var bands))
            {
                desired.AddRange(bands);
                bandsByLine.Remove(id);
            }
        }
        // Bands whose line is gone stay at the end rather than vanish.
        foreach (var bands in bandsByLine.Values)
        {
            desired.AddRange(bands);
        }

        foreach (var (id, row) in _rows.Where(p => !seenRows.Contains(p.Key)).ToList())
        {
            _rows.Remove(id);
            if (animate && _lines.Children.Contains(row))
            {
                _leaving.Add(row);
                row.Left += r =>
                {
                    _leaving.Remove(r);
                    _lines.Children.Remove(r);
                };
                row.Leave();
            }
            else
            {
                _lines.Children.Remove(row);
            }
        }

        Merge(desired);
        UpdateTabStop();
        Highlight();
        UpdateExtent();
    }

    private static void Add(Dictionary<string, List<CodeReviewBand>> map, string key, CodeReviewBand band)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = new List<CodeReviewBand>(1);
            map[key] = list;
        }
        list.Add(band);
    }

    // Puts the children in the desired order with as few moves as possible; leaving rows stay where they were.
    private void Merge(List<Control> desired)
    {
        var children = _lines.Children;
        var wanted = new HashSet<Control>(desired);
        var i = 0;
        foreach (var control in desired)
        {
            while (i < children.Count && !wanted.Contains(children[i]))
            {
                if (_leaving.Contains(children[i]))
                {
                    i++;
                }
                else
                {
                    children.RemoveAt(i);
                }
            }
            if (i < children.Count && ReferenceEquals(children[i], control))
            {
                i++;
                continue;
            }
            var at = children.IndexOf(control);
            if (at >= 0)
            {
                children.RemoveAt(at);
                if (at < i)
                {
                    i--;
                }
            }
            children.Insert(i, control);
            i++;
        }
        while (i < children.Count)
        {
            if (_leaving.Contains(children[i]))
            {
                i++;
            }
            else
            {
                children.RemoveAt(i);
            }
        }
    }

    // ---- Comment boxes ----

    /// <summary>Opens the comment box under the line, or closes it when it is open.</summary>
    public void ToggleComposer(CodeReviewLineRow row)
    {
        var id = row.Row.Line.Id;
        if (_composers.ContainsKey(id))
        {
            CloseComposer(id, focusLine: false);
            return;
        }
        var view = CodeReviewThreadView.Composer(_host, _file.Id, id, _host.Labels.LineLabel(row.Row.Number), row.Row.Line.Text);
        var band = CodeReviewBand.Opening(view);
        _composers[id] = band;
        row.IsActive = true;
        Rebuild(animate: true);
        view.FocusInput();
    }

    /// <summary>Closes the box under a line (Cancel, Escape, or the same line number again).</summary>
    public void CloseComposer(string lineId, bool focusLine)
    {
        if (!_composers.Remove(lineId, out var band))
        {
            return;
        }
        if (_rows.TryGetValue(lineId, out var row))
        {
            row.IsActive = false;
            if (focusLine)
            {
                row.IsTabStop = true;
                _tabStopId = lineId;
                UpdateTabStop();
                row.Focus(NavigationMethod.Tab);
            }
        }
        _leaving.Add(band);
        band.Collapsed += b =>
        {
            _leaving.Remove(b);
            _lines.Children.Remove(b);
        };
        band.IsExpanded = false;
    }

    /// <summary>The box under a line turned into a thread: the band now belongs to the thread.</summary>
    public void PromoteComposer(string lineId, CodeReviewThread thread)
    {
        if (!_composers.Remove(lineId, out var band))
        {
            return;
        }
        _threadBands[thread.Id] = band;
        if (_rows.TryGetValue(lineId, out var row))
        {
            row.IsActive = false;
        }
    }

    /// <summary>The view showing a thread, if it is in this file.</summary>
    public CodeReviewThreadView? ViewOf(CodeReviewThread thread) =>
        _threadBands.TryGetValue(thread.Id, out var band) ? band.Child as CodeReviewThreadView : null;

    /// <summary>The composer under a line, if open (tests).</summary>
    internal CodeReviewThreadView? ComposerAt(string lineId) =>
        _composers.TryGetValue(lineId, out var band) ? band.Child as CodeReviewThreadView : null;

    /// <summary>Re-reads every thread view (resolved states, labels, layout width).</summary>
    public void RefreshThreads()
    {
        foreach (var band in _threadBands.Values)
        {
            (band.Child as CodeReviewThreadView)?.Refresh();
        }
    }

    private void OnInvoked(CodeReviewLineRow row)
    {
        _tabStopId = row.Row.Line.Id;
        UpdateTabStop();
        ToggleComposer(row);
    }

    // ---- Keyboard ----

    private void OnNavigate(CodeReviewLineRow row, Key key)
    {
        var rows = Rows.Where(r => !r.IsHunk).ToList();
        var index = rows.IndexOf(row);
        if (index < 0 || rows.Count == 0)
        {
            return;
        }
        index = key switch
        {
            Key.Up => Math.Max(0, index - 1),
            Key.Down => Math.Min(rows.Count - 1, index + 1),
            Key.Home => 0,
            _ => rows.Count - 1
        };
        var target = rows[index];
        _tabStopId = target.Row.Line.Id;
        UpdateTabStop();
        target.Focus(NavigationMethod.Directional);
        target.BringIntoView();
    }

    private void UpdateTabStop()
    {
        if (_tabStopId is null || !_rows.TryGetValue(_tabStopId, out var stop) || stop.IsHunk)
        {
            stop = Rows.FirstOrDefault(r => !r.IsHunk);
            _tabStopId = stop?.Row.Line.Id;
        }
        foreach (var row in _rows.Values)
        {
            row.IsTabStop = ReferenceEquals(row, stop);
        }
    }

    // ---- Colors ----

    private void Highlight()
    {
        if (!IsLoaded && VisualRoot is null)
        {
            return;
        }
        var lines = new List<string>(_model.Count);
        foreach (var row in _model)
        {
            if (row.Kind != CodeReviewLineKind.Hunk)
            {
                lines.Add(row.Line.Text);
            }
        }
        _highlighter.Set(lines, _file.File.Language, CodeReviewUi.IsDark(this));
    }

    private void ApplyTokens()
    {
        var index = 0;
        foreach (var row in _model)
        {
            if (row.Kind == CodeReviewLineKind.Hunk)
            {
                continue;
            }
            if (_rows.TryGetValue(row.Line.Id, out var control))
            {
                control.Update(row, _highlighter.TokensFor(index, row.Line.Text), _host.Labels.LineName);
            }
            index++;
        }
        UpdateExtent();
    }

    /// <summary>The theme changed: redraw every row in the new colors.</summary>
    public void Repaint()
    {
        foreach (var row in _rows.Values)
        {
            row.Update(row.Row, row.Tokens, _host.Labels.LineName);
            row.InvalidateMeasure();
        }
        Highlight();
        UpdateExtent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Highlight();
    }

    // ---- Horizontal scroll ----

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) > 0.5)
        {
            UpdateExtent();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_bar.Maximum <= 0)
        {
            return;
        }
        var dx = e.Delta.X;
        if (Math.Abs(dx) < 0.001 && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            dx = e.Delta.Y;
        }
        if (Math.Abs(dx) < 0.001)
        {
            return;
        }
        e.Handled = true;
        SetScroll(_scrollX - dx * 50);
    }

    private void SetScroll(double value)
    {
        value = Math.Clamp(value, 0, Math.Max(0, _bar.Maximum));
        if (Math.Abs(value - _scrollX) < 0.01)
        {
            return;
        }
        _scrollX = value;
        _bar.Value = value;
        foreach (var row in _rows.Values)
        {
            row.ScrollX = value;
        }
    }

    private void UpdateExtent()
    {
        var width = Bounds.Width;
        if (width <= 0)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (Bounds.Width > 0)
                {
                    UpdateExtent();
                }
            }, DispatcherPriority.Background);
            return;
        }
        double widest = 0, gutter = 0;
        foreach (var row in _rows.Values)
        {
            if (!row.IsHunk)
            {
                widest = Math.Max(widest, row.ContentWidth);
                gutter = row.GutterWidth;
            }
        }
        var viewport = Math.Max(0, width - gutter);
        var max = Math.Max(0, widest - viewport);
        _bar.Maximum = max;
        _bar.ViewportSize = viewport;
        _bar.IsVisible = max > 0.5;
        if (_scrollX > max)
        {
            SetScroll(max);
        }
    }
}
