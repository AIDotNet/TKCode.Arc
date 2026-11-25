// Ported from vscode (src/vs/editor/contrib/find/browser/findWidget.ts, commit b57d5c9e), MIT license.
// The find widget's behavior: what it shows for the find state, what its inputs, toggles and buttons change, the match
// count, which buttons are enabled, the view zone that makes room above the first line, and the narrow layout. The DOM,
// CSS and the FindInput / ReplaceInput widgets of upstream are an Avalonia template (Themes/CodeEditorFindWidgetTheme.axaml)
// built from Arc controls (Input, FieldIconButton).
// Differences:
//  - the widget cannot be resized (no sash), and its narrow layout only hides the match count;
//  - no search / replace history (Up / Down in the inputs) and no multi-line search string (Ctrl+Enter);
//  - no accessibility announcements or help hint;
//  - the find options widget (the toggles shown alone when an option changes while the widget is hidden) is not
//    ported, and highlightFindOptions (a short flash of the toggles) does nothing;
//  - the labels are English.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Find;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

/// <summary>The find widget's labels (upstream's NLS_* strings).</summary>
internal static class FindWidgetStrings
{
    public const string FindPlaceholder = "Find";
    public const string ReplacePlaceholder = "Replace";
    public const string PreviousMatch = "Previous Match";
    public const string NextMatch = "Next Match";
    public const string FindInSelection = "Find in Selection";
    public const string Close = "Close";
    public const string Replace = "Replace";
    public const string ReplaceAll = "Replace All";
    public const string ToggleReplace = "Toggle Replace";
    public const string MatchCase = "Match Case";
    public const string WholeWord = "Match Whole Word";
    public const string Regex = "Use Regular Expression";
    public const string PreserveCase = "Preserve Case";
    public const string NoResults = "No results";

    /// <summary>Upstream's NLS_MATCHES_LOCATION ("{0} of {1}").</summary>
    public static string MatchesLocation(string position, string count) => $"{position} of {count}";

    /// <summary>Upstream's NLS_MATCHES_COUNT_LIMIT_TITLE.</summary>
    public static string MatchesCountLimit => $"Only the first {FindModelBoundToEditorModel.MatchesLimit} results are highlighted, but all find operations work on the entire text.";
}

/// <summary>
/// The find widget of a <see cref="CodeEditor"/> (upstream's FindWidget): an overlay widget in the top right corner
/// that shows and edits the find state of the editor's <see cref="FindController"/>.
/// </summary>
[TemplatePart(FindInputPart, typeof(Input))]
[TemplatePart(ReplaceInputPart, typeof(Input))]
[PseudoClasses(ReplacePseudoClass, NoResultsPseudoClass, NarrowPseudoClass, RevealedPseudoClass)]
internal sealed class CodeEditorFindWidget : TemplatedControl, ICodeEditorOverlayWidget
{
    public const string WidgetId = "editor.contrib.findWidget";

    public const string FindInputPart = "PART_FindInput";
    public const string ReplaceInputPart = "PART_ReplaceInput";

    /// <summary>Set while the replace row shows.</summary>
    public const string ReplacePseudoClass = ":replace";

    /// <summary>Set while the search string is not empty and nothing matches it.</summary>
    public const string NoResultsPseudoClass = ":no-results";

    /// <summary>Set while the editor is too narrow for the whole widget.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the widget is shown (it slides in).</summary>
    public const string RevealedPseudoClass = ":revealed";

    /// <summary>The match count ("3 of 12", "No results").</summary>
    public static readonly DirectProperty<CodeEditorFindWidget, string> MatchesCountTextProperty =
        AvaloniaProperty.RegisterDirect<CodeEditorFindWidget, string>(nameof(MatchesCountText), w => w.MatchesCountText);

    /// <summary>The editor width below which the match count is hidden.</summary>
    public static readonly StyledProperty<double> NarrowEditorWidthProperty =
        AvaloniaProperty.Register<CodeEditorFindWidget, double>(nameof(NarrowEditorWidth));

    private readonly CodeEditor _editor;
    private readonly FindController _controller;
    private readonly FindReplaceState _state;
    private readonly IContextKey<bool> _findInputFocused;
    private readonly IContextKey<bool> _replaceInputFocused;
    private readonly Action<FindReplaceStateChangedEvent> _onStateChanged;
    private readonly Action<CursorStateChangedEvent> _onCursorChanged;
    private readonly Action<Size> _onLayoutChanged;
    private readonly Action<bool> _onTextFocusChanged;

    private Input? _findInput;
    private Input? _replaceInput;
    private FieldIconButton? _toggleReplace;
    private FieldIconButton? _matchCase;
    private FieldIconButton? _wholeWord;
    private FieldIconButton? _regex;
    private FieldIconButton? _preserveCase;
    private FieldIconButton? _previous;
    private FieldIconButton? _next;
    private FieldIconButton? _findInSelection;
    private FieldIconButton? _close;
    private FieldIconButton? _replace;
    private FieldIconButton? _replaceAll;
    private TextBlock? _matchesCount;

    private string _matchesCountText = string.Empty;
    private bool _isVisible;
    private bool _isReplaceVisible;
    private bool _ignoreChangeEvent;
    private bool _isDisposed;
    private string? _viewZoneId;
    private CodeEditorViewZone? _viewZone;

    public CodeEditorFindWidget(CodeEditor editor, FindController controller, FindReplaceState state)
    {
        _editor = editor;
        _controller = controller;
        _state = state;
        _findInputFocused = editor.CreateContextKey(CodeEditorContextKeys.FindInputFocussed);
        _replaceInputFocused = editor.CreateContextKey(CodeEditorContextKeys.ReplaceInputFocussed);
        IsVisible = false;

        _onStateChanged = OnStateChanged;
        _state.FindReplaceStateChange += _onStateChanged;
        _onCursorChanged = _ =>
        {
            if (_isVisible)
            {
                UpdateToggleSelectionFindButton();
            }
        };
        _editor.DidChangeCursorSelection += _onCursorChanged;
        _onLayoutChanged = _ => TryUpdateWidgetWidth();
        _editor.DidLayoutChange += _onLayoutChanged;
        _onTextFocusChanged = focused =>
        {
            if (focused)
            {
                // the editor's text took the focus back: no input of the widget has it
                _findInputFocused.Set(false);
                _replaceInputFocused.Set(false);
            }
        };
        _editor.DidChangeTextFocus += _onTextFocusChanged;

        UpdateMatchesCount();
    }

    /// <summary>The match count ("3 of 12", "No results").</summary>
    public string MatchesCountText
    {
        get => _matchesCountText;
        private set => SetAndRaise(MatchesCountTextProperty, ref _matchesCountText, value);
    }

    /// <summary>The editor width below which the match count is hidden (Monaco's narrow find widget).</summary>
    public double NarrowEditorWidth
    {
        get => GetValue(NarrowEditorWidthProperty);
        set => SetValue(NarrowEditorWidthProperty, value);
    }

    /// <summary>Whether the widget is shown (upstream's _isVisible).</summary>
    public bool IsRevealed => _isVisible;

    /// <summary>Whether the replace row is shown (upstream's _isReplaceVisible).</summary>
    public bool IsReplaceVisible => _isReplaceVisible;

    /// <summary>Whether the replace input was the last input of the widget with focus (upstream's lastFocusedInputWasReplace).</summary>
    public bool LastFocusedInputWasReplace { get; private set; }

    /// <summary>The id of the view zone above the first line while the widget shows, or null.</summary>
    internal string? ViewZoneId => _viewZoneId;

    internal Input? FindInput => _findInput;

    internal Input? ReplaceInput => _replaceInput;

    internal FieldIconButton? MatchCaseToggle => _matchCase;

    internal FieldIconButton? WholeWordToggle => _wholeWord;

    internal FieldIconButton? RegexToggle => _regex;

    internal FieldIconButton? PreserveCaseToggle => _preserveCase;

    internal FieldIconButton? PreviousButton => _previous;

    internal FieldIconButton? NextButton => _next;

    internal FieldIconButton? FindInSelectionButton => _findInSelection;

    internal FieldIconButton? CloseButton => _close;

    internal FieldIconButton? ReplaceButton => _replace;

    internal FieldIconButton? ReplaceAllButton => _replaceAll;

    internal FieldIconButton? ToggleReplaceButton => _toggleReplace;

    string ICodeEditorOverlayWidget.Id => WidgetId;

    Control ICodeEditorOverlayWidget.Control => this;

    OverlayWidgetPosition? ICodeEditorOverlayWidget.GetPosition() => new(OverlayWidgetPositionPreference.TopRightCorner);

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _state.FindReplaceStateChange -= _onStateChanged;
        _editor.DidChangeCursorSelection -= _onCursorChanged;
        _editor.DidLayoutChange -= _onLayoutChanged;
        _editor.DidChangeTextFocus -= _onTextFocusChanged;
        RemoveViewZone();
        _findInputFocused.Reset();
        _replaceInputFocused.Reset();
    }

    // ----- the template

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachParts();

        var scope = e.NameScope;
        _findInput = scope.Find<Input>(FindInputPart);
        _replaceInput = scope.Find<Input>(ReplaceInputPart);
        _toggleReplace = scope.Find<FieldIconButton>("PART_ToggleReplace");
        _matchCase = scope.Find<FieldIconButton>("PART_MatchCase");
        _wholeWord = scope.Find<FieldIconButton>("PART_WholeWord");
        _regex = scope.Find<FieldIconButton>("PART_Regex");
        _preserveCase = scope.Find<FieldIconButton>("PART_PreserveCase");
        _previous = scope.Find<FieldIconButton>("PART_Previous");
        _next = scope.Find<FieldIconButton>("PART_Next");
        _findInSelection = scope.Find<FieldIconButton>("PART_FindInSelection");
        _close = scope.Find<FieldIconButton>("PART_Close");
        _replace = scope.Find<FieldIconButton>("PART_Replace");
        _replaceAll = scope.Find<FieldIconButton>("PART_ReplaceAll");
        _matchesCount = scope.Find<TextBlock>("PART_MatchesCount");

        if (_findInput is { } findInput)
        {
            findInput.PlaceholderText = FindWidgetStrings.FindPlaceholder;
            findInput.TextChanged += OnFindInputTextChanged;
            findInput.GotFocus += OnFindInputGotFocus;
            findInput.LostFocus += OnFindInputLostFocus;
            findInput.KeyDown += OnInputKeyDown;
        }

        if (_replaceInput is { } replaceInput)
        {
            replaceInput.PlaceholderText = FindWidgetStrings.ReplacePlaceholder;
            replaceInput.TextChanged += OnReplaceInputTextChanged;
            replaceInput.GotFocus += OnReplaceInputGotFocus;
            replaceInput.LostFocus += OnReplaceInputLostFocus;
            replaceInput.KeyDown += OnInputKeyDown;
        }

        Wire(_toggleReplace, FindWidgetStrings.ToggleReplace, null, OnToggleReplaceClick);
        Wire(_matchCase, FindWidgetStrings.MatchCase, FindKeybindings.ToggleCaseSensitive, OnMatchCaseClick);
        Wire(_wholeWord, FindWidgetStrings.WholeWord, FindKeybindings.ToggleWholeWord, OnWholeWordClick);
        Wire(_regex, FindWidgetStrings.Regex, FindKeybindings.ToggleRegex, OnRegexClick);
        Wire(_preserveCase, FindWidgetStrings.PreserveCase, FindKeybindings.TogglePreserveCase, OnPreserveCaseClick);
        Wire(_previous, FindWidgetStrings.PreviousMatch, FindKeybindings.PreviousMatch, OnPreviousClick);
        Wire(_next, FindWidgetStrings.NextMatch, FindKeybindings.NextMatch, OnNextClick);
        Wire(_findInSelection, FindWidgetStrings.FindInSelection, FindKeybindings.ToggleSearchScope, OnFindInSelectionClick);
        Wire(_close, FindWidgetStrings.Close, "Escape", OnCloseClick);
        Wire(_replace, FindWidgetStrings.Replace, FindKeybindings.ReplaceOne, OnReplaceClick);
        Wire(_replaceAll, FindWidgetStrings.ReplaceAll, FindKeybindings.ReplaceAll, OnReplaceAllClick);

        // show the whole state in the new parts
        _ignoreChangeEvent = true;
        try
        {
            if (_findInput is not null)
            {
                _findInput.Text = _state.SearchString;
            }

            if (_replaceInput is not null)
            {
                _replaceInput.Text = _state.ReplaceString;
            }
        }
        finally
        {
            _ignoreChangeEvent = false;
        }

        UpdateToggles();
        UpdateMatchesCount();
        UpdateButtons();
    }

    private static void Wire(FieldIconButton? button, string label, string? keybinding, EventHandler<RoutedEventArgs> click)
    {
        if (button is null)
        {
            return;
        }

        var tip = keybinding is null ? label : $"{label} ({keybinding})";
        ToolTip.SetTip(button, tip);
        Avalonia.Automation.AutomationProperties.SetName(button, label);
        button.Click += click;
    }

    private void DetachParts()
    {
        if (_findInput is { } findInput)
        {
            findInput.TextChanged -= OnFindInputTextChanged;
            findInput.GotFocus -= OnFindInputGotFocus;
            findInput.LostFocus -= OnFindInputLostFocus;
            findInput.KeyDown -= OnInputKeyDown;
        }

        if (_replaceInput is { } replaceInput)
        {
            replaceInput.TextChanged -= OnReplaceInputTextChanged;
            replaceInput.GotFocus -= OnReplaceInputGotFocus;
            replaceInput.LostFocus -= OnReplaceInputLostFocus;
            replaceInput.KeyDown -= OnInputKeyDown;
        }

        Unwire(_toggleReplace, OnToggleReplaceClick);
        Unwire(_matchCase, OnMatchCaseClick);
        Unwire(_wholeWord, OnWholeWordClick);
        Unwire(_regex, OnRegexClick);
        Unwire(_preserveCase, OnPreserveCaseClick);
        Unwire(_previous, OnPreviousClick);
        Unwire(_next, OnNextClick);
        Unwire(_findInSelection, OnFindInSelectionClick);
        Unwire(_close, OnCloseClick);
        Unwire(_replace, OnReplaceClick);
        Unwire(_replaceAll, OnReplaceAllClick);
    }

    private static void Unwire(FieldIconButton? button, EventHandler<RoutedEventArgs> click)
    {
        if (button is not null)
        {
            button.Click -= click;
        }
    }

    // ----- the find state

    private void OnStateChanged(FindReplaceStateChangedEvent e)
    {
        if (e.SearchString)
        {
            _ignoreChangeEvent = true;
            try
            {
                if (_findInput is not null && _findInput.Text != _state.SearchString)
                {
                    _findInput.Text = _state.SearchString;
                }
            }
            finally
            {
                _ignoreChangeEvent = false;
            }

            UpdateButtons();
        }

        if (e.ReplaceString)
        {
            _ignoreChangeEvent = true;
            try
            {
                if (_replaceInput is not null && _replaceInput.Text != _state.ReplaceString)
                {
                    _replaceInput.Text = _state.ReplaceString;
                }
            }
            finally
            {
                _ignoreChangeEvent = false;
            }
        }

        if (e.IsRevealed)
        {
            if (_state.IsRevealed)
            {
                Reveal();
            }
            else
            {
                Hide(true);
            }
        }

        if (e.IsReplaceRevealed)
        {
            if (_state.IsReplaceRevealed)
            {
                if (!_editor.IsReadOnly && !_isReplaceVisible)
                {
                    _isReplaceVisible = true;
                    UpdateButtons();
                }
            }
            else if (_isReplaceVisible)
            {
                _isReplaceVisible = false;
                UpdateButtons();
            }
        }

        if ((e.IsRevealed || e.IsReplaceRevealed) && (_state.IsRevealed || _state.IsReplaceRevealed))
        {
            ShowViewZone(adjustScroll: true);
        }

        if (e.IsRegex || e.WholeWord || e.MatchCase || e.PreserveCase || e.SearchScope)
        {
            UpdateToggles();
        }

        if (e.SearchScope)
        {
            UpdateToggleSelectionFindButton();
        }

        if (e.SearchString || e.MatchesCount || e.MatchesPosition)
        {
            var showRedOutline = _state.SearchString.Length > 0 && _state.MatchesCount == 0;
            PseudoClasses.Set(NoResultsPseudoClass, showRedOutline);
            if (_findInput is not null)
            {
                _findInput.IsInvalid = showRedOutline;
            }

            UpdateMatchesCount();
            UpdateButtons();
        }

        if (e.Loop)
        {
            UpdateButtons();
        }
    }

    private void UpdateToggles()
    {
        SetOn(_matchCase, _state.MatchCase);
        SetOn(_wholeWord, _state.WholeWord);
        SetOn(_regex, _state.IsRegex);
        SetOn(_preserveCase, _state.PreserveCase);
        SetOn(_findInSelection, _state.SearchScope is not null);
    }

    private static void SetOn(FieldIconButton? button, bool on)
    {
        if (button is not null)
        {
            button.IsOn = on;
        }
    }

    private void UpdateMatchesCount()
    {
        string label;
        if (_state.MatchesCount > 0)
        {
            var matchesCount = _state.MatchesCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (_state.MatchesCount >= FindModelBoundToEditorModel.MatchesLimit)
            {
                matchesCount += "+";
            }

            var matchesPosition = _state.MatchesPosition.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (matchesPosition == "0")
            {
                matchesPosition = "?";
            }

            label = FindWidgetStrings.MatchesLocation(matchesPosition, matchesCount);
        }
        else
        {
            label = FindWidgetStrings.NoResults;
        }

        MatchesCountText = label;
        if (_matchesCount is not null)
        {
            ToolTip.SetTip(_matchesCount, _state.MatchesCount >= FindModelBoundToEditorModel.MatchesLimit ? FindWidgetStrings.MatchesCountLimit : null);
        }
    }

    private void UpdateToggleSelectionFindButton()
    {
        if (_findInSelection is null)
        {
            return;
        }

        var selection = _editor.GetSelection();
        var isSelection = selection is { } s && (s.StartLineNumber != s.EndLineNumber || s.StartColumn != s.EndColumn);
        var isChecked = _findInSelection.IsOn;
        _findInSelection.IsEnabled = _isVisible && (isChecked || isSelection);
    }

    private void UpdateButtons()
    {
        if (_findInput is not null)
        {
            _findInput.IsEnabled = _isVisible;
        }

        if (_replaceInput is not null)
        {
            _replaceInput.IsEnabled = _isVisible && _isReplaceVisible;
        }

        UpdateToggleSelectionFindButton();
        SetEnabled(_close, _isVisible);

        var findInputIsNonEmpty = _state.SearchString.Length > 0;
        var matchesCount = _state.MatchesCount > 0;
        SetEnabled(_previous, _isVisible && findInputIsNonEmpty && matchesCount && _state.CanNavigateBack());
        SetEnabled(_next, _isVisible && findInputIsNonEmpty && matchesCount && _state.CanNavigateForward());
        SetEnabled(_replace, _isVisible && _isReplaceVisible && findInputIsNonEmpty);
        SetEnabled(_replaceAll, _isVisible && _isReplaceVisible && findInputIsNonEmpty);

        PseudoClasses.Set(ReplacePseudoClass, _isReplaceVisible);
        SetOn(_toggleReplace, _isReplaceVisible);

        var canReplace = !_editor.IsReadOnly;
        SetEnabled(_toggleReplace, _isVisible && canReplace);
    }

    private static void SetEnabled(Control? control, bool enabled)
    {
        if (control is not null)
        {
            control.IsEnabled = enabled;
        }
    }

    // ----- showing and hiding

    private void Reveal()
    {
        if (_isVisible)
        {
            return;
        }

        _isVisible = true;
        var selection = _editor.GetSelection();
        if (_findInSelection is not null)
        {
            switch (_controller.FindEditor.FindOptions.AutoFindInSelection)
            {
                case AutoFindInSelection.Always:
                    _findInSelection.IsOn = true;
                    break;
                case AutoFindInSelection.Never:
                    _findInSelection.IsOn = _state.SearchScope is not null;
                    break;
                case AutoFindInSelection.Multiline:
                    _findInSelection.IsOn = selection is { } s && s.StartLineNumber != s.EndLineNumber;
                    break;
            }
        }

        IsVisible = true;
        PseudoClasses.Set(RevealedPseudoClass, true);
        TryUpdateWidgetWidth();
        UpdateButtons();
        _editor.LayoutOverlayWidget(this);

        // keep the text where it is, unless the selection would end up under the widget
        var adjustEditorScrollTop = true;
        if (_controller.FindEditor.FindOptions.SeedSearchStringFromSelection != SeedSearchStringFromSelection.Never && selection is { } selected
            && _editor.GetScrolledVisiblePosition(selected.Range.Start) is { } start && start.Top < GetHeight())
        {
            if (selected.EndLineNumber > selected.StartLineNumber)
            {
                adjustEditorScrollTop = false;
            }

            var leftOfFindWidget = _editor.GetLayoutSize().Width - GetWidth();
            if (start.Left > leftOfFindWidget)
            {
                adjustEditorScrollTop = false;
            }

            if (_editor.GetScrolledVisiblePosition(selected.Range.End) is { } end && end.Left > leftOfFindWidget)
            {
                adjustEditorScrollTop = false;
            }
        }

        ShowViewZone(adjustEditorScrollTop);
    }

    private void Hide(bool focusTheEditor)
    {
        if (!_isVisible)
        {
            return;
        }

        _isVisible = false;
        UpdateButtons();
        PseudoClasses.Set(RevealedPseudoClass, false);
        IsVisible = false;
        _findInputFocused.Set(false);
        _replaceInputFocused.Set(false);
        if (focusTheEditor)
        {
            _editor.Focus();
        }

        _editor.LayoutOverlayWidget(this);
        RemoveViewZone();
    }

    // The widget's height for the view zone: measured when it has been laid out, else from its desired size.
    private double GetHeight()
    {
        if (Bounds.Height > 0)
        {
            return Bounds.Height + Margin.Top + Margin.Bottom;
        }

        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return DesiredSize.Height;
    }

    private double GetWidth() => Bounds.Width > 0 ? Bounds.Width + Margin.Right : DesiredSize.Width;

    private void ShowViewZone(bool adjustScroll)
    {
        if (!_isVisible || !_controller.FindEditor.FindOptions.AddExtraSpaceOnTop || _editor.ViewModel is null)
        {
            return;
        }

        _viewZone ??= new CodeEditorViewZone { AfterLineNumber = 0, SuppressMouseDown = false };
        var viewZone = _viewZone;
        var newHeight = GetHeight();
        if (newHeight <= 0)
        {
            return;
        }

        _editor.ChangeViewZones(accessor =>
        {
            if (_viewZoneId is { } id)
            {
                // the view zone already exists: update its height
                if (newHeight == viewZone.HeightInPx)
                {
                    return;
                }

                var scrollAdjustment = newHeight - (viewZone.HeightInPx ?? 0);
                viewZone.HeightInPx = newHeight;
                accessor.LayoutZone(id);
                if (adjustScroll)
                {
                    SetScrollTop(GetScrollTop() + scrollAdjustment);
                }

                return;
            }

            viewZone.HeightInPx = newHeight;
            _viewZoneId = accessor.AddZone(viewZone);
            if (adjustScroll)
            {
                SetScrollTop(GetScrollTop() + newHeight);
            }
        });
    }

    private void RemoveViewZone()
    {
        if (_viewZoneId is not { } id)
        {
            return;
        }

        _editor.ChangeViewZones(accessor =>
        {
            accessor.RemoveZone(id);
            _viewZoneId = null;
            if (_viewZone is { } zone)
            {
                SetScrollTop(GetScrollTop() - (zone.HeightInPx ?? 0));
                _viewZone = null;
            }
        });
    }

    private double GetScrollTop() => _editor.ViewModel?.ViewLayout.GetCurrentScrollTop() ?? 0;

    private void SetScrollTop(double scrollTop) =>
        _editor.ViewModel?.ViewLayout.SetScrollPosition(new NewScrollPosition(ScrollTop: Math.Max(0, scrollTop)), ScrollType.Immediate);

    private void TryUpdateWidgetWidth()
    {
        if (!_isVisible)
        {
            return;
        }

        var editorWidth = _editor.GetLayoutSize().Width;
        if (editorWidth <= 0)
        {
            return;
        }

        PseudoClasses.Set(NarrowPseudoClass, editorWidth < NarrowEditorWidth);

        // the minimal left offset of the widget is 15px
        MaxWidth = Math.Max(0, editorWidth - Margin.Right - 15);
    }

    // ----- focus

    /// <summary>Focuses the find input and selects its text (upstream's focusFindInput).</summary>
    public void FocusFindInput()
    {
        if (_findInput is not { } input)
        {
            return;
        }

        input.SelectAll();
        input.Focus();
    }

    /// <summary>Focuses the replace input and selects its text (upstream's focusReplaceInput).</summary>
    public void FocusReplaceInput()
    {
        if (_replaceInput is not { } input)
        {
            return;
        }

        input.SelectAll();
        input.Focus();
    }

    /// <summary>Focuses the input that had the focus last (upstream's focusLastElement, without the buttons).</summary>
    public void FocusLastElement()
    {
        if (LastFocusedInputWasReplace && _isReplaceVisible)
        {
            FocusReplaceInput();
        }
        else
        {
            FocusFindInput();
        }
    }

    /// <summary>Upstream's highlightFindOptions (a short flash of the toggles); not ported, the toggles show their state.</summary>
    public void HighlightFindOptions()
    {
    }

    private void OnFindInputGotFocus(object? sender, FocusChangedEventArgs e)
    {
        _findInputFocused.Set(true);
        LastFocusedInputWasReplace = false;
    }

    private void OnFindInputLostFocus(object? sender, FocusChangedEventArgs e) => _findInputFocused.Set(false);

    private void OnReplaceInputGotFocus(object? sender, FocusChangedEventArgs e)
    {
        _replaceInputFocused.Set(true);
        LastFocusedInputWasReplace = true;
    }

    private void OnReplaceInputLostFocus(object? sender, FocusChangedEventArgs e) => _replaceInputFocused.Set(false);

    // Ctrl+Down (Cmd+Down) in an input gives the focus to the editor (upstream's _onFindInputKeyDown).
    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        var ctrlCmd = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        if (e.Key == Key.Down && e.KeyModifiers == ctrlCmd)
        {
            _editor.Focus();
            e.Handled = true;
        }
    }

    // ----- input

    private void OnFindInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_ignoreChangeEvent || _findInput is null)
        {
            return;
        }

        _state.Change(new NewFindReplaceState { SearchString = _findInput.Text ?? string.Empty }, true);
    }

    private void OnReplaceInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_ignoreChangeEvent || _replaceInput is null)
        {
            return;
        }

        _state.Change(new NewFindReplaceState { ReplaceString = _replaceInput.Text ?? string.Empty }, false);
    }

    private void OnToggleReplaceClick(object? sender, RoutedEventArgs e)
    {
        _state.Change(new NewFindReplaceState { IsReplaceRevealed = !_isReplaceVisible }, false);
        if (_isReplaceVisible)
        {
            FocusReplaceInput();
        }
    }

    private void OnMatchCaseClick(object? sender, RoutedEventArgs e)
    {
        _state.Change(new NewFindReplaceState { MatchCase = !_state.MatchCase }, true);
        RefocusInput();
    }

    private void OnWholeWordClick(object? sender, RoutedEventArgs e)
    {
        _state.Change(new NewFindReplaceState { WholeWord = !_state.WholeWord }, true);
        RefocusInput();
    }

    private void OnRegexClick(object? sender, RoutedEventArgs e)
    {
        _state.Change(new NewFindReplaceState { IsRegex = !_state.IsRegex }, true);
        RefocusInput();
    }

    private void OnPreserveCaseClick(object? sender, RoutedEventArgs e)
    {
        _state.Change(new NewFindReplaceState { PreserveCase = !_state.PreserveCase }, false);
        _replaceInput?.Focus();
    }

    // a toggle clicked with the pointer gives the focus back to the find input (upstream's fixFocusOnOptionClickEnabled)
    private void RefocusInput() => _findInput?.Focus();

    private void OnPreviousClick(object? sender, RoutedEventArgs e) => _ = FindActions.MatchFind(_controller, next: false);

    private void OnNextClick(object? sender, RoutedEventArgs e) => _ = FindActions.MatchFind(_controller, next: true);

    private void OnFindInSelectionClick(object? sender, RoutedEventArgs e)
    {
        if (_state.SearchScope is not null)
        {
            _state.Change(new NewFindReplaceState { SearchScope = null }, true);
            return;
        }

        if (_editor.GetSelections() is not { Count: > 0 } selections)
        {
            return;
        }

        var model = _editor.Model;
        var scopes = new List<TextRange>(selections.Count);
        foreach (var original in selections)
        {
            var selection = original;
            if (selection.EndColumn == 1 && selection.EndLineNumber > selection.StartLineNumber)
            {
                selection = selection.SetEndPosition(selection.EndLineNumber - 1, model.GetLineMaxColumn(selection.EndLineNumber - 1));
            }

            if (!selection.IsEmpty)
            {
                scopes.Add(selection);
            }
        }

        if (scopes.Count > 0)
        {
            _state.Change(new NewFindReplaceState { SearchScope = scopes }, true);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => _controller.CloseFindWidget();

    private void OnReplaceClick(object? sender, RoutedEventArgs e) => _controller.Replace();

    private void OnReplaceAllClick(object? sender, RoutedEventArgs e) => _controller.ReplaceAll();
}
