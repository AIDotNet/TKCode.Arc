using System.Globalization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One token that a <see cref="MixtureOfExperts"/> routes: its text and, optionally, the router's raw score for every
/// expert. (The name ends in Item because <c>MixtureOfExpertsToken</c> is the control's design token.)
/// </summary>
public class MixtureOfExpertsTokenItem : AvaloniaObject
{
    /// <summary>
    /// The token as the model sees it (spaces are kept, so " the" and "the" are different tokens). It is shown in the
    /// input, output and sequence chips and named in the footer.
    /// </summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MixtureOfExpertsTokenItem, string>(nameof(Text), string.Empty);

    /// <summary>
    /// The router's raw score for each expert, in the order of <see cref="MixtureOfExperts.Experts"/> ("24, 6, 3, 58"
    /// in XAML). Scores are non-negative and are normalized to sum to 1; missing entries count as 0 and extra ones are
    /// ignored. Null derives stable example scores from <see cref="Text"/>.
    /// </summary>
    public static readonly StyledProperty<AvaloniaList<double>?> ScoresProperty =
        AvaloniaProperty.Register<MixtureOfExpertsTokenItem, AvaloniaList<double>?>(nameof(Scores));

    /// <summary>Creates an empty token.</summary>
    public MixtureOfExpertsTokenItem()
    {
    }

    /// <summary>Creates a token with its text and, optionally, one raw score per expert.</summary>
    public MixtureOfExpertsTokenItem(string text, params double[] scores)
    {
        Text = text;
        if (scores.Length > 0)
        {
            Scores = new AvaloniaList<double>(scores);
        }
    }

    /// <inheritdoc cref="TextProperty"/>
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="ScoresProperty"/>
    public AvaloniaList<double>? Scores { get => GetValue(ScoresProperty); set => SetValue(ScoresProperty, value); }
}

/// <summary>One expert of a <see cref="MixtureOfExperts"/>: a short name, what it tends to handle and an icon.</summary>
public class MixtureOfExpertsExpert : AvaloniaObject
{
    /// <summary>The expert's name, best one short word (the narrow layout shows only the icon and speaks the name).</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<MixtureOfExpertsExpert, string>(nameof(Name), string.Empty);

    /// <summary>A short sentence on what the expert tends to handle, shown while it is inspected.</summary>
    public static readonly StyledProperty<string?> HandlesProperty =
        AvaloniaProperty.Register<MixtureOfExpertsExpert, string?>(nameof(Handles));

    /// <summary>A 24×24 stroked glyph drawn at 14 px (Lucide style); null shows the first letter of the name.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<MixtureOfExpertsExpert, Geometry?>(nameof(Icon));

    /// <summary>Creates an empty expert.</summary>
    public MixtureOfExpertsExpert()
    {
    }

    /// <summary>Creates an expert with a name, an optional description and an optional icon.</summary>
    public MixtureOfExpertsExpert(string name, string? handles = null, Geometry? icon = null)
    {
        Name = name;
        Handles = handles;
        Icon = icon;
    }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="HandlesProperty"/>
    public string? Handles { get => GetValue(HandlesProperty); set => SetValue(HandlesProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
}

/// <summary>
/// The parameter counts behind the active-parameters readout, in billions: every expert-free part of the layer stack
/// (<see cref="Shared"/>) runs for every token, plus <see cref="PerExpert"/> for each expert a token uses. Parses from
/// "480, 6, 12" in XAML.
/// </summary>
/// <param name="Total">All parameters of the model, in billions.</param>
/// <param name="Shared">Parameters every token uses (attention, embeddings, shared layers), in billions.</param>
/// <param name="PerExpert">Parameters of one expert, in billions.</param>
public readonly record struct MixtureOfExpertsParameters(double Total, double Shared, double PerExpert)
{
    /// <summary>The invented default: 480B in all, 6B shared and 12B per expert (18B active at top 1, 30B at top 2).</summary>
    public static MixtureOfExpertsParameters Default => new(480, 6, 12);

    /// <summary>The parameters that run for one token when it uses <paramref name="k"/> experts.</summary>
    public double Active(int k) => Shared + Math.Max(0, k) * PerExpert;

    /// <summary>Parses "total, shared, perExpert" (commas or spaces, invariant culture).</summary>
    public static MixtureOfExpertsParameters Parse(string text)
    {
        var parts = text.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
        {
            throw new FormatException("Expected three numbers: total, shared, per expert.");
        }
        return new MixtureOfExpertsParameters(
            double.Parse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture),
            double.Parse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture),
            double.Parse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// How one token was routed: its normalized gate scores, the experts it chose (best first) with their weights within the
/// choice, the chosen experts that were full and skipped it, and the expert loads once it has been routed. The whole
/// sequence is planned at once, so a route can be read before its token plays.
/// </summary>
public sealed class MixtureOfExpertsRoute
{
    private readonly int[] _experts;
    private readonly int[] _dropped;

    internal MixtureOfExpertsRoute(int index, string token, double[] scores, int[] experts, double[] weights, int[] dropped,
        int[] loads, int routes, double? loadBalance)
    {
        Index = index;
        Token = token;
        Scores = scores;
        _experts = experts;
        Weights = weights;
        _dropped = dropped;
        Loads = loads;
        Routes = routes;
        LoadBalance = loadBalance;
    }

    /// <summary>The token's position in the sequence.</summary>
    public int Index { get; }

    /// <summary>The token's text.</summary>
    public string Token { get; }

    /// <summary>The gate's score for every expert, normalized to sum to 1.</summary>
    public IReadOnlyList<double> Scores { get; }

    /// <summary>The experts the gate chose (the top k), best first; the ones in <see cref="Dropped"/> did not process the token.</summary>
    public IReadOnlyList<int> Experts => _experts;

    /// <summary>
    /// The share of each chosen expert, in the order of <see cref="Experts"/>: its score divided by the sum of the chosen
    /// scores. A skipped expert keeps its weight; the others are not raised to fill the gap.
    /// </summary>
    public IReadOnlyList<double> Weights { get; }

    /// <summary>The chosen experts that were already at capacity and skipped the token.</summary>
    public IReadOnlyList<int> Dropped => _dropped;

    /// <summary>True when every chosen expert was full, so the token passed through the layer unchanged.</summary>
    public bool IsPassThrough => _dropped.Length == _experts.Length && _experts.Length > 0;

    /// <summary>The number of tokens each expert has processed once this token is routed.</summary>
    public IReadOnlyList<int> Loads { get; }

    /// <summary>The number of expert assignments made up to and including this token.</summary>
    public int Routes { get; }

    /// <summary>
    /// The load balance once this token is routed: (E − max/mean) / (E − 1) over the loads, 1 when every expert has
    /// the same load and 0 when one expert took everything; null while nothing has been routed.
    /// </summary>
    public double? LoadBalance { get; }

    /// <summary>True when <paramref name="expert"/> was chosen but skipped the token.</summary>
    public bool IsDropped(int expert) => Array.IndexOf(_dropped, expert) >= 0;

    /// <summary>The rank of <paramref name="expert"/> in the choice (0 = first choice), or −1 when it was not chosen.</summary>
    public int RankOf(int expert) => Array.IndexOf(_experts, expert);
}

/// <summary>Arguments of <see cref="MixtureOfExperts.Routed"/>: a token leaves the router for its experts.</summary>
public sealed class MixtureOfExpertsRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="route"/>.</summary>
    public MixtureOfExpertsRoutedEventArgs(RoutedEvent routedEvent, MixtureOfExpertsRoute route) : base(routedEvent)
    {
        TokenRoute = route;
    }

    /// <summary>The whole route, with the loads after it.</summary>
    public MixtureOfExpertsRoute TokenRoute { get; }

    /// <summary>The token's position in the sequence.</summary>
    public int Index => TokenRoute.Index;

    /// <summary>The token's text.</summary>
    public string Token => TokenRoute.Token;

    /// <summary>The gate's normalized score for every expert.</summary>
    public IReadOnlyList<double> Scores => TokenRoute.Scores;

    /// <summary>The chosen experts, best first.</summary>
    public IReadOnlyList<int> Experts => TokenRoute.Experts;

    /// <summary>The weights of the chosen experts within the choice, in the order of <see cref="Experts"/>.</summary>
    public IReadOnlyList<double> Weights => TokenRoute.Weights;

    /// <summary>The chosen experts that were full and skipped the token.</summary>
    public IReadOnlyList<int> Dropped => TokenRoute.Dropped;
}
