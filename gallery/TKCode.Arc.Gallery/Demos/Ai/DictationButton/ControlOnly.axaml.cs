using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>DictationControl in a composer of its own: settled words in the text color, the guess muted.</summary>
public partial class DictationButtonControlOnlyDemo : UserControl
{
    private readonly Run _text = new();
    private readonly Run _final = new();
    private readonly Run _interim = new();
    private string _note = string.Empty;

    public DictationButtonControlOnlyDemo()
    {
        InitializeComponent();
        ArcTokens.Bind(_interim, Run.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        Note.Inlines = new InlineCollection { _text, _final, _interim };
        Mic.TranscriptChanged += OnTranscript;
        Mic.Committed += OnCommitted;
        Mic.Cancelled += OnCancelled;
        Show(string.Empty, string.Empty);
    }

    private void OnTranscript(object? sender, DictationTranscriptEventArgs e) => Show(e.Final, e.Interim);

    private void OnCommitted(object? sender, DictationCommittedEventArgs e)
    {
        _note = _note.Length == 0 ? e.Text : _note + " " + e.Text;
        Show(string.Empty, string.Empty);
    }

    private void OnCancelled(object? sender, RoutedEventArgs e) => Show(string.Empty, string.Empty);

    private void Show(string final, string interim)
    {
        if (_note.Length == 0 && final.Length == 0 && interim.Length == 0)
        {
            // An empty note reads as a muted prompt.
            _text.Text = string.Empty;
            _final.Text = string.Empty;
            _interim.Text = "Press the mic and speak a note.";
            return;
        }
        _text.Text = _note;
        _final.Text = final.Length > 0 ? Lead(_note) + final : string.Empty;
        _interim.Text = (interim.Length > 0 ? Lead(_note + final) : string.Empty) + interim;
    }

    private static string Lead(string before) => before.Length == 0 || before.EndsWith(' ') ? string.Empty : " ";
}
