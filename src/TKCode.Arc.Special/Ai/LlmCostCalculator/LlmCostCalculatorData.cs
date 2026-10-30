using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One model an <see cref="LlmCostCalculator"/> compares: its id, name and provider, an optional 16px mark, and its list
/// prices in US dollars per 1M tokens.
/// </summary>
public sealed class LlmModelPrice
{
    /// <summary>Creates an empty model; set its properties.</summary>
    public LlmModelPrice()
    {
    }

    /// <summary>Creates a model with its prices in US dollars per 1M tokens.</summary>
    public LlmModelPrice(string id, string name, string provider, double input, double output, double? cachedInput = null,
        Geometry? mark = null)
    {
        Id = id;
        Name = name;
        Provider = provider;
        Input = input;
        Output = output;
        CachedInput = cachedInput;
        Mark = mark;
    }

    /// <summary>A stable id, used by <see cref="LlmCostCalculator.SelectedId"/>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The model's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Who serves the model, shown muted after the name.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// The model's mark as a 24 × 24 stroked glyph drawn at 16px. Null shows the first letter of the provider in a tinted
    /// rounded square.
    /// </summary>
    public Geometry? Mark { get; set; }

    /// <summary>The mark's color. Null draws it in the row's secondary text color.</summary>
    public IBrush? MarkBrush { get; set; }

    /// <summary>US dollars per 1M input tokens.</summary>
    public double Input { get; set; }

    /// <summary>US dollars per 1M cached input tokens (cache reads). Null bills cached tokens at <see cref="Input"/>.</summary>
    public double? CachedInput { get; set; }

    /// <summary>US dollars per 1M output tokens.</summary>
    public double Output { get; set; }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>A workload: tokens in and out per request, requests a day and whether prompt caching is on.</summary>
/// <param name="InputTokens">Input (prompt) tokens per request.</param>
/// <param name="OutputTokens">Output tokens per request.</param>
/// <param name="RequestsPerDay">Requests a day.</param>
/// <param name="Cached">Whether part of the input bills at the cache read price.</param>
public readonly record struct LlmUsage(int InputTokens, int OutputTokens, int RequestsPerDay, bool Cached);

/// <summary>A workload shortcut of an <see cref="LlmCostCalculator"/>: an id, a short label and the workload it sets.</summary>
public sealed class LlmUsagePreset
{
    /// <summary>Creates an empty preset; set its properties.</summary>
    public LlmUsagePreset()
    {
    }

    /// <summary>Creates a preset.</summary>
    public LlmUsagePreset(string id, string label, LlmUsage usage)
    {
        Id = id;
        Label = label;
        Usage = usage;
    }

    /// <summary>A stable id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The segment's text; keep it to one short word.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The workload the preset sets.</summary>
    public LlmUsage Usage { get; set; }
}

/// <summary>What one model costs for a workload, in US dollars a month (<see cref="PerRequest"/> per call).</summary>
/// <param name="Input">Uncached input tokens.</param>
/// <param name="CachedInput">Input tokens billed at the cache read price.</param>
/// <param name="Output">Output tokens.</param>
/// <param name="Total">The month: input, cached input and output.</param>
/// <param name="PerRequest">The total divided by the month's requests.</param>
public readonly record struct LlmCost(double Input, double CachedInput, double Output, double Total, double PerRequest);

/// <summary>Arguments of <see cref="LlmCostCalculator.UsageChanged"/>.</summary>
public sealed class LlmUsageChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public LlmUsageChangedEventArgs(RoutedEvent routedEvent, LlmUsage oldUsage, LlmUsage usage) : base(routedEvent)
    {
        OldUsage = oldUsage;
        Usage = usage;
    }

    /// <summary>The workload before the change.</summary>
    public LlmUsage OldUsage { get; }

    /// <summary>The whole new workload.</summary>
    public LlmUsage Usage { get; }
}

/// <summary>Arguments of <see cref="LlmCostCalculator.SelectedModelChanged"/>.</summary>
public sealed class LlmSelectedModelChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public LlmSelectedModelChangedEventArgs(RoutedEvent routedEvent, LlmModelPrice model, LlmCost cost) : base(routedEvent)
    {
        Model = model;
        Cost = cost;
    }

    /// <summary>The id of the picked model.</summary>
    public string Id => Model.Id;

    /// <summary>The picked model.</summary>
    public LlmModelPrice Model { get; }

    /// <summary>What the picked model costs for the current workload.</summary>
    public LlmCost Cost { get; }
}
