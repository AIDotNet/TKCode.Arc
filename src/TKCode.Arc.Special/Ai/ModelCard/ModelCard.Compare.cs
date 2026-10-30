using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

// The compare view of ModelCard: a rival menu, a side by side table and a tally of who leads.
public partial class ModelCard
{
    /// <summary>One row of the compare table: what it compares, how, and for benchmark rows which benchmark.</summary>
    private sealed record CompareRowSpec(string Key, ModelCardCompareKind Kind, ModelCardBenchmark? Benchmark);

    /// <summary>
    /// The rows: limits, cutoff, prices (cached only when any model in the pool has one), the cost at the chosen volume and
    /// one row per benchmark, scored or not, so rows never appear or disappear when the model or rival changes.
    /// </summary>
    private List<CompareRowSpec> CompareSpecs()
    {
        var specs = new List<CompareRowSpec>
        {
            new("context", ModelCardCompareKind.Count, null),
            new("output", ModelCardCompareKind.Count, null),
            new("cutoff", ModelCardCompareKind.Month, null),
            new("input", ModelCardCompareKind.Price, null),
            new("outputPrice", ModelCardCompareKind.Price, null),
        };
        if (PoolHasCached())
        {
            specs.Add(new("cached", ModelCardCompareKind.Price, null));
        }
        specs.Add(new("cost", ModelCardCompareKind.Price, null));
        foreach (var benchmark in AllBenchmarks)
        {
            specs.Add(new("b:" + benchmark.Id, ModelCardCompareKind.Score, benchmark));
        }
        return specs;
    }

    /// <summary>The raw number a row compares (null when the model has no value).</summary>
    private double? RawOf(CompareRowSpec spec, ModelCardModel model) => spec.Key switch
    {
        "context" => model.ContextWindow,
        "output" => model.MaxOutput,
        "cutoff" => ModelCardFormat.MonthNumber(model.KnowledgeCutoff),
        "input" => model.Pricing?.Input,
        "outputPrice" => model.Pricing?.Output,
        "cached" => model.Pricing?.CachedInput,
        "cost" => ModelCardFormat.MonthlyCost(model, MonthlyTokens, InputShare),
        _ => spec.Benchmark is { } b ? model.ScoreOf(b.Id) : null
    };

    /// <summary>The number a row shows.</summary>
    private ModelCardNumberValue? NumberOf(CompareRowSpec spec, ModelCardModel model)
    {
        if (RawOf(spec, model) is not { } raw)
        {
            return null;
        }
        return spec.Key switch
        {
            "context" or "output" => ModelCardFormat.Tokens((long)raw),
            "input" or "outputPrice" or "cached" => ModelCardFormat.Price(raw, Currency),
            "cost" => ModelCardFormat.Cost(raw, Currency),
            _ => spec.Benchmark is { } b ? ModelCardFormat.Score(raw, b) : null
        };
    }

    private string LabelOf(CompareRowSpec spec, ModelCardLabels labels) => spec.Key switch
    {
        "context" => labels.ContextWindow,
        "output" => labels.MaxOutput,
        "cutoff" => labels.KnowledgeCutoff,
        "input" => labels.InputPrice,
        "outputPrice" => labels.OutputPrice,
        "cached" => labels.CachedPrice,
        "cost" => labels.CostAt(ModelCardFormat.TokensText(MonthlyTokens)),
        _ => spec.Benchmark?.Name ?? string.Empty
    };

    private void RefreshCompare(ModelCardModel model, ModelCardLabels labels, bool anim)
    {
        var rivals = ShownRivals;
        var rival = CurrentRival;
        RefreshRivalMenu(rivals, rival);
        if (rival is null)
        {
            return;
        }
        _rivalButtonText!.Show(rival.Name, anim);
        AutomationProperties.SetName(_rivalButton!, labels.CompareWith + " " + rival.Name);
        _headModel!.Show(model.Name, anim);
        _headRival!.Show(rival.Name, anim);

        var specs = CompareSpecs();
        var key = string.Join("\u0001", specs.Select(s => s.Key));
        if (key != _compareRowsKey)
        {
            _compareRowsKey = key;
            _compareRows.Clear();
            _compareRowsPanel!.Children.Clear();
            foreach (var spec in specs)
            {
                var row = new ModelCardCompareRow(spec.Kind, spec.Benchmark is not null);
                if (spec.Kind == ModelCardCompareKind.Month)
                {
                    row.Cell(0).UseText();
                    row.Cell(1).UseText();
                }
                row.Cell(0).Bar.Reveal(TimeSpan.Zero, animate: false);
                row.Cell(1).Bar.Reveal(TimeSpan.Zero, animate: false);
                row.SetStacked(!IsCompareWide && _breakpoints is not null);
                _compareRows.Add(row);
                _compareRowsPanel.Children.Add(row);
            }
            anim = false;
        }

        var wins = new int[3];
        for (var i = 0; i < specs.Count; i++)
        {
            var spec = specs[i];
            var row = _compareRows[i];
            var label = LabelOf(spec, labels);
            row.SetLabel(label);
            var verdict = ModelCardFormat.Compare(RawOf(spec, model), RawOf(spec, rival), spec.Kind);
            wins[verdict.Winner]++;
            var pair = new[] { model, rival };
            for (var c = 0; c < 2; c++)
            {
                var cell = row.Cell(c);
                string spoken;
                if (spec.Kind == ModelCardCompareKind.Month)
                {
                    var month = ModelCardFormat.Month(pair[c].KnowledgeCutoff, labels.NotListed);
                    cell.Text.Show(month, anim);
                    spoken = month;
                }
                else
                {
                    cell.Number.Show(NumberOf(spec, pair[c]), labels.NotListed, anim);
                    spoken = cell.Number.Text;
                }
                if (spec.Benchmark is { } b)
                {
                    var raw = RawOf(spec, pair[c]);
                    cell.Bar.SetFraction(raw is { } v && b.Max > 0 ? v / b.Max : 0, anim);
                }
                var better = verdict.Winner == c + 1;
                cell.SetVerdict(better, better ? verdict.Margin : string.Empty, pair[c].Name + ": " + spoken, labels.Better);
            }
            AutomationProperties.SetName(row, label);
        }
        var rows = specs.Count;
        var tally = wins[1] > wins[2] ? labels.Tally(model.Name, wins[1], rows)
            : wins[2] > wins[1] ? labels.Tally(rival.Name, wins[2], rows)
            : labels.Tied(wins[1], rows);
        _tally!.Show(tally, anim);
        _footnote!.Text = Note ?? (IsSample ? labels.SampleFootnote : string.Empty);
    }

    private void RefreshRivalMenu(IReadOnlyList<ModelCardModel> rivals, ModelCardModel? current)
    {
        if (_rivalMenu is null)
        {
            return;
        }
        var key = string.Join("\u0001", rivals.Select(r => r.Id + "\u0002" + r.Name));
        if (key != _rivalMenuKey)
        {
            _rivalMenuKey = key;
            _rivalMenu.Items.Clear();
            foreach (var rival in rivals)
            {
                _rivalMenu.Items.Add(new MenuEntry
                {
                    Header = rival.Name,
                    Tag = rival.Id,
                    ToggleType = MenuEntryToggleType.Radio,
                    GroupName = "model-card-rival"
                });
            }
        }
        foreach (var item in _rivalMenu.Items)
        {
            if (item is MenuEntry entry)
            {
                entry.IsChecked = current is not null && Equals(entry.Tag, current.Id);
            }
        }
    }

    /// <summary>
    /// From a 520px card the table has a label column beside the two model columns and shows margins and mini bars;
    /// narrower, each label sits above its two values.
    /// </summary>
    private void LayoutCompare(bool wide)
    {
        var header = _compareHeader!;
        header.ColumnDefinitions.Clear();
        if (wide)
        {
            header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var spacer = header.Children.OfType<Border>().FirstOrDefault();
            if (spacer is null)
            {
                spacer = new Border();
                header.Children.Add(spacer);
            }
            spacer.Tok(WidthProperty, ModelCardTokenKind.CompareLabelWidth);
            spacer.IsVisible = true;
            Grid.SetColumn(_headModel!, 1);
            Grid.SetColumn(_headRival!, 2);
        }
        else
        {
            header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            foreach (var spacer in header.Children.OfType<Border>())
            {
                spacer.IsVisible = false;
            }
            Grid.SetColumn(_headModel!, 0);
            Grid.SetColumn(_headRival!, 1);
        }
        foreach (var row in _compareRows)
        {
            row.SetStacked(!wide);
        }
    }
}
