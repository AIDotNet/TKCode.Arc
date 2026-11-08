using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>
/// The floating search bar of a <see cref="PdfViewer"/>: a field, the count, previous and next, match case and whole word, close.
/// All state lives in the viewer; this shows it and passes on what the user does. Enter and Escape in the field bubble to the
/// viewer's keyboard handling like every other key.
/// </summary>
[TemplatePart(InputPart, typeof(Input))]
[TemplatePart(CountPart, typeof(TextBlock))]
[TemplatePart(PreviousPart, typeof(Button))]
[TemplatePart(NextPart, typeof(Button))]
[TemplatePart(MatchCasePart, typeof(PdfToolbarToggle))]
[TemplatePart(WholeWordPart, typeof(PdfToolbarToggle))]
[TemplatePart(ClosePart, typeof(Button))]
[PseudoClasses(NarrowPseudoClass)]
internal sealed class PdfSearchBar : TemplatedControl
{
    public const string InputPart = "PART_Input";
    public const string CountPart = "PART_Count";
    public const string PreviousPart = "PART_Previous";
    public const string NextPart = "PART_Next";
    public const string MatchCasePart = "PART_MatchCase";
    public const string WholeWordPart = "PART_WholeWord";
    public const string ClosePart = "PART_Close";
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Lucide "whole-word"; not in Core's subset, and only this bar draws it.</summary>
    public static Avalonia.Media.Geometry WholeWordGlyph { get; } = ArcIcons.Parse("M4 12a3 3 0 1 0 6 0a3 3 0 1 0 -6 0z M10 9v6 M14 12a3 3 0 1 0 6 0a3 3 0 1 0 -6 0z M14 7v8 M22 17v1c0 .5-.5 1-1 1H3c-.5 0-1-.5-1-1v-1");

    private readonly PdfViewer _viewer;
    private Input? _input;
    private TextBlock? _count;
    private Button? _previous;
    private Button? _next;
    private PdfToolbarToggle? _matchCase;
    private PdfToolbarToggle? _wholeWord;
    private Button? _close;
    private bool _syncing;
    private bool _focusWanted;

    public PdfSearchBar(PdfViewer viewer)
    {
        _viewer = viewer;
    }

    /// <summary>The search field (tests).</summary>
    public Input? InputField => _input;

    /// <summary>The count on show (tests).</summary>
    public string? CountText => _count?.Text;

    /// <summary>What the count says to a screen reader (tests).</summary>
    public string? Announcement => _count is { } count ? AutomationProperties.GetName(count) : null;

    /// <summary>Whether a descendant has the keyboard focus.</summary>
    public bool HoldsFocus => _input?.IsKeyboardFocusWithin == true || IsKeyboardFocusWithin;

    public void SetNarrow(bool narrow) => PseudoClasses.Set(NarrowPseudoClass, narrow);

    /// <summary>Focuses the field with its text selected, as soon as it can take focus.</summary>
    public void FocusField()
    {
        _focusWanted = true;
        Dispatcher.UIThread.Post(() =>
        {
            if (_focusWanted && _input is { } input)
            {
                _focusWanted = false;
                input.Focus(NavigationMethod.Tab);
                input.SelectAll();
            }
        }, DispatcherPriority.Input);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_input is not null)
        {
            _input.TextChanged -= OnTextChanged;
        }
        Unhook(_previous, OnPreviousClick);
        Unhook(_next, OnNextClick);
        Unhook(_matchCase, OnMatchCaseClick);
        Unhook(_wholeWord, OnWholeWordClick);
        Unhook(_close, OnCloseClick);
        _input = e.NameScope.Find<Input>(InputPart);
        _count = e.NameScope.Find<TextBlock>(CountPart);
        _previous = e.NameScope.Find<Button>(PreviousPart);
        _next = e.NameScope.Find<Button>(NextPart);
        _matchCase = e.NameScope.Find<PdfToolbarToggle>(MatchCasePart);
        _wholeWord = e.NameScope.Find<PdfToolbarToggle>(WholeWordPart);
        _close = e.NameScope.Find<Button>(ClosePart);
        if (_input is not null)
        {
            _input.TextChanged += OnTextChanged;
        }
        Hook(_previous, OnPreviousClick);
        Hook(_next, OnNextClick);
        Hook(_matchCase, OnMatchCaseClick);
        Hook(_wholeWord, OnWholeWordClick);
        Hook(_close, OnCloseClick);
        Sync();
        Announce();
        if (_focusWanted)
        {
            FocusField();
        }
    }

    private static void Hook(Button? button, EventHandler<RoutedEventArgs> handler)
    {
        if (button is not null)
        {
            button.Click += handler;
        }
    }

    private static void Unhook(Button? button, EventHandler<RoutedEventArgs> handler)
    {
        if (button is not null)
        {
            button.Click -= handler;
        }
    }

    private void OnPreviousClick(object? sender, RoutedEventArgs e) => _viewer.SearchPrevious();

    private void OnNextClick(object? sender, RoutedEventArgs e) => _viewer.SearchNext();

    private void OnMatchCaseClick(object? sender, RoutedEventArgs e) => _viewer.SetCurrentValue(PdfViewer.IsMatchCaseProperty, !_viewer.IsMatchCase);

    private void OnWholeWordClick(object? sender, RoutedEventArgs e) => _viewer.SetCurrentValue(PdfViewer.IsWholeWordProperty, !_viewer.IsWholeWord);

    private void OnCloseClick(object? sender, RoutedEventArgs e) => _viewer.SetCurrentValue(PdfViewer.IsSearchOpenProperty, false);

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_syncing)
        {
            _viewer.SetCurrentValue(PdfViewer.SearchTextProperty, _input?.Text ?? string.Empty);
        }
    }

    /// <summary>Shows the viewer's search state.</summary>
    public void Sync()
    {
        if (_input is { } input && (input.Text ?? string.Empty) != _viewer.SearchText)
        {
            _syncing = true;
            input.Text = _viewer.SearchText;
            _syncing = false;
        }
        if (_matchCase is { } matchCase)
        {
            matchCase.IsActive = _viewer.IsMatchCase;
        }
        if (_wholeWord is { } wholeWord)
        {
            wholeWord.IsActive = _viewer.IsWholeWord;
        }
        var count = _viewer.SearchMatchCount;
        if (_previous is { } previous)
        {
            previous.IsEnabled = count > 0;
        }
        if (_next is { } next)
        {
            next.IsEnabled = count > 0;
        }
        if (_count is { } text)
        {
            text.Text = CountDisplay();
        }
    }

    /// <summary>Updates what a screen reader is told. Not on every page of a scan: it hears of a finished scan and of a move to another result.</summary>
    public void Announce()
    {
        if (_count is { } count)
        {
            AutomationProperties.SetName(count, Spoken());
        }
    }

    private string CountDisplay()
    {
        if (_viewer.SearchText.Length == 0 || (_viewer.IsSearchPending && !_viewer.IsSearching && _viewer.SearchMatchCount == 0))
        {
            return string.Empty;
        }
        var count = _viewer.SearchMatchCount;
        if (_viewer.IsSearching)
        {
            return string.Create(System.Globalization.CultureInfo.CurrentCulture, $"{_viewer.CurrentSearchMatch + 1} / {count}+");
        }
        return count == 0
            ? PdfViewerLangResources.Get(PdfViewerLangResourceKind.NoResults)
            : string.Create(System.Globalization.CultureInfo.CurrentCulture, $"{_viewer.CurrentSearchMatch + 1} / {count}");
    }

    private string Spoken()
    {
        if (!_viewer.IsSearchOpen || _viewer.SearchText.Length == 0 || _viewer.IsSearching || _viewer.IsSearchPending)
        {
            return string.Empty;
        }
        var count = _viewer.SearchMatchCount;
        return count == 0
            ? PdfViewerLangResources.Get(PdfViewerLangResourceKind.NoResults)
            : PdfViewerLangResources.Format(PdfViewerLangResourceKind.MatchPosition, _viewer.CurrentSearchMatch + 1, count);
    }
}
