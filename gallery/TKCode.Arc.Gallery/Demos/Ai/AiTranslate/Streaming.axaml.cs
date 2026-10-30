using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A card wired to a translator that streams its reply in small deltas.</summary>
public partial class AiTranslateStreamingDemo : UserControl
{
    // Demo content: what the stand-in model "replies" for each language.
    private static readonly Dictionary<AiTranslateLanguage, string> s_replies = new()
    {
        [AiTranslateLanguage.English] =
            "Hi all, since the update the export has been stuck at 80 percent. Could you please take a look by Friday?",
        [AiTranslateLanguage.French] =
            "Bonjour à tous, depuis la mise à jour, l'export reste bloqué à 80 %. Pourriez-vous y jeter un œil d'ici vendredi ?",
        [AiTranslateLanguage.Japanese] =
            "皆さん、アップデート以降、エクスポートが80%で止まったままです。金曜日までに確認していただけますか？",
    };

    public AiTranslateStreamingDemo()
    {
        InitializeComponent();
        Card.Languages = [AiTranslateLanguage.English, AiTranslateLanguage.German, AiTranslateLanguage.French, AiTranslateLanguage.Japanese];
        Card.Author = new AiTranslateAuthor { Name = "Jonas Weber", Role = "Customer success" };
        Card.Translator = Translate;
        Card.ValueChanged += (_, e) => Log.Text = e.IsOriginal ? "Showing the original" : $"Picked {e.Language}";
        Card.Translated += (_, e) => Log.Text = $"Translated {e.From} to {e.To}: {e.Text.Length} characters, now cached";
        Card.TranslationFailed += (_, e) => Log.Text = $"Failed: {e.Message}";
    }

    /// <summary>A stand-in for your model call: waits like a network request, then streams two words at a time.</summary>
    private static async IAsyncEnumerable<string> Translate(AiTranslateRequest request)
    {
        var token = request.CancellationToken;
        await Task.Delay(400, token);
        if (!s_replies.TryGetValue(request.To, out var reply))
        {
            throw new InvalidOperationException("The translation service is unavailable.");
        }
        var words = reply.Split(' ');
        for (var i = 0; i < words.Length; i += 2)
        {
            yield return string.Join(' ', words.Skip(i).Take(2)) + (i + 2 < words.Length ? " " : string.Empty);
            await Task.Delay(60, token);
        }
    }
}
