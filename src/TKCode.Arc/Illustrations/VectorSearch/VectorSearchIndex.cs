namespace TKCode.Arc;

/// <summary>A document in a <see cref="VectorSearch"/> index.</summary>
/// <param name="Title">What the results list shows.</param>
/// <param name="Topic">The cluster it belongs to (labels the plot).</param>
/// <param name="Keywords">Extra words that count toward its embedding but are not shown.</param>
public sealed record VectorSearchDocument(string Title, string Topic, string? Keywords = null);

/// <summary>One result of a <see cref="VectorSearch"/> query.</summary>
/// <param name="Document">The matching document.</param>
/// <param name="Score">Cosine similarity with the query (−1 to 1).</param>
/// <param name="Rank">1 for the best match.</param>
public sealed record VectorSearchResult(VectorSearchDocument Document, double Score, int Rank);

/// <summary>
/// The small, real search behind <see cref="VectorSearch"/>: words are lowercased, stop words dropped and plurals
/// trimmed, then each word and its character trigrams are hashed into a signed 256-dimension vector that is
/// L2-normalized (trigrams let related words such as "bill" and "billing" meet part way). Documents are ranked by cosine
/// similarity. The map places documents so their distances follow their cosine distances (stress minimization seeded
/// with the first two principal components), and a query lands among its nearest documents.
/// </summary>
internal sealed class VectorSearchIndex
{
    public const int Dimensions = 256;

    /// <summary>Weight of each character trigram relative to its whole word.</summary>
    private const double TrigramWeight = 0.3;

    private static readonly HashSet<string> s_stopWords = new(StringComparer.Ordinal)
    {
        "a", "an", "the", "for", "my", "to", "of", "and", "in", "on", "how", "do", "i", "is", "with", "can", "what", "where",
        "why", "was", "your", "you", "or", "at", "by", "from", "it", "me", "be", "are", "this", "that", "our", "we"
    };

    private readonly double[] _mean = new double[Dimensions];
    private readonly double[] _axisX = new double[Dimensions];
    private readonly double[] _axisY = new double[Dimensions];

    public VectorSearchIndex(IReadOnlyList<VectorSearchDocument> documents)
    {
        Documents = documents;
        Vectors = documents.Select(d => Embed($"{d.Title} {d.Topic} {d.Keywords}")).ToArray();
        ComputeAxes();
        Positions = Layout(Vectors.Select(Project).ToArray());
    }

    public IReadOnlyList<VectorSearchDocument> Documents { get; }

    public double[][] Vectors { get; }

    /// <summary>Each document on the two principal components (unscaled).</summary>
    public (double X, double Y)[] Positions { get; }

    /// <summary>Lowercase words with stop words dropped and plural / -ing / -ed endings trimmed.</summary>
    public static IEnumerable<string> Tokens(string text)
    {
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var letter = i < text.Length && char.IsLetterOrDigit(text[i]);
            if (letter && start < 0)
            {
                start = i;
            }
            else if (!letter && start >= 0)
            {
                var word = text[start..i].ToLowerInvariant();
                start = -1;
                if (s_stopWords.Contains(word))
                {
                    continue;
                }
                yield return Stem(word);
            }
        }
    }

    private static string Stem(string word)
    {
        if (word.Length > 5 && word.EndsWith("ing", StringComparison.Ordinal))
        {
            return word[..^3];
        }
        if (word.Length > 4 && word.EndsWith("ed", StringComparison.Ordinal))
        {
            return word[..^2];
        }
        if (word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal))
        {
            return word[..^1];
        }
        return word;
    }

    /// <summary>The normalized embedding of <paramref name="text"/> (zero vector when it has no words).</summary>
    public static double[] Embed(string text)
    {
        var vector = new double[Dimensions];
        foreach (var token in Tokens(text))
        {
            Add(vector, Fnv(token), 1);
            Add(vector, Fnv(token + "\u0001"), 1);
            var padded = $"#{token}#";
            for (var i = 0; i + 3 <= padded.Length; i++)
            {
                Add(vector, Fnv(padded.Substring(i, 3)), TrigramWeight);
            }
        }
        var length = Math.Sqrt(vector.Sum(v => v * v));
        if (length > 0)
        {
            for (var i = 0; i < Dimensions; i++)
            {
                vector[i] /= length;
            }
        }
        return vector;
    }

    private static void Add(double[] vector, uint hash, double weight) =>
        vector[hash % Dimensions] += (hash >> 9 & 1) == 0 ? weight : -weight;

    private static uint Fnv(string text)
    {
        var hash = 2166136261u;
        foreach (var c in text)
        {
            hash ^= c;
            hash *= 16777619u;
        }
        return hash;
    }

    public static double Cosine(double[] a, double[] b)
    {
        var dot = 0.0;
        for (var i = 0; i < Dimensions; i++)
        {
            dot += a[i] * b[i];
        }
        return dot;
    }

    /// <summary>The <paramref name="k"/> most similar documents, best first.</summary>
    public IReadOnlyList<VectorSearchResult> Search(double[] query, int k)
    {
        return Vectors.Select((v, i) => (Index: i, Score: Cosine(query, v)))
                      .OrderByDescending(x => x.Score)
                      .ThenBy(x => x.Index)
                      .Take(Math.Clamp(k, 1, Documents.Count))
                      .Select((x, rank) => new VectorSearchResult(Documents[x.Index], x.Score, rank + 1))
                      .ToArray();
    }

    /// <summary>
    /// Moves the documents (seeded at their principal components) until their distances on the map follow their
    /// cosine distances (stress minimization, a fixed number of steps, deterministic).
    /// </summary>
    private (double X, double Y)[] Layout((double X, double Y)[] seed)
    {
        var n = seed.Length;
        if (n < 3)
        {
            return seed;
        }
        var x = new double[n];
        var y = new double[n];
        var spread = Math.Max(1e-6, seed.Max(p => Math.Max(Math.Abs(p.X), Math.Abs(p.Y))));
        for (var i = 0; i < n; i++)
        {
            x[i] = seed[i].X / spread;
            y[i] = seed[i].Y / spread;
        }
        var target = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                target[i, j] = target[j, i] = 1 - Cosine(Vectors[i], Vectors[j]);
            }
        }
        for (var iteration = 0; iteration < 300; iteration++)
        {
            var rate = 0.08 * (1 - iteration / 300.0) + 0.005;
            for (var i = 0; i < n; i++)
            {
                double dx = 0;
                double dy = 0;
                for (var j = 0; j < n; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }
                    var vx = x[i] - x[j];
                    var vy = y[i] - y[j];
                    var distance = Math.Sqrt(vx * vx + vy * vy) + 1e-6;
                    // Pull together or push apart toward the target distance; near pairs count more.
                    var weight = 1 / (target[i, j] * target[i, j] + 0.05);
                    var force = (target[i, j] - distance) / distance * weight;
                    dx += vx * force;
                    dy += vy * force;
                }
                x[i] += rate * dx / n;
                y[i] += rate * dy / n;
            }
        }
        var result = new (double X, double Y)[n];
        for (var i = 0; i < n; i++)
        {
            result[i] = (x[i], y[i]);
        }
        return result;
    }

    /// <summary>Where a query lands: among its nearest documents, weighted by similarity.</summary>
    public (double X, double Y) Place(IReadOnlyList<VectorSearchResult> results)
    {
        double x = 0;
        double y = 0;
        double total = 0;
        foreach (var result in results)
        {
            var index = IndexOf(result.Document);
            var weight = Math.Max(0.05, result.Score) * Math.Max(0.05, result.Score);
            x += Positions[index].X * weight;
            y += Positions[index].Y * weight;
            total += weight;
        }
        return total <= 0 ? (0, 0) : (x / total, y / total);
    }

    public int IndexOf(VectorSearchDocument document)
    {
        for (var i = 0; i < Documents.Count; i++)
        {
            if (ReferenceEquals(Documents[i], document))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>A vector on the two principal components.</summary>
    public (double X, double Y) Project(double[] vector)
    {
        double x = 0;
        double y = 0;
        for (var i = 0; i < Dimensions; i++)
        {
            var centered = vector[i] - _mean[i];
            x += centered * _axisX[i];
            y += centered * _axisY[i];
        }
        return (x, y);
    }

    private void ComputeAxes()
    {
        var n = Vectors.Length;
        if (n == 0)
        {
            return;
        }
        foreach (var v in Vectors)
        {
            for (var i = 0; i < Dimensions; i++)
            {
                _mean[i] += v[i] / n;
            }
        }
        PowerIterate(_axisX, null, seed: 1);
        PowerIterate(_axisY, _axisX, seed: 2);
    }

    /// <summary>The leading eigenvector of the covariance (orthogonal to <paramref name="exclude"/>), without forming the matrix.</summary>
    private void PowerIterate(double[] axis, double[]? exclude, int seed)
    {
        for (var i = 0; i < Dimensions; i++)
        {
            axis[i] = Math.Sin(seed * 12.9898 + i * 78.233);
        }
        var scores = new double[Vectors.Length];
        for (var iteration = 0; iteration < 60; iteration++)
        {
            for (var r = 0; r < Vectors.Length; r++)
            {
                var s = 0.0;
                for (var i = 0; i < Dimensions; i++)
                {
                    s += (Vectors[r][i] - _mean[i]) * axis[i];
                }
                scores[r] = s;
            }
            Array.Clear(axis);
            for (var r = 0; r < Vectors.Length; r++)
            {
                for (var i = 0; i < Dimensions; i++)
                {
                    axis[i] += (Vectors[r][i] - _mean[i]) * scores[r];
                }
            }
            if (exclude is not null)
            {
                var along = 0.0;
                for (var i = 0; i < Dimensions; i++)
                {
                    along += axis[i] * exclude[i];
                }
                for (var i = 0; i < Dimensions; i++)
                {
                    axis[i] -= along * exclude[i];
                }
            }
            var length = Math.Sqrt(axis.Sum(v => v * v));
            if (length < 1e-12)
            {
                return;
            }
            for (var i = 0; i < Dimensions; i++)
            {
                axis[i] /= length;
            }
        }
    }
}
