// Ported from vscode (src/vs/editor/contrib/suggest/browser/suggestWidget.ts and suggestWidgetDetails.ts, commit
// b57d5c9e), MIT license. The suggest widget's states (hidden, loading, empty, open, frozen), the list focus and its
// keyboard moves, the details pane and the context keys it sets. The DOM, CSS and the list widget of upstream are an
// Avalonia template (Themes/CodeEditorSuggestWidgetTheme.axaml) with a drawn, virtualized list (CodeEditorSuggestList).
// Differences:
//  - the widget cannot be resized (no sash) and keeps no persisted size; the list shows up to MaxVisibleItems rows;
//  - the details pane sits right of the list when the editor has room for both, else below it; it never takes focus
//    (no State.Details, no toggleDetailsFocus / toggleExplainMode) and Page Up / Page Down move the list;
//  - documentation markdown is shown as text: paragraphs, code blocks in the editor font, inline marks removed;
//  - no status bar, no aria announcements; the messages are English.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Suggest;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc;

/// <summary>The focused suggestion of the widget (upstream's ISelectedSuggestion).</summary>
internal sealed record SelectedSuggestion(SuggestCompletionItem Item, int Index, CompletionModel Model);

/// <summary>The suggest widget of a <see cref="CodeEditor"/> (upstream's SuggestWidget): a content widget below or above the cursor.</summary>
[TemplatePart(ListPart, typeof(CodeEditorSuggestList))]
[TemplatePart(MessagePart, typeof(TextBlock))]
[TemplatePart(DetailsPart, typeof(Control))]
[TemplatePart(DetailsTitlePart, typeof(TextBlock))]
[TemplatePart(DetailsTypePart, typeof(TextBlock))]
[TemplatePart(DetailsDocsPart, typeof(StackPanel))]
[PseudoClasses(MessagePseudoClass, FrozenPseudoClass, DetailsPseudoClass, DetailsBelowPseudoClass)]
internal sealed class CodeEditorSuggestWidget : TemplatedControl, ICodeEditorContentWidget
{
    public const string WidgetId = "editor.widget.suggestWidget";

    public const string ListPart = "PART_List";
    public const string MessagePart = "PART_Message";
    public const string DetailsPart = "PART_Details";
    public const string DetailsTitlePart = "PART_DetailsTitle";
    public const string DetailsTypePart = "PART_DetailsType";
    public const string DetailsDocsPart = "PART_DetailsDocs";

    /// <summary>Set while a message ("Loading...", "No suggestions.") shows instead of the list.</summary>
    public const string MessagePseudoClass = ":message";

    /// <summary>Set while the list is frozen (manual suggestions that no longer match what was typed).</summary>
    public const string FrozenPseudoClass = ":frozen";

    /// <summary>Set while the details pane shows.</summary>
    public const string DetailsPseudoClass = ":details";

    /// <summary>Set while the details pane is below the list (the editor is too narrow for both side by side).</summary>
    public const string DetailsBelowPseudoClass = ":details-below";

    /// <summary>Upstream's SuggestWidget.LOADING_MESSAGE.</summary>
    public const string LoadingMessage = "Loading...";

    /// <summary>Upstream's SuggestWidget.NO_SUGGESTIONS_MESSAGE.</summary>
    public const string NoSuggestionsMessage = "No suggestions.";

    /// <summary>Defines the <see cref="Message"/> property.</summary>
    public static readonly DirectProperty<CodeEditorSuggestWidget, string> MessageProperty =
        AvaloniaProperty.RegisterDirect<CodeEditorSuggestWidget, string>(nameof(Message), o => o.Message);

    /// <summary>Defines the <see cref="DetailsFontFamily"/> property.</summary>
    public static readonly StyledProperty<FontFamily> DetailsFontFamilyProperty =
        AvaloniaProperty.Register<CodeEditorSuggestWidget, FontFamily>(nameof(DetailsFontFamily), FontFamily.Default);

    /// <summary>Defines the <see cref="DetailsWidth"/> property.</summary>
    public static readonly StyledProperty<double> DetailsWidthProperty =
        AvaloniaProperty.Register<CodeEditorSuggestWidget, double>(nameof(DetailsWidth), 320);

    private enum State
    {
        Hidden,
        Loading,
        Empty,
        Open,
        Frozen,
    }

    private readonly CodeEditor _editor;
    private readonly IContextKey<bool> _ctxVisible;
    private readonly IContextKey<bool> _ctxDetailsVisible;
    private readonly IContextKey<bool> _ctxMultipleSuggestions;
    private readonly IContextKey<bool> _ctxHasFocusedSuggestion;
    private readonly CodeEditorSuggestList _fallbackList = new();

    private State _state = State.Hidden;
    private bool _isAuto;
    private bool _detailsVisible;
    private string _message = string.Empty;
    private CompletionModel? _completionModel;
    private SuggestCompletionItem? _focusedItem;
    private CancellationTokenSource? _currentSuggestionDetails;
    private IDisposable? _loadingTimeout;
    private TextPosition? _position;
    private CodeEditorSuggestList? _list;
    private Control? _details;
    private TextBlock? _detailsTitle;
    private TextBlock? _detailsType;
    private StackPanel? _detailsDocs;
    private bool _ignoreFocusEvents;

    public CodeEditorSuggestWidget(CodeEditor editor)
    {
        _editor = editor;
        Focusable = false;
        _ctxVisible = editor.CreateContextKey(CodeEditorContextKeys.SuggestWidgetVisible);
        _ctxDetailsVisible = editor.CreateContextKey(SuggestContext.DetailsVisible);
        _ctxMultipleSuggestions = editor.CreateContextKey(SuggestContext.MultipleSuggestions);
        _ctxHasFocusedSuggestion = editor.CreateContextKey(SuggestContext.HasFocusedSuggestion);
        _fallbackList.RowClicked += OnRowClicked;
    }

    /// <summary>The message shown instead of the list (loading, no suggestions).</summary>
    public string Message
    {
        get => _message;
        private set => SetAndRaise(MessageProperty, ref _message, value);
    }

    /// <summary>The font of the details pane's documentation (the code blocks use the editor's font).</summary>
    public FontFamily DetailsFontFamily { get => GetValue(DetailsFontFamilyProperty); set => SetValue(DetailsFontFamilyProperty, value); }

    /// <summary>The width of the details pane.</summary>
    public double DetailsWidth { get => GetValue(DetailsWidthProperty); set => SetValue(DetailsWidthProperty, value); }

    /// <summary>An item was chosen (clicked); the controller inserts it (upstream's onDidSelect).</summary>
    public event Action<SelectedSuggestion>? DidSelect;

    /// <summary>The focused item changed (upstream's onDidFocus).</summary>
    public event Action<SelectedSuggestion>? DidFocus;

    /// <summary>The list became visible (upstream's onDidShow).</summary>
    public event Action? DidShow;

    /// <summary>The widget was hidden (upstream's onDidHide).</summary>
    public event Action? DidHide;

    public string Id => WidgetId;

    public Control Control => this;

    /// <summary>The list (the template's, or a detached one before the template is applied).</summary>
    internal CodeEditorSuggestList List => _list ?? _fallbackList;

    /// <summary>Whether the widget shows anything.</summary>
    public bool IsVisibleState => _state != State.Hidden;

    /// <summary>Whether the list shows a message.</summary>
    public bool IsMessageState => _state is State.Loading or State.Empty;

    /// <summary>Whether the details pane is open.</summary>
    public bool IsDetailsVisible => _detailsVisible;

    /// <summary>Whether the list no longer follows typing (upstream's isFrozen).</summary>
    public bool IsFrozen() => _state == State.Frozen;

    public ContentWidgetPosition? GetPosition()
    {
        if (_state == State.Hidden || _position is not { } position)
        {
            return null;
        }

        FitToEditor(position);
        return new ContentWidgetPosition(position, [ContentWidgetPositionPreference.Below, ContentWidgetPositionPreference.Above]);
    }

    // upstream's _layout: as many rows as fit below or above the line, whichever has more room
    private void FitToEditor(TextPosition position)
    {
        if (_editor.GetScrolledVisiblePosition(position) is not { } visible)
        {
            return;
        }

        var size = _editor.GetLayoutSize();
        var height = size.Height;

        // never wider than the text area
        var textWidth = size.Width - (_editor.View?.GutterWidth ?? 0);
        var horizontalChrome = Padding.Left + Padding.Right + BorderThickness.Left + BorderThickness.Right;
        if (textWidth > 0)
        {
            // the vertical scroll bar overlays the right edge of the text area
            List.MaxWidth = Math.Max(120, textWidth - horizontalChrome - 16);
        }
        var room = Math.Max(visible.Top, height - visible.Top - visible.Height);
        var chrome = Padding.Top + Padding.Bottom + BorderThickness.Top + BorderThickness.Bottom;
        var rowHeight = List.RowHeight;
        if (rowHeight > 0)
        {
            List.RowLimit = (int)Math.Floor((room - chrome) / rowHeight);
        }
    }

    public void AfterRender(ContentWidgetPositionPreference? position, Point? coordinate)
    {
        UpdateDetailsPlacement();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_list is { } old)
        {
            old.RowClicked -= OnRowClicked;
        }

        _list = e.NameScope.Find<CodeEditorSuggestList>(ListPart);
        _details = e.NameScope.Find<Control>(DetailsPart);
        _detailsTitle = e.NameScope.Find<TextBlock>(DetailsTitlePart);
        _detailsType = e.NameScope.Find<TextBlock>(DetailsTypePart);
        _detailsDocs = e.NameScope.Find<StackPanel>(DetailsDocsPart);
        if (_list is { } list)
        {
            list.RowClicked += OnRowClicked;
            list.SetItems(_fallbackList.Items);
            list.SetFocus(_fallbackList.FocusedIndex);
        }

        RenderDetails();
    }

    private void OnRowClicked(int index)
    {
        if (_completionModel is not { } model || index < 0 || index >= model.Items.Count)
        {
            return;
        }

        SetFocus(index);
        DidSelect?.Invoke(new SelectedSuggestion(model.Items[index], index, model));
    }

    // ---- states

    private void SetState(State state)
    {
        if (_state == state)
        {
            return;
        }

        _state = state;
        PseudoClasses.Set(FrozenPseudoClass, state == State.Frozen);
        PseudoClasses.Set(MessagePseudoClass, state is State.Loading or State.Empty);

        switch (state)
        {
            case State.Hidden:
                _ctxVisible.Reset();
                _ctxMultipleSuggestions.Reset();
                _ctxHasFocusedSuggestion.Reset();
                List.SetItems([]);
                _focusedItem = null;
                break;
            case State.Loading:
                Message = LoadingMessage;
                _focusedItem = null;
                Show();
                break;
            case State.Empty:
                Message = NoSuggestionsMessage;
                _focusedItem = null;
                Show();
                break;
            case State.Open:
            case State.Frozen:
                Show();
                break;
        }

        RenderDetails();
        _editor.LayoutContentWidget(this);
    }

    private void Show()
    {
        _ctxVisible.Set(true);
        Dispatcher.UIThread.Post(() =>
        {
            if (_state != State.Hidden)
            {
                DidShow?.Invoke();
            }
        });
    }

    /// <summary>Suggestions were asked for; a manual request shows "Loading..." after <paramref name="delay"/> ms (upstream's showTriggered).</summary>
    public void ShowTriggered(bool auto, int delay)
    {
        if (_state != State.Hidden)
        {
            return;
        }

        _position = AnchorPosition();
        _isAuto = auto;
        if (!_isAuto)
        {
            _loadingTimeout?.Dispose();
            _loadingTimeout = DispatcherTimer.RunOnce(() => SetState(State.Loading), TimeSpan.FromMilliseconds(Math.Max(1, delay)));
        }
    }

    /// <summary>Shows the items of a completion model (upstream's showSuggestions).</summary>
    public void ShowSuggestions(CompletionModel completionModel, int selectionIndex, bool isFrozen, bool isAuto, bool noFocus)
    {
        _position = AnchorPosition();
        _loadingTimeout?.Dispose();
        _loadingTimeout = null;

        _currentSuggestionDetails?.Cancel();
        _currentSuggestionDetails = null;

        _completionModel = completionModel;

        if (isFrozen && _state != State.Empty && _state != State.Hidden)
        {
            SetState(State.Frozen);
            return;
        }

        var visibleCount = completionModel.Items.Count;
        _ctxMultipleSuggestions.Set(visibleCount > 1);

        if (visibleCount == 0)
        {
            SetState(isAuto ? State.Hidden : State.Empty);
            _completionModel = null;
            return;
        }

        _focusedItem = null;
        _ignoreFocusEvents = true;
        try
        {
            List.SetItems(completionModel.Items);
            SetState(isFrozen ? State.Frozen : State.Open);
            List.Reveal(selectionIndex);
        }
        finally
        {
            _ignoreFocusEvents = false;
        }

        SetFocus(noFocus ? -1 : selectionIndex);
        _editor.LayoutContentWidget(this);
    }

    /// <summary>Hides the widget (upstream's hideWidget).</summary>
    public void HideWidget()
    {
        _loadingTimeout?.Dispose();
        _loadingTimeout = null;
        _currentSuggestionDetails?.Cancel();
        _currentSuggestionDetails = null;
        SetState(State.Hidden);
        DidHide?.Invoke();
    }

    /// <summary>The focused suggestion, or null (upstream's getFocusedItem).</summary>
    public SelectedSuggestion? GetFocusedItem()
    {
        if (_state is not (State.Hidden or State.Empty or State.Loading)
            && _completionModel is { } model
            && List.FocusedIndex >= 0
            && List.FocusedIndex < model.Items.Count)
        {
            return new SelectedSuggestion(model.Items[List.FocusedIndex], List.FocusedIndex, model);
        }
        return null;
    }

    // ---- focus moves

    /// <summary>Focuses the first item when nothing is focused (upstream's focusSelected).</summary>
    public void FocusSelected()
    {
        if (List.Items.Count > 0)
        {
            SetFocus(0);
        }
    }

    public bool SelectNextPage() => Move(() =>
    {
        var count = List.Items.Count;
        var focus = List.FocusedIndex;
        var page = List.VisibleCount;
        var lastVisible = List.ScrollTop + page - 1;
        // like the list's focusNextPage: to the last visible row, or a page further when it is focused already
        SetFocus(Math.Min(count - 1, focus == lastVisible || focus < 0 ? Math.Max(focus, 0) + page : lastVisible));
    });

    public bool SelectNext() => Move(() =>
    {
        var count = List.Items.Count;
        SetFocus(count == 0 ? -1 : (List.FocusedIndex + 1) % count);
    });

    public bool SelectLast() => Move(() => SetFocus(List.Items.Count - 1));

    public bool SelectPreviousPage() => Move(() =>
    {
        var focus = List.FocusedIndex;
        var page = List.VisibleCount;
        var firstVisible = List.ScrollTop;
        SetFocus(Math.Max(0, focus == firstVisible || focus < 0 ? Math.Max(focus, 0) - page : firstVisible));
    });

    public bool SelectPrevious()
    {
        Move(() =>
        {
            var count = List.Items.Count;
            var focus = List.FocusedIndex;
            SetFocus(count == 0 ? -1 : focus <= 0 ? count - 1 : focus - 1);
        });
        // upstream returns false here (the command is handled either way)
        return false;
    }

    public bool SelectFirst() => Move(() => SetFocus(List.Items.Count > 0 ? 0 : -1));

    private bool Move(Action move)
    {
        switch (_state)
        {
            case State.Hidden:
                return false;
            case State.Loading:
                return !_isAuto;
            case State.Empty:
                return true;
            default:
                move();
                return true;
        }
    }

    private void SetFocus(int index)
    {
        List.SetFocus(index);
        OnListFocus();
    }

    // upstream's _onListFocus
    private void OnListFocus()
    {
        if (_ignoreFocusEvents)
        {
            return;
        }

        var index = List.FocusedIndex;
        if (index < 0 || _completionModel is not { } model || index >= model.Items.Count)
        {
            _currentSuggestionDetails?.Cancel();
            _currentSuggestionDetails = null;
            _focusedItem = null;
            _ctxHasFocusedSuggestion.Set(false);
            RenderDetails();
            return;
        }

        _ctxHasFocusedSuggestion.Set(true);
        var item = model.Items[index];
        if (!ReferenceEquals(item, _focusedItem))
        {
            _currentSuggestionDetails?.Cancel();
            _focusedItem = item;
            List.Reveal(index);

            var cts = new CancellationTokenSource();
            _currentSuggestionDetails = cts;
            var resolve = item.Resolve(cts.Token);
            if (resolve.IsCompleted)
            {
                RenderDetails();
            }
            else
            {
                _ = AfterResolve(resolve, item, cts);
            }
        }

        DidFocus?.Invoke(new SelectedSuggestion(item, index, model));
    }

    private async Task AfterResolve(Task resolve, SuggestCompletionItem item, CancellationTokenSource cts)
    {
        try
        {
            await resolve;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            UnexpectedErrors.Report(e);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cts.IsCancellationRequested || !ReferenceEquals(item, _focusedItem))
        {
            return;
        }

        // item can have extra information, so re-render
        List.InvalidateVisual();
        RenderDetails();
    }

    // ---- details

    /// <summary>Whether the item has something for the details pane (upstream's canExpandCompletionItem).</summary>
    public static bool CanExpandCompletionItem(SuggestCompletionItem? item)
    {
        if (item is null)
        {
            return false;
        }
        var completion = item.Completion;
        return completion.Documentation is not null || (completion.Detail is { Length: > 0 } detail && detail != item.TextLabel);
    }

    /// <summary>Shows or hides the details pane of the focused item (upstream's toggleDetails).</summary>
    public void ToggleDetails()
    {
        if (_detailsVisible)
        {
            _detailsVisible = false;
            _ctxDetailsVisible.Set(false);
        }
        else if (CanExpandCompletionItem(_focusedItem) && _state is State.Open or State.Frozen)
        {
            _detailsVisible = true;
            _ctxDetailsVisible.Set(true);
        }

        RenderDetails();
        _editor.LayoutContentWidget(this);
    }

    private void RenderDetails()
    {
        var show = _detailsVisible && _state is State.Open or State.Frozen && CanExpandCompletionItem(_focusedItem);
        PseudoClasses.Set(DetailsPseudoClass, show);
        List.ShowFocusedDetail = !show;
        if (_details is { } details)
        {
            details.IsVisible = show;
        }

        if (!show || _focusedItem is not { } item)
        {
            return;
        }

        var completion = item.Completion;
        if (_detailsTitle is { } title)
        {
            title.Text = item.TextLabel;
        }
        if (_detailsType is { } type)
        {
            var detail = completion.Detail ?? string.Empty;
            type.Text = detail;
            type.IsVisible = detail.Length > 0 && detail != item.TextLabel;
        }
        if (_detailsDocs is { } docs)
        {
            docs.Children.Clear();
            if (completion.Documentation is { } documentation)
            {
                foreach (var block in SuggestDocumentation.ToBlocks(documentation))
                {
                    docs.Children.Add(CreateBlock(block));
                }
            }
            docs.IsVisible = docs.Children.Count > 0;
        }

        UpdateDetailsPlacement();
    }

    private Control CreateBlock(SuggestDocumentationBlock block)
    {
        var text = new TextBlock
        {
            Text = block.Text,
            TextWrapping = block.IsCode ? TextWrapping.NoWrap : TextWrapping.Wrap,
        };
        // code in the editor's font, text in the details font
        text.FontFamily = block.IsCode ? _editor.FontFamily : DetailsFontFamily;
        return text;
    }

    // the details go beside the list when the editor has room, else below it
    private void UpdateDetailsPlacement()
    {
        var available = _editor.Bounds.Width;
        var below = List.ListWidth + DetailsWidth + 16 > available;
        PseudoClasses.Set(DetailsBelowPseudoClass, below);
        if (_details is { } details)
        {
            DockPanel.SetDock(details, below ? Dock.Bottom : Dock.Right);

            // below, as wide as the list (its text would widen the widget otherwise)
            details.Width = below ? Math.Min(List.ListWidth, List.MaxWidth) + Padding.Left + Padding.Right : DetailsWidth;
        }
    }

    // the start of the word at the cursor: the list sits under what is being completed
    private TextPosition? AnchorPosition()
    {
        if (_editor.GetPosition() is not { } position)
        {
            return null;
        }
        var word = SuggestWords.GetWordUntilPosition(_editor.Model, position, _editor.LanguageConfigurationService);
        return new TextPosition(position.LineNumber, word.StartColumn);
    }
}
