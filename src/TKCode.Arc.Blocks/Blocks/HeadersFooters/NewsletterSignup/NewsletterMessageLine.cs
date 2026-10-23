// Ported from Arc's free newsletter-signup block (MIT, Copyright (c) 2026 Elia Kuratli): the message line under the form.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>What the message line says.</summary>
internal enum NewsletterMessageKind
{
    /// <summary>The privacy note and its link.</summary>
    Note,

    /// <summary>An error (the address is missing or malformed, or the send failed).</summary>
    Problem,

    /// <summary>The confirmation and the "use a different email" button.</summary>
    Done
}

/// <summary>
/// The line under the form. Every text it can show (the note with its link, the confirmation with its button, the three
/// errors) sits in the same cell, unseen, so the line is always as tall as the longest and nothing below it moves; the one
/// that is current swaps in place (<see cref="SiteSwap"/>, old and new overlapping). The note is muted, an error is the
/// danger color, the confirmation is secondary. The inset at the start is the control's own margin (22, or 4 when narrow).
/// </summary>
internal sealed class NewsletterMessageLine : Panel
{
    private const double LineHeight = 19.6;
    private const double Tracking = -0.14;

    private readonly SiteSwap _swap = new()
    {
        HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
        VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Top
    };

    private readonly List<TextBlock> _sizers = new();
    private NewsletterMessageKind _kind;
    private string? _problem;
    private string _note = "No tracking pixels. Unsubscribe with one click.";
    private string _linkLabel = "Privacy policy";
    private bool _linkVisible = true;
    private string _done = "Check your inbox to confirm.";
    private string _different = "Use a different email";
    private string _empty = "Enter your email address";
    private string _format = "Enter an email like name@company.com";
    private string _failed = "That didn't go through. Your email is kept, so try again.";
    private string? _key;
    private NewsletterLink? _link;
    private NewsletterLink? _again;

    public NewsletterMessageLine()
    {
        Children.Add(_swap);
        Rebuild(sameKeyReplaces: true);
    }

    /// <summary>The privacy link was pressed.</summary>
    public event EventHandler? LinkInvoked;

    /// <summary>"Use a different email" was pressed.</summary>
    public event EventHandler? DifferentEmailInvoked;

    /// <summary>What the line says now.</summary>
    public NewsletterMessageKind Kind => _kind;

    /// <summary>The error text while <see cref="Kind"/> is <see cref="NewsletterMessageKind.Problem"/>.</summary>
    public string? ProblemText => _problem;

    /// <summary>Shows a message: the swap runs once, even when the kind and the error text both change.</summary>
    public void Show(NewsletterMessageKind kind, string? problem = null)
    {
        _kind = kind;
        _problem = kind == NewsletterMessageKind.Problem ? problem : null;
        Rebuild(sameKeyReplaces: false);
    }

    public string NoteText { get => _note; set { if (_note != value) { _note = value; Rebuild(sameKeyReplaces: true); } } }

    public string LinkLabel { get => _linkLabel; set { if (_linkLabel != value) { _linkLabel = value; Rebuild(sameKeyReplaces: true); } } }

    public bool IsLinkVisible { get => _linkVisible; set { if (_linkVisible != value) { _linkVisible = value; Rebuild(sameKeyReplaces: true); } } }

    public string DoneText { get => _done; set { if (_done != value) { _done = value; Rebuild(sameKeyReplaces: true); } } }

    public string DifferentEmailLabel { get => _different; set { if (_different != value) { _different = value; Rebuild(sameKeyReplaces: true); } } }

    public string EmptyMessage { get => _empty; set { if (_empty != value) { _empty = value; Rebuild(sameKeyReplaces: true); } } }

    public string FormatMessage { get => _format; set { if (_format != value) { _format = value; Rebuild(sameKeyReplaces: true); } } }

    public string FailedMessage { get => _failed; set { if (_failed != value) { _failed = value; Rebuild(sameKeyReplaces: true); } } }

    /// <summary>The swap that shows the message (tests read it).</summary>
    public SiteSwap Swap => _swap;

    /// <summary>The unseen copies that size the line: note and link, confirmation and button, and the three errors.</summary>
    public IReadOnlyList<TextBlock> Sizers => _sizers;

    /// <summary>The privacy link while the note shows.</summary>
    public NewsletterLink? Link => _kind == NewsletterMessageKind.Note ? _link : null;

    /// <summary>"Use a different email" while the confirmation shows.</summary>
    public NewsletterLink? DifferentEmailButton => _kind == NewsletterMessageKind.Done ? _again : null;

    /// <summary>The visible text, as one string (tests and the screen reader description read it).</summary>
    public string VisibleText => _kind switch
    {
        NewsletterMessageKind.Problem => _problem ?? string.Empty,
        NewsletterMessageKind.Done => $"{_done} {_different}",
        _ => _linkVisible ? $"{_note} {_linkLabel}" : _note
    };

    private string KeyFor() => _kind switch
    {
        NewsletterMessageKind.Problem => "problem-" + _problem,
        NewsletterMessageKind.Done => "done",
        _ => "note"
    };

    private void Rebuild(bool sameKeyReplaces)
    {
        RebuildSizers();
        var key = KeyFor();
        if (key == _key && !sameKeyReplaces)
        {
            return;
        }
        // The same message with new words (or a new link) replaces its content; a different message swaps.
        _key = key;
        _swap.Content = BuildContent();
    }

    private void RebuildSizers()
    {
        foreach (var sizer in _sizers)
        {
            Children.Remove(sizer);
        }
        _sizers.Clear();
        var texts = new[]
        {
            _linkVisible ? $"{_note} {_linkLabel}" : _note,
            $"{_done} {_different}",
            _empty,
            _format,
            _failed
        };
        foreach (var text in texts)
        {
            var block = Text(text);
            block.Opacity = 0;
            block.IsHitTestVisible = false;
            AutomationProperties.SetAccessibilityView(block, AccessibilityView.Raw);
            _sizers.Add(block);
            Children.Insert(_sizers.Count - 1, block);
        }
    }

    private static TextBlock Text(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        LineHeight = LineHeight,
        LetterSpacing = Tracking
    };

    private Control BuildContent()
    {
        switch (_kind)
        {
            case NewsletterMessageKind.Problem:
            {
                var block = Text(_problem ?? string.Empty);
                ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.Danger);
                return block;
            }
            case NewsletterMessageKind.Done:
            {
                var panel = new WrapPanel { ItemSpacing = 4 };
                AddWords(panel, _done, ArcDesignTokenKind.TextSecondary);
                _again = CreateLink(_different);
                _again.Click += (_, _) => DifferentEmailInvoked?.Invoke(this, EventArgs.Empty);
                panel.Children.Add(_again);
                return panel;
            }
            default:
            {
                var panel = new WrapPanel { ItemSpacing = 4 };
                AddWords(panel, _note, ArcDesignTokenKind.TextMuted);
                _link = null;
                if (_linkVisible)
                {
                    _link = CreateLink(_linkLabel);
                    _link.Click += (_, _) => LinkInvoked?.Invoke(this, EventArgs.Empty);
                    panel.Children.Add(_link);
                }
                return panel;
            }
        }
    }

    // The words are separate items so that the text and the link flow as one wrapped line (a button cannot sit inside a run).
    private static void AddWords(WrapPanel panel, string text, ArcDesignTokenKind color)
    {
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var block = Text(word);
            block.TextWrapping = TextWrapping.NoWrap;
            ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
            panel.Children.Add(block);
        }
    }

    private static NewsletterLink CreateLink(string label) => new()
    {
        Content = label,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
    };
}
