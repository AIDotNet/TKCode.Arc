// Ported from vscode (src/vs/editor/contrib/gotoSymbol/browser/peek/referencesWidget.ts: ReferenceWidget with its
// preview editor, DecorationsManager and layout; src/vs/editor/contrib/gotoSymbol/browser/peek/referencesTree.ts: the
// file and reference rows; commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the preview is a read-only CodeEditor showing a copy of the document's text (the editor's own, or what the host
//    resolves); upstream shows the document's model itself, editable; edits made while the peek is open are not shown;
//  - the result list is a list of rows (file rows when there is more than one file, then their references), not a tree:
//    the files cannot be collapsed; Up / Down / Enter / Escape work while the list has the focus;
//  - the layout ratio (70 % preview) is fixed: no sash; layout data is not stored;
//  - the reference decorations in the preview are 'reference-decoration' in the FindMatch theme color.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.GotoSymbol;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

/// <summary>What the user did with a reference in the peek view (upstream's SelectionEvent kinds).</summary>
internal enum ReferenceSelectKind
{
    /// <summary>Selected it: the preview shows it.</summary>
    Show,

    /// <summary>Opened it from the list (Enter, double click).</summary>
    Goto,

    /// <summary>Opened it from the preview (double click).</summary>
    Open,

    /// <summary>Opened it to the side.</summary>
    Side,
}

/// <summary>The peek view of references and definitions: a preview of the selected one and the list of all (upstream's ReferenceWidget).</summary>
internal sealed class ReferencesWidget : PeekViewWidget
{
    private const string ReferenceDecorationClassName = "reference-decoration";

    private readonly Grid _body;
    private readonly CodeEditor _preview;
    private readonly ScrollViewer _listScroll;
    private readonly StackPanel _list;
    private readonly Border _listHost;
    private readonly TextBlock _messageText;
    private readonly List<(OneReference? Reference, FileReferences? File, Border Row)> _rows = new();
    private readonly List<IDisposable> _previewStyles = new();
    private ReferencesModel? _model;
    private OneReference? _selected;
    private IDisposable? _selectionBinding;
    private string? _previewUri;
    private string[] _previewDecorations = [];
    private TextRange? _pendingReveal;
    private bool _isClosing;

    public ReferencesWidget(CodeEditor editor)
        : base(editor, new ZoneWidgetOptions { ShowFrame = true, FrameWidth = 1, ShowArrow = true, KeepEditorSelection = true })
    {
        _preview = new CodeEditor
        {
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            FontFamily = editor.FontFamily,
            FontSize = editor.FontSize,
            MinHeight = 0,
            Focusable = true,
        };
        // the preview is an embedded editor inside peek (its own peek commands are off)
        _preview.CreateContextKey(PeekContext.InPeekEditor).Set(true);
        _preview.CreateContextKey(EditorContextKeys.IsInEmbeddedEditor).Set(true);
        Surface.BindBrush(_preview, Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty, CodeEditorPeekSurface.PreviewBackgroundProperty);
        _previewStyles.Add(_preview.DecorationStyles.Register(ReferenceDecorationClassName, new CodeEditorDecorationStyle { Background = CodeEditorThemeColor.FindMatch }));
        _preview.DidChangeViewModel += _ => RevealPending();
        _preview.MouseDown += OnPreviewMouseDown;

        _list = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 2) };
        _listScroll = new ScrollViewer { Content = _list, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        _listHost = new Border { Child = _listScroll, Focusable = true };
        Surface.BindBrush(_listHost, Border.BackgroundProperty, CodeEditorPeekSurface.ResultsBackgroundProperty);
        _listHost.KeyDown += OnListKeyDown;

        _messageText = new TextBlock { Margin = new Thickness(12), IsVisible = false, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

        var separator = new Border { Width = 1 };
        Surface.BindBrush(separator, Border.BackgroundProperty, Border.BorderBrushProperty);
        _body = new Grid { ColumnDefinitions = new ColumnDefinitions("7*,Auto,3*") };
        _body.Children.Add(_preview);
        Grid.SetColumn(separator, 1);
        _body.Children.Add(separator);
        Grid.SetColumn(_listHost, 2);
        _body.Children.Add(_listHost);
        _body.Children.Add(_messageText);
        Grid.SetColumnSpan(_messageText, 3);
        Body.Child = _body;
    }

    /// <summary>Raised when the user selects or opens a reference.</summary>
    public event Action<OneReference, ReferenceSelectKind>? SelectReference;

    /// <summary>Raised when the widget closes itself (the close button).</summary>
    public event Action? Closed;

    public bool IsClosing => _isClosing;

    /// <summary>The preview editor (for tests).</summary>
    internal CodeEditor Preview => _preview;

    /// <summary>The selected reference.</summary>
    internal OneReference? Selected => _selected;

    /// <summary>The rows of the list: references and file headers (for tests).</summary>
    internal IReadOnlyList<(OneReference? Reference, FileReferences? File, Border Row)> Rows => _rows;

    /// <summary>The list (it takes the keyboard focus).</summary>
    internal Control List => _listHost;

    public override void Dispose()
    {
        _isClosing = true;
        _preview.MouseDown -= OnPreviewMouseDown;
        foreach (var style in _previewStyles)
        {
            style.Dispose();
        }

        base.Dispose();
    }

    /// <summary>Shows the references of <paramref name="model"/> (null: none); the previews are resolved first (upstream's setModel).</summary>
    public async Task SetModel(ReferencesModel? model)
    {
        _model = model;
        _selected = null;
        _rows.Clear();
        _list.Children.Clear();
        if (model is null)
        {
            return;
        }

        if (model.IsEmpty)
        {
            SetTitle(string.Empty);
            _messageText.Text = "No results";
            _messageText.IsVisible = true;
            _preview.IsVisible = false;
            _listHost.IsVisible = false;
            return;
        }

        _messageText.IsVisible = false;
        _preview.IsVisible = true;
        _listHost.IsVisible = true;
        foreach (var group in model.Groups)
        {
            await group.Resolve(uri => Editor.ResolveTextModel(uri)).ConfigureAwait(true);
        }

        if (!ReferenceEquals(_model, model))
        {
            return;
        }

        var showFiles = model.Groups.Count > 1;
        foreach (var group in model.Groups)
        {
            if (showFiles)
            {
                AddRow(null, group, CreateFileRow(group));
            }

            foreach (var reference in group.Children)
            {
                AddRow(reference, null, CreateReferenceRow(reference, indent: showFiles));
            }
        }
    }

    /// <summary>Selects <paramref name="selection"/> in the list and shows it in the preview (upstream's setSelection).</summary>
    public Task SetSelection(OneReference selection)
    {
        var index = _rows.FindIndex(r => ReferenceEquals(r.Reference, selection));
        if (index >= 0)
        {
            SelectRow(index, reveal: true);
        }

        return RevealReference(selection);
    }

    /// <summary>Shows <paramref name="reference"/> in the preview and its file in the title (upstream's _revealReference).</summary>
    public async Task RevealReference(OneReference reference)
    {
        var uri = reference.Uri;
        SetTitle(ReferencesModel.Basename(uri), DirName(uri));

        var key = uri.ToString();
        if (_previewUri != key)
        {
            _previewUri = key;
            _previewDecorations = _preview.Model.DeltaDecorations(_previewDecorations, []);
            if (await Editor.ResolveTextModel(uri).ConfigureAwait(true) is { } resolved)
            {
                // the preview shows a copy of the text
                if (_previewUri == key)
                {
                    _preview.LanguageId = resolved.Model.GetLanguageId();
                    _preview.Text = resolved.Model.GetValue();
                }

                if (resolved.Owned)
                {
                    resolved.Model.Dispose();
                }
            }
            else if (_previewUri == key)
            {
                _preview.Text = string.Empty;
            }
        }

        if (!ReferenceEquals(_selected, reference))
        {
            var index = _rows.FindIndex(r => ReferenceEquals(r.Reference, reference));
            if (index >= 0)
            {
                SelectRow(index, reveal: true);
            }
        }

        UpdatePreviewDecorations(reference.Parent);
        _pendingReveal = reference.Range;
        RevealPending();
    }

    /// <summary>Gives the list the keyboard focus (upstream's focusOnReferenceTree).</summary>
    public void FocusOnReferenceTree() => _listHost.Focus();

    /// <summary>Gives the preview the keyboard focus (upstream's focusOnPreviewEditor).</summary>
    public void FocusOnPreviewEditor() => _preview.Focus();

    public bool IsPreviewEditorFocused() => _preview.IsKeyboardFocusWithin;

    /// <inheritdoc />
    protected override void OnCloseRequested()
    {
        _isClosing = true;
        Closed?.Invoke();
    }

    /// <inheritdoc />
    protected override void DoLayoutBody(double heightInPixel)
    {
        _body.Height = heightInPixel;
    }

    private void AddRow(OneReference? reference, FileReferences? file, Border row)
    {
        var index = _rows.Count;
        _rows.Add((reference, file, row));
        _list.Children.Add(row);
        if (reference is null)
        {
            return;
        }

        row.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            var currentIndex = _rows.FindIndex(r => ReferenceEquals(r.Row, row));
            SelectRow(currentIndex, reveal: false);
            _listHost.Focus();
            if (e.ClickCount >= 2)
            {
                SelectReference?.Invoke(reference, ReferenceSelectKind.Goto);
            }
            else
            {
                SelectReference?.Invoke(reference, ReferenceSelectKind.Show);
            }
        };
    }

    private void SelectRow(int index, bool reveal)
    {
        if (index < 0 || index >= _rows.Count || _rows[index].Reference is not { } reference)
        {
            return;
        }

        _selected = reference;
        _selectionBinding?.Dispose();
        _selectionBinding = Surface.BindBrush((Border)_rows[index].Row.Child!, Border.BackgroundProperty, CodeEditorWidgetSurface.SelectionBackgroundProperty);
        if (reveal)
        {
            _rows[index].Row.BringIntoView();
        }
    }

    private void MoveSelection(int delta)
    {
        if (_rows.Count == 0)
        {
            return;
        }

        var index = _rows.FindIndex(r => ReferenceEquals(r.Reference, _selected));
        for (var i = index + delta; i >= 0 && i < _rows.Count; i += delta)
        {
            if (_rows[i].Reference is { } reference)
            {
                SelectRow(i, reveal: true);
                SelectReference?.Invoke(reference, ReferenceSelectKind.Show);
                return;
            }
        }
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                MoveSelection(+1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Enter when _selected is { } selected:
                e.Handled = true;
                SelectReference?.Invoke(selected, e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta) ? ReferenceSelectKind.Side : ReferenceSelectKind.Goto);
                break;
        }
    }

    // a double click in the preview opens the reference under it (upstream's preview onMouseDown with detail 2)
    private void OnPreviewMouseDown(CodeEditorMouseEvent e)
    {
        if (e.Event is PointerPressedEventArgs { ClickCount: 2 } && _selected is { } selected && _model is not null)
        {
            e.Handled = true;
            SelectReference?.Invoke(selected, ReferenceSelectKind.Open);
        }
    }

    private void UpdatePreviewDecorations(FileReferences file)
    {
        if (_preview.Model is not { } previewModel)
        {
            return;
        }

        _previewDecorations = previewModel.DeltaDecorations(_previewDecorations,
        [
            .. file.Children.Select(child => new ModelDeltaDecoration(
                child.Range,
                new ModelDecorationOptions("reference-decoration")
                {
                    ClassName = ReferenceDecorationClassName,
                    Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
                })),
        ]);
    }

    private void RevealPending()
    {
        if (_pendingReveal is not { } range || _preview.ViewModel is null)
        {
            return;
        }

        _pendingReveal = null;
        var start = _preview.Model.ValidatePosition(range.Start);
        _preview.SetSelection(new TextSelection(start, start));
        _preview.RevealRange(_preview.Model.ValidateRange(range), VerticalRevealType.Center, ScrollType.Immediate);
        Dispatcher.UIThread.Post(() => _preview.RevealRange(_preview.Model.ValidateRange(range), VerticalRevealType.Center, ScrollType.Immediate), DispatcherPriority.Background);
    }

    private Border CreateFileRow(FileReferences file)
    {
        var name = new TextBlock { Text = ReferencesModel.Basename(file.Uri), FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        var dir = new TextBlock { Text = DirName(file.Uri), Margin = new Thickness(6, 0, 0, 0), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        Surface.BindBrush(dir, TextBlock.ForegroundProperty, CodeEditorWidgetSurface.MutedForegroundProperty);
        var count = new TextBlock { Text = file.Children.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
        Surface.BindBrush(count, TextBlock.ForegroundProperty, CodeEditorWidgetSurface.MutedForegroundProperty);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        grid.Children.Add(name);
        Grid.SetColumn(dir, 1);
        grid.Children.Add(dir);
        Grid.SetColumn(count, 2);
        grid.Children.Add(count);
        return new Border { Background = Brushes.Transparent, Child = new Border { Child = grid, Padding = new Thickness(8, 2) } };
    }

    private Border CreateReferenceRow(OneReference reference, bool indent)
    {
        var text = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, FontFamily = Editor.FontFamily };
        if (reference.Parent.GetPreview(reference)?.Preview(reference.Range) is { } preview)
        {
            var before = new Run(preview.Value[..preview.HighlightStart]);
            var inside = new Run(preview.Value[preview.HighlightStart..preview.HighlightEnd]) { FontWeight = FontWeight.SemiBold };
            Surface.BindBrush(inside, TextElement.BackgroundProperty, CodeEditorPeekSurface.MatchHighlightBackgroundProperty);
            var after = new Run(preview.Value[preview.HighlightEnd..]);
            text.Inlines = [before, inside, after];
        }
        else
        {
            text.Text = $"line {reference.Range.StartLineNumber}, column {reference.Range.StartColumn}";
        }

        // the outer border catches the pointer across the row, the inner one shows the selection
        return new Border
        {
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new Border { Child = text, Padding = new Thickness(indent ? 22 : 10, 2, 8, 2) },
        };
    }

    private static string DirName(Uri uri)
    {
        var path = uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString;
        var slash = path.LastIndexOf('/');
        return slash > 0 ? Uri.UnescapeDataString(path[..slash]) : string.Empty;
    }
}
