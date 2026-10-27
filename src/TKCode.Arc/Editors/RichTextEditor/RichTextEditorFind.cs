using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The find bar at the top right of the editor: a search field with the match count, previous and next, match case and close, and a
/// replace row (a field, Replace and Replace all) that Ctrl+H or its toggle opens.
/// </summary>
internal sealed class RichTextEditorFindBar : RichTextEditorFloatingSurface
{
    private readonly StackPanel _content;
    private readonly StackPanel _replaceRow;

    public RichTextEditorFindBar()
    {
        FindBox = NewBox("Find", "Find");
        ReplaceBox = NewBox("Replace with", "Replace");
        Count = new TextBlock { MinWidth = 52, Margin = new Thickness(6, 0), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, TextAlignment = TextAlignment.Right };
        ArcTokens.Bind(Count, TextBlock.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextMuted);
        MatchCase = new RichTextEditorToolButton(ArcIcons.ALargeSmall, "Match case", true);
        Previous = new RichTextEditorToolButton(ArcIcons.ArrowUp, "Previous match");
        Next = new RichTextEditorToolButton(ArcIcons.ArrowDown, "Next match");
        ToggleReplace = new RichTextEditorToolButton(ArcIcons.Replace, "Replace", true);
        Close = new RichTextEditorToolButton(ArcIcons.X, "Close find");
        ReplaceOne = new RichTextEditorToolButton(ArcIcons.Replace, "Replace match");
        ReplaceEvery = new RichTextEditorToolButton(ArcIcons.ReplaceAll, "Replace all");
        Tooltip.SetTip(MatchCase, "Match case");
        Tooltip.SetTip(Previous, "Previous match (⇧Enter)");
        Tooltip.SetTip(Next, "Next match (Enter)");
        Tooltip.SetTip(ToggleReplace, "Replace (⌘H)");
        Tooltip.SetTip(Close, "Close (Esc)");
        Tooltip.SetTip(ReplaceOne, "Replace (Enter)");
        Tooltip.SetTip(ReplaceEvery, "Replace all");
        var findRow = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 2,
            Children = { FindBox, Count, MatchCase, Previous, Next, ToggleReplace, Close }
        };
        _replaceRow = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 2, IsVisible = false,
            Children = { ReplaceBox, ReplaceOne, ReplaceEvery }
        };
        _content = new StackPanel { Spacing = 4, Margin = new Thickness(6), Children = { findRow, _replaceRow } };
        Host.Children.Add(_content);
        AutomationProperties.SetName(this, "Find and replace");
    }

    private static TextBox NewBox(string placeholder, string name)
    {
        var box = SelectInputBox.Create();
        box.Width = 200;
        box.Height = 30;
        box.Padding = new Thickness(8, 0);
        box.PlaceholderText = placeholder;
        box.Classes.Add("rich-text-link-input");
        AutomationProperties.SetName(box, name);
        return box;
    }

    public TextBox FindBox { get; }
    public TextBox ReplaceBox { get; }
    public TextBlock Count { get; }
    public RichTextEditorToolButton MatchCase { get; }
    public RichTextEditorToolButton Previous { get; }
    public RichTextEditorToolButton Next { get; }
    public RichTextEditorToolButton ToggleReplace { get; }
    public RichTextEditorToolButton Close { get; }
    public RichTextEditorToolButton ReplaceOne { get; }
    public RichTextEditorToolButton ReplaceEvery { get; }

    /// <summary>True when the replace row shows.</summary>
    public bool ShowsReplace
    {
        get => _replaceRow.IsVisible;
        set
        {
            _replaceRow.IsVisible = value;
            ToggleReplace.IsChecked = value;
            InvalidateMeasure();
        }
    }

    /// <summary>Puts the bar's top right corner at <paramref name="right"/>, <paramref name="top"/> (overlay coordinates).</summary>
    public void Place(double right, double top)
    {
        _content.Measure(Size.Infinity);
        var size = new Size(_content.DesiredSize.Width + 2, _content.DesiredSize.Height + 2);
        SetSurfaceSize(size);
        SetPlacement(Math.Max(0, right - size.Width), top, 1, 0, 0);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _content.Measure(Size.Infinity);
        base.MeasureOverride(availableSize);
        return new Size(_content.DesiredSize.Width + 2, _content.DesiredSize.Height + 2);
    }
}

public partial class RichTextEditor
{
    private RichTextEditorFindBar? _findBar;
    private List<RichTextEditorMatch> _matches = new();
    private int _matchIndex = -1;

    internal RichTextEditorFindBar? FindBar => _findBar;

    /// <summary>True while the find bar is open.</summary>
    public bool IsFindOpen => _findBar is { IsVisible: true };

    private bool IsFindFocused => _findBar is { IsVisible: true, IsKeyboardFocusWithin: true };

    /// <summary>
    /// Opens the find bar (Ctrl+F; with <paramref name="replace"/>, the replace row too, Ctrl+H) and puts the focus in its search field,
    /// filled with the selected text when the selection is a piece of one line. Matches highlight as you type; Enter and Shift+Enter (or
    /// F3 and Shift+F3) step through them, Escape closes the bar. A read-only editor finds but does not replace.
    /// </summary>
    public void OpenFind(bool replace = false)
    {
        EnsureOverlay();
        CloseSlash();
        var bar = _findBar ??= CreateFindBar();
        bar.ShowsReplace = replace && !IsReadOnly || bar.ShowsReplace && bar.IsVisible && !IsReadOnly;
        var selection = _engine.Selection;
        if (!selection.IsCollapsed && selection.Start.Block == selection.End.Block && _engine.Blocks[selection.Start.Block].IsTextual)
        {
            var text = _engine.Blocks[selection.Start.Block].Text[selection.Start.Offset..selection.End.Offset];
            if (text.Length is > 0 and <= 200 && text.IndexOf('\n') < 0)
            {
                bar.FindBox.Text = text;
            }
        }
        bar.IsVisible = true;
        _toolbar?.Dismiss();
        PlaceFindBar();
        bar.FindBox.Focus();
        bar.FindBox.SelectAll();
        RefreshMatches(select: true);
    }

    /// <summary>Closes the find bar, clears the highlights and returns the focus to the text (the last match stays selected).</summary>
    public void CloseFind()
    {
        if (!IsFindOpen)
        {
            return;
        }
        _findBar!.IsVisible = false;
        _matches = new List<RichTextEditorMatch>();
        _matchIndex = -1;
        _surface?.SetMatches(null, -1);
        FocusEditor();
    }

    /// <summary>How many times <paramref name="text"/> occurs in the document (inside single blocks; case-insensitive unless <paramref name="matchCase"/>).</summary>
    public int CountMatches(string text, bool matchCase = false) => _engine.FindAll(text, matchCase).Count;

    /// <summary>
    /// Replaces every occurrence of <paramref name="text"/> with <paramref name="replacement"/> as one undo step; each replacement takes
    /// the style of the first character it replaces. Returns how many were replaced (0 in a read-only editor).
    /// </summary>
    public int ReplaceAll(string text, string replacement, bool matchCase = false)
    {
        if (IsReadOnly)
        {
            return 0;
        }
        var count = _engine.ReplaceAll(text, replacement ?? string.Empty, matchCase);
        if (count > 0)
        {
            Announce(count == 1 ? "1 replaced" : $"{count} replaced");
        }
        return count;
    }

    private RichTextEditorFindBar CreateFindBar()
    {
        var bar = new RichTextEditorFindBar();
        _overlay!.Children.Add(bar);
        bar.FindBox.TextChanged += (_, _) => RefreshMatches(select: true);
        bar.FindBox.KeyDown += OnFindKeyDown;
        bar.ReplaceBox.KeyDown += OnFindKeyDown;
        bar.MatchCase.Click += (_, _) =>
        {
            bar.MatchCase.IsChecked = !bar.MatchCase.IsChecked;
            RefreshMatches(select: true);
        };
        bar.Previous.Click += (_, _) => StepMatch(-1);
        bar.Next.Click += (_, _) => StepMatch(1);
        bar.ToggleReplace.Click += (_, _) =>
        {
            bar.ShowsReplace = !bar.ShowsReplace && !IsReadOnly;
            PlaceFindBar();
        };
        bar.Close.Click += (_, _) => CloseFind();
        bar.ReplaceOne.Click += (_, _) => ReplaceCurrent();
        bar.ReplaceEvery.Click += (_, _) =>
        {
            ReplaceAll(bar.FindBox.Text ?? string.Empty, bar.ReplaceBox.Text ?? string.Empty, bar.MatchCase.IsChecked);
            RefreshMatches(select: false);
        };
        return bar;
    }

    private void OnFindKeyDown(object? sender, KeyEventArgs e)
    {
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        switch (e.Key)
        {
            case Key.Escape:
                CloseFind();
                e.Handled = true;
                break;
            case Key.Enter when ReferenceEquals(sender, _findBar?.ReplaceBox):
                ReplaceCurrent();
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.F3:
                StepMatch(shift ? -1 : 1);
                e.Handled = true;
                break;
            case Key.H when EditorsHotkeys.IsMod(e.KeyModifiers) && !IsReadOnly:
                _findBar!.ShowsReplace = true;
                PlaceFindBar();
                _findBar.ReplaceBox.Focus();
                e.Handled = true;
                break;
        }
    }

    /// <summary>Finds the query again; with <paramref name="select"/> the match at or after the selection becomes the current one and is selected.</summary>
    private void RefreshMatches(bool select)
    {
        if (_findBar is not { } bar)
        {
            return;
        }
        var query = bar.FindBox.Text ?? string.Empty;
        _matches = _engine.FindAll(query, bar.MatchCase.IsChecked);
        _matchIndex = _engine.MatchFrom(_matches, _engine.Selection.Start);
        if (select && _matchIndex >= 0)
        {
            ShowMatch(_matchIndex, announce: false);
            return;
        }
        UpdateFindChrome();
    }

    private void StepMatch(int delta)
    {
        if (_matches.Count == 0)
        {
            RefreshMatches(select: true);
            return;
        }
        var current = _matchIndex >= 0 && IsSelected(_matches[_matchIndex]);
        var index = current ? _matchIndex + delta : delta > 0 ? _engine.MatchFrom(_matches, _engine.Selection.Start) : _engine.MatchFrom(_matches, _engine.Selection.Start) - 1;
        ShowMatch(((index % _matches.Count) + _matches.Count) % _matches.Count, announce: true);
    }

    private bool IsSelected(RichTextEditorMatch match)
    {
        var selection = _engine.Selection;
        return selection.Start == new RichTextEditorPosition(match.Block, match.Start) && selection.End == new RichTextEditorPosition(match.Block, match.End);
    }

    private void ShowMatch(int index, bool announce)
    {
        _matchIndex = index;
        var match = _matches[index];
        _engine.SetSelection(RichTextEditorSelection.Range(match.Block, match.Start, match.End));
        UpdateFindChrome();
        RevealCaret();
        if (announce)
        {
            Announce($"{index + 1} of {_matches.Count}");
        }
    }

    /// <summary>Replaces the current match and moves on to the next one; when no match is selected yet, it selects one first.</summary>
    private void ReplaceCurrent()
    {
        if (IsReadOnly || _findBar is not { } bar)
        {
            return;
        }
        if (_matchIndex < 0 || _matchIndex >= _matches.Count || !IsSelected(_matches[_matchIndex]))
        {
            StepMatch(1);
            return;
        }
        var query = bar.FindBox.Text ?? string.Empty;
        if (!_engine.ReplaceMatch(_matches[_matchIndex], query, bar.ReplaceBox.Text ?? string.Empty, bar.MatchCase.IsChecked))
        {
            RefreshMatches(select: true);
            return;
        }
        Announce("Replaced");
        _matches = _engine.FindAll(query, bar.MatchCase.IsChecked);
        _matchIndex = _engine.MatchFrom(_matches, _engine.Selection.End);
        if (_matchIndex >= 0)
        {
            ShowMatch(_matchIndex, announce: false);
        }
        else
        {
            UpdateFindChrome();
        }
    }

    private void UpdateFindChrome()
    {
        if (_findBar is not { } bar)
        {
            return;
        }
        var query = bar.FindBox.Text ?? string.Empty;
        bar.Count.Text = query.Length == 0 ? string.Empty
            : _matches.Count == 0 ? "No results"
            : _matchIndex >= 0 ? $"{_matchIndex + 1} of {_matches.Count}"
            : $"{_matches.Count}";
        bar.Previous.IsEnabled = _matches.Count > 0;
        bar.Next.IsEnabled = _matches.Count > 0;
        bar.ReplaceOne.IsEnabled = _matches.Count > 0 && !IsReadOnly;
        bar.ReplaceEvery.IsEnabled = _matches.Count > 0 && !IsReadOnly;
        _surface?.SetMatches(_matches, _matchIndex);
        PlaceFindBar();
    }

    /// <summary>Keeps the bar at the top right of the part of the editor that is in view.</summary>
    private void PlaceFindBar()
    {
        if (_findBar is not { IsVisible: true } bar)
        {
            return;
        }
        var band = ClipBand();
        var top = Math.Max(8, double.IsFinite(band.Top) ? band.Top + 8 : 8);
        bar.Place(Bounds.Width - 8, Math.Min(top, Math.Max(8, Bounds.Height - 48)));
    }

    /// <summary>After an edit while the bar is open: the matches follow the text, and the current one is the one at the selection.</summary>
    private void OnContentChangedForFind()
    {
        if (!IsFindOpen)
        {
            return;
        }
        RefreshMatches(select: false);
    }
}
