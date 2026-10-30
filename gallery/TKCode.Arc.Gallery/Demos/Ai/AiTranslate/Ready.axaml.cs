using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Ready translations streamed by the built-in simulation, with a missing language.</summary>
public partial class AiTranslateReadyDemo : UserControl
{
    public AiTranslateReadyDemo()
    {
        InitializeComponent();
        // Demo content: translations you stored with the post.
        Card.Translations = new Dictionary<AiTranslateLanguage, string>
        {
            [AiTranslateLanguage.English] = "We have updated the prices of the Team plan. Current customers keep their rate until January.",
            [AiTranslateLanguage.Portuguese] = "Atualizamos os preços do plano Equipe. Os clientes atuais mantêm sua tarifa até janeiro.",
        };
        Card.TranslationFailed += (_, e) => Log.Text = $"Failed ({e.From} to {e.To}): {e.Message}";
    }
}
