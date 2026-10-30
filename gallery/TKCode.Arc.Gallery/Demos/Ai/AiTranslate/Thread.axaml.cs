using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Two messages that follow one preferred language.</summary>
public partial class AiTranslateThreadDemo : UserControl
{
    private readonly bool _ready;

    public AiTranslateThreadDemo()
    {
        InitializeComponent();
        Second.Author = new AiTranslateAuthor { Name = "Haruto Sato", Role = "Design" };
        // Demo content: the second message's stored translations.
        Second.Translations = new Dictionary<AiTranslateLanguage, string>
        {
            [AiTranslateLanguage.English] = "Tomorrow's weekly meeting now starts at 10:00.",
            [AiTranslateLanguage.German] = "Das wöchentliche Meeting morgen beginnt jetzt um 10:00 Uhr.",
            [AiTranslateLanguage.Spanish] = "La reunión semanal de mañana ahora empieza a las 10:00.",
        };
        // A reader who picks a language in one card sets it for the whole thread.
        First.ValueChanged += (_, e) => Follow(e);
        Second.ValueChanged += (_, e) => Follow(e);
        _ready = true;
    }

    private void OnPreferredChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The control applies SelectedValue while the XAML loads, before the constructor has run.
        if (!_ready)
        {
            return;
        }
        AiTranslateLanguage? language = Enum.TryParse<AiTranslateLanguage>(Preferred.SelectedValue as string, out var picked) ? picked : null;
        First.Value = language;
        Second.Value = language;
    }

    private void Follow(AiTranslateValueChangedEventArgs e)
    {
        var value = e.IsOriginal ? "original" : e.Language.ToString();
        if (!Equals(Preferred.SelectedValue, value))
        {
            Preferred.SelectedValue = value;
        }
    }
}
