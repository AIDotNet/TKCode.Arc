using System.Text.RegularExpressions;

namespace TKCode.Arc;

/// <summary>
/// A tuning session a <see cref="ToneTuner"/> plays without a model, from data you give it: a draft, who it is for, the
/// model the status names and the rewritten versions per tone. Set it as <see cref="ToneTuner.Simulation"/> while the
/// card has no <see cref="ToneTuner.Text"/>: the card then tunes <see cref="Text"/>, answers each tone with its version
/// after <see cref="Latency"/>, says the rewrites are simulated (nothing is sent) and, unless
/// <see cref="ToneTuner.AutoPlay"/> is false, tours the voices by itself until someone touches it.
/// <para>
/// A tone with no version in <see cref="Versions"/> is written by a few plain rules (contractions and greetings for
/// formality, fewer sentences or a closing offer for detail, an opening line for the voice). That is a stand-in so the
/// dials do something in a demo, not a language model.
/// </para>
/// </summary>
public sealed partial class ToneTunerSimulation
{
    private static readonly (string Long, string Short)[] s_contractions =
    [
        ("I am", "I'm"), ("I will", "I'll"), ("I have", "I've"), ("we are", "we're"), ("we have", "we've"),
        ("we will", "we'll"), ("you are", "you're"), ("it is", "it's"), ("that is", "that's"), ("do not", "don't"),
        ("does not", "doesn't"), ("cannot", "can't"), ("will not", "won't"), ("is not", "isn't"), ("are not", "aren't"),
        ("let us", "let's"), ("Thank you", "Thanks"),
    ];

    private static readonly string[] s_signOffs = ["Best", "Best regards", "Kind regards", "Regards", "Cheers", "Thanks", "Warm regards", "Sincerely", "Warmly"];

    /// <summary>The draft to tune, shown at the starting tone (<see cref="ToneSettings.Default"/>). Blank lines split paragraphs.</summary>
    public string? Text { get; set; }

    /// <summary>
    /// The rewritten draft per tone. Keep the sentences a dial does not touch identical between versions, so only the
    /// changed ones crossfade. A tone that is missing is written by the simulation's plain rules.
    /// </summary>
    public IReadOnlyList<ToneTunerVersion>? Versions { get; set; }

    /// <summary>Who the message is for, shown in the header when the card has no <see cref="ToneTuner.RecipientName"/>.</summary>
    public string? RecipientName { get; set; }

    /// <summary>The subject under the recipient's name.</summary>
    public string? RecipientSubject { get; set; }

    /// <summary>The recipient's picture; without it the avatar shows their initials.</summary>
    public ImageSource? RecipientAvatar { get; set; }

    /// <summary>The model the status names while a simulated rewrite runs, when the card has no <see cref="ToneTuner.Model"/>.</summary>
    public string? Model { get; set; }

    /// <summary>How long a simulated rewrite pretends the model takes (650ms by default; zero answers at once).</summary>
    public TimeSpan Latency { get; set; } = TimeSpan.FromMilliseconds(650);

    /// <summary>The draft, normalized the way the card shows it.</summary>
    internal string Original => ToneTunerText.Normalize(Text ?? string.Empty);

    /// <summary>The draft in <paramref name="value"/>'s tone: the version given for it, else the plain rules.</summary>
    internal string Rewrite(ToneSettings value)
    {
        var original = Original;
        if (value == ToneSettings.Default)
        {
            return original;
        }
        if (Versions is { } versions)
        {
            foreach (var version in versions)
            {
                if (version is not null && version.Settings.F == value.F && version.Settings.D == value.D && version.Settings.Voice == value.Voice)
                {
                    return version.Text ?? string.Empty;
                }
            }
        }
        return Rules(original, value);
    }

    /// <summary>
    /// The plain rules: contractions and greetings for formality, fewer sentences or a closing offer for detail, an
    /// opening line for the voice. The draft comes back unchanged at the starting tone.
    /// </summary>
    internal static string Rules(string original, ToneSettings value)
    {
        if (value == ToneSettings.Default)
        {
            return original;
        }
        var paragraphs = ToneTunerText.Paragraphs(original).Select(p => p.Text).ToList();
        if (paragraphs.Count == 0)
        {
            return original;
        }
        var first = IsGreeting(paragraphs[0]) ? 1 : 0;
        var last = paragraphs.Count > first && IsSignOff(paragraphs[^1]) ? paragraphs.Count - 2 : paragraphs.Count - 1;
        for (var i = 0; i < paragraphs.Count; i++)
        {
            var text = paragraphs[i];
            if (i < first)
            {
                text = Greeting(text, value.F);
            }
            else if (i > last)
            {
                text = SignOff(text, value.F);
            }
            else
            {
                text = Formality(text, value.F);
                text = Detail(text, value.D, i == last, value.F);
                if (i == first)
                {
                    text = Voice(text, value.Voice, value.F);
                }
            }
            paragraphs[i] = text;
        }
        return string.Join("\n\n", paragraphs.Where(p => p.Length > 0));
    }

    private static bool IsGreeting(string paragraph) =>
        !paragraph.Contains('\n') && paragraph.Length <= 40 && (paragraph.EndsWith(',') || paragraph.EndsWith('!'));

    private static bool IsSignOff(string paragraph)
    {
        var line = paragraph.Split('\n')[0].TrimEnd(',', '!', '.');
        return s_signOffs.Any(s => string.Equals(s, line, StringComparison.OrdinalIgnoreCase));
    }

    private static string Greeting(string text, int f)
    {
        var match = GreetingPattern().Match(text);
        if (!match.Success)
        {
            return text;
        }
        var name = match.Groups["name"].Value;
        return f switch
        {
            0 => "Hey " + name + ",",
            2 => "Dear " + name + ",",
            _ => "Hi " + name + ",",
        };
    }

    private static string SignOff(string text, int f)
    {
        var lines = text.Split('\n');
        lines[0] = f switch
        {
            0 => "Cheers,",
            2 => "Kind regards,",
            _ => "Best,",
        };
        return string.Join("\n", lines);
    }

    private static string Formality(string text, int f)
    {
        foreach (var (longForm, shortForm) in s_contractions)
        {
            text = f switch
            {
                0 => text.Replace(longForm, shortForm, StringComparison.Ordinal),
                2 => text.Replace(shortForm, longForm, StringComparison.Ordinal),
                _ => text,
            };
        }
        return text;
    }

    private static string Detail(string text, int d, bool last, int f)
    {
        var sentences = ToneTunerText.Pieces(text).Where(p => p.IsSentence).Select(p => text.Substring(p.Start, p.Length)).ToList();
        if (d == 0 && sentences.Count > 2)
        {
            // Brief: the first sentence and the ones with numbers (facts) survive, at most two.
            var kept = sentences.Where((s, i) => i == 0 || s.Any(char.IsDigit)).Take(2).ToList();
            return string.Join(" ", kept);
        }
        if (d == 2 && last)
        {
            return text + " " + (f == 2
                ? "Please let me know if you would like more detail on any of this."
                : "Let me know if you'd like more detail on any of this.");
        }
        return text;
    }

    private static string Voice(string text, ToneVoice voice, int f) => voice switch
    {
        ToneVoice.Friendly => (f == 2 ? "Thank you very much for reaching out. " : "Thanks so much for reaching out! ") + text,
        ToneVoice.Confident => HedgePattern().Replace(text, string.Empty),
        ToneVoice.Empathetic => (f == 2 ? "I am sorry for the trouble this has caused. " : "I'm sorry for the trouble this has caused. ") + text,
        _ => text,
    };

    [GeneratedRegex(@"^(Hi|Hey|Hello|Dear|Good (morning|afternoon|evening))\s+(?<name>[^,!]+)[,!]?$")]
    private static partial Regex GreetingPattern();

    [GeneratedRegex(@"\b(I think|just|maybe|perhaps|hopefully),?\s+", RegexOptions.IgnoreCase)]
    private static partial Regex HedgePattern();
}
