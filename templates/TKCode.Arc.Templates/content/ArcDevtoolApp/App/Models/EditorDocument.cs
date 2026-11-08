using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using TKCode.Arc;

namespace ArcStarterApp.Models;

/// <summary>
/// A file open in an editor tab, and what the Properties pane shows of it. [PropertyGridModel] has Arc's generator
/// describe the public properties at build time, so the PropertyGrid edits them without reflection; the
/// System.ComponentModel attributes give the categories, names and descriptions.
/// </summary>
[PropertyGridModel]
public sealed class EditorDocument : INotifyPropertyChanged
{
    private string _text;
    private string _saved;
    private bool _wordWrap;
    private bool _isReadOnly;

    public EditorDocument(string path, string text)
    {
        Path = path;
        _text = text;
        _saved = text;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    [Category("File"), ReadOnly(true), Description("The file's name.")]
    public string Name => System.IO.Path.GetFileName(Path);

    [Category("File"), ReadOnly(true), Description("The path from the workspace root.")]
    public string Path { get; }

    [Category("File"), ReadOnly(true), Description("Chosen from the file's extension.")]
    public string Language => System.IO.Path.GetExtension(Path) switch
    {
        ".cs" => "C#",
        ".md" => "Markdown",
        ".json" => "JSON",
        ".axaml" or ".xml" or ".csproj" => "XML",
        _ => "Plain text",
    };

    [Category("File"), ReadOnly(true), Description("Lines in the editor, saved or not.")]
    public int Lines => _text.Count(c => c == '\n') + 1;

    [Category("Editor"), DisplayName("Word wrap"), Description("Wraps long lines at the edge of the editor.")]
    public bool WordWrap
    {
        get => _wordWrap;
        set
        {
            if (Set(ref _wordWrap, value))
            {
                OnPropertyChanged(nameof(Wrapping));
            }
        }
    }

    [Category("Editor"), DisplayName("Read only"), Description("Stops changes to this file.")]
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set => Set(ref _isReadOnly, value);
    }

    /// <summary>The text in the editor.</summary>
    [Browsable(false)]
    public string Text
    {
        get => _text;
        set
        {
            if (Set(ref _text, value))
            {
                OnPropertyChanged(nameof(Lines));
                OnPropertyChanged(nameof(IsModified));
            }
        }
    }

    /// <summary>Whether the text differs from the saved file (the tab shows a dot).</summary>
    [Browsable(false)]
    public bool IsModified => _text != _saved;

    [Browsable(false)]
    public TextWrapping Wrapping => WordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;

    /// <summary>Called after the workspace wrote the text: the tab is no longer modified.</summary>
    public void MarkSaved()
    {
        _saved = _text;
        OnPropertyChanged(nameof(IsModified));
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
