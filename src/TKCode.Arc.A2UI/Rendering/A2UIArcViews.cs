using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.A2UI;

/// <summary>The Arc catalog's components (<see cref="A2UICatalogs.Arc"/>), each an Arc control fed from the node's values.</summary>
internal static class A2UIArcViews
{
    /// <summary>Most points a chart draws (more would only cost time; agents rarely need more).</summary>
    internal const int MaxChartPoints = 5000;

    public static void RegisterAll(A2UIComponentRegistry registry)
    {
        var id = A2UICatalogs.ArcCatalogId;
        registry.Register(id, "BarChart", static () => new BarChartView())
                .Register(id, "LineChart", static () => new LineChartView())
                .Register(id, "DonutChart", static () => new DonutChartView())
                .Register(id, "Sparkline", static () => new SparklineView())
                .Register(id, "Metric", static () => new MetricView())
                .Register(id, "Table", static () => new TableView())
                .Register(id, "Timeline", static () => new TimelineView())
                .Register(id, "Stepper", static () => new StepperView())
                .Register(id, "Progress", static () => new ProgressView())
                .Register(id, "Alert", static () => new AlertView())
                .Register(id, "Badge", static () => new BadgeView())
                .Register(id, "Avatar", static () => new AvatarView())
                .Register(id, "Markdown", static () => new MarkdownView())
                .Register(id, "CodeBlock", static () => new CodeBlockView())
                .Register(id, "Accordion", static () => new AccordionView())
                .Register(id, "Switch", static () => new SwitchView())
                .Register(id, "SegmentedControl", static () => new SegmentedView())
                .Register(id, "Rating", static () => new RatingView());
    }

    private static List<double> Numbers(JsonNode? node)
        => (node as JsonArray ?? new JsonArray()).Take(MaxChartPoints).Select(v => A2UIJson.TryGetDouble(v, out var d) ? d : 0).ToList();

    // The plot height: the agent's "height" (120-600), else the given default.
    private static double PlotHeight(A2UINode node, double fallback) => node.GetNumber("height") is { } h ? Math.Clamp(h, 120, 600) : fallback;

    private sealed class BarChartView : A2UIComponentView<BarChart>
    {
        protected override BarChart Create() => new();

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            var chart = View;
            chart.PlotHeight = PlotHeight(Node, 180);
            chart.Label = Node.GetString("title") ?? string.Empty;
            chart.Unit = Node.GetString("unit") ?? string.Empty;
            var labels = Node.GetStringList("labels") ?? [];
            var values = Numbers(Node.Get("values"));
            var points = new List<ChartPoint>();
            for (var i = 0; i < Math.Min(Math.Min(labels.Count, values.Count), 48); i++)
            {
                points.Add(new ChartPoint(labels[i], values[i]));
            }

            chart.ItemsSource = points;
        }
    }

    private sealed class LineChartView : A2UIComponentView<LineChart>
    {
        private string _shape = string.Empty;

        protected override LineChart Create() => new();

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            var chart = View;
            chart.PlotHeight = PlotHeight(Node, 200);
            chart.Label = Node.GetString("title") ?? string.Empty;
            chart.Unit = Node.GetString("unit") ?? string.Empty;
            var labels = Node.GetStringList("labels") ?? [];
            var series = (Node.Get("series") as JsonArray ?? new JsonArray()).OfType<JsonObject>().ToList();
            var shape = string.Join("|", labels) + "#" + string.Join("|", series.Select(s => A2UIJson.ToDisplayString(s["name"])));
            if (shape != _shape)
            {
                _shape = shape;
                chart.ItemsSource = labels.Take(MaxChartPoints).ToList();
                chart.Series.Clear();
                foreach (var s in series)
                {
                    chart.Series.Add(new ChartSeries { Name = A2UIJson.ToDisplayString(s["name"]), Values = Numbers(s["values"]) });
                }

                return;
            }

            // Same labels and series: only the values move (the chart animates instead of rebuilding).
            for (var i = 0; i < series.Count && i < chart.Series.Count; i++)
            {
                chart.Series[i].Values = Numbers(series[i]["values"]);
            }
        }
    }

    private sealed class DonutChartView : A2UIComponentView<DonutChart>
    {
        protected override DonutChart Create() => new() { Diameter = 180 };

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            View.Label = Node.GetString("title") ?? string.Empty;
            if (Node.GetString("totalLabel") is { } total)
            {
                View.TotalLabel = total;
            }

            var labels = Node.GetStringList("labels") ?? [];
            var values = Numbers(Node.Get("values"));
            View.ItemsSource = labels.Zip(values, (l, v) => new ChartPoint(l, v)).ToList();
        }
    }

    private sealed class SparklineView : A2UIComponentView<Sparkline>
    {
        protected override Sparkline Create() => new();

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            View.Label = Node.GetString("label") ?? string.Empty;
            View.Value = Node.GetString("value");
            View.Change = Node.GetString("change");
            View.ItemsSource = Numbers(Node.Get("values")).Select((v, i) => new ChartPoint(i, v)).ToList();
        }
    }

    private sealed class MetricView : A2UIComponentView<MetricCard>
    {
        protected override MetricCard Create() => new();

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            View.Label = Node.GetString("label");
            View.Value = Node.GetNumber("value") ?? 0;
            View.Prefix = Node.GetString("prefix");
            View.Suffix = Node.GetString("suffix");
            View.Decimals = Math.Clamp((int)(Node.GetNumber("decimals") ?? 0), 0, 4);
            View.Change = Node.GetString("change");
            View.Context = Node.GetString("context");
        }
    }

    private sealed class TableView : A2UIComponentView<Table>
    {
        private string _columns = string.Empty;

        protected override Table Create() => new() { IsStriped = false };

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            View.Caption = Node.GetString("caption") ?? string.Empty;
            var columns = (Node.Get("columns") as JsonArray ?? new JsonArray()).OfType<JsonObject>().ToList();
            var signature = string.Join("|", columns.Select(c => c.ToJsonString()));
            if (signature != _columns)
            {
                _columns = signature;
                View.Columns.Clear();
                foreach (var column in columns)
                {
                    var key = A2UIJson.AsString(column["key"]) ?? string.Empty;
                    var format = A2UIJson.AsString(column["format"]);
                    var numeric = A2UIJson.AsBoolean(column["numeric"]) == true || format is not null;
                    View.Columns.Add(new DataTableColumn
                    {
                        Header = A2UIJson.AsString(column["title"]) ?? key,
                        Key = key,
                        IsNumeric = numeric,
                        ValueSelector = row => Cell(row, key, format),
                        SortValueSelector = row => row is JsonObject o && A2UIJson.TryGetDouble(o[key], out var d) ? d : A2UIJson.ToDisplayString((row as JsonObject)?[key])
                    });
                }
            }

            View.ItemsSource = (Node.Get("rows") as JsonArray ?? new JsonArray()).OfType<JsonObject>().Take(MaxChartPoints)
                .Select(r => (object)r.DeepClone().AsObject()).ToList();
        }

        private static object? Cell(object row, string key, string? format)
        {
            var value = (row as JsonObject)?[key];
            if (format is not null && A2UIJson.TryGetDouble(value, out var d))
            {
                if (format.StartsWith("currency:", StringComparison.Ordinal))
                {
                    return A2UICurrency.Format(d, format[9..], 0, grouping: true, CultureInfo.CurrentCulture);
                }

                return format switch
                {
                    "percent" => d.ToString("0.#", CultureInfo.CurrentCulture) + "%",
                    _ => d.ToString("N0", CultureInfo.CurrentCulture)
                };
            }

            return A2UIJson.ToDisplayString(value);
        }
    }

    private sealed class TimelineView : A2UIComponentView<Timeline>
    {
        protected override Timeline Create() => new() { MaxHeight = 420 };

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            if (Node.GetString("label") is { } label)
            {
                View.Label = label;
            }

            var events = new List<TimelineEvent>();
            foreach (var e in (Node.Get("events") as JsonArray ?? new JsonArray()).OfType<JsonObject>())
            {
                if (!DateTimeOffset.TryParse(A2UIJson.AsString(e["at"]), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at))
                {
                    continue;
                }

                events.Add(new TimelineEvent(A2UIJson.AsString(e["id"]) ?? at.ToString("o", CultureInfo.InvariantCulture), at,
                                             A2UIJson.ToDisplayString(e["title"]), A2UIJson.AsString(e["actor"]))
                {
                    Meta = A2UIJson.AsString(e["meta"])
                });
            }

            // Relative times ("4m") count from the newest event, so a replayed or saved answer reads the same later.
            if (events.Count > 0)
            {
                View.Now = events.Max(e => e.At).AddMinutes(1);
            }

            View.ItemsSource = events.OrderByDescending(e => e.At).ToList();
        }
    }

    private sealed class StepperView : A2UIComponentView<Stepper>
    {
        private string _steps = string.Empty;

        protected override Stepper Create() => new();

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            var steps = (Node.Get("steps") as JsonArray ?? new JsonArray()).OfType<JsonObject>().ToList();
            var signature = string.Join("|", steps.Select(s => s.ToJsonString()));
            if (signature != _steps)
            {
                _steps = signature;
                View.ItemsSource = steps.Select(s => new StepperStep
                {
                    Label = A2UIJson.ToDisplayString(s["label"]),
                    Description = A2UIJson.AsString(s["description"])
                }).ToList();
            }

            View.Orientation = Node.GetString("orientation") == "vertical" ? Orientation.Vertical : Orientation.Horizontal;
            View.CurrentIndex = Math.Clamp((int)(Node.GetNumber("current") ?? 0), 0, Math.Max(0, steps.Count));
        }
    }

    private sealed class ProgressView : A2UIComponentView<Progress>
    {
        protected override Progress Create() => new() { IsValueVisible = true };

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            View.Label = Node.GetString("label");
            View.Maximum = Node.GetNumber("max") is > 0 and var max ? max : 100;
            View.Value = Math.Clamp(Node.GetNumber("value") ?? 0, 0, View.Maximum);
        }
    }

    private sealed class AlertView : A2UIComponentView<Alert>
    {
        protected override Alert Create() => new();

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            View.Title = Node.GetString("title");
            View.Content = Node.GetString("text");
            View.Tone = Node.GetString("tone") switch
            {
                "success" => AlertTone.Success,
                "warning" => AlertTone.Warning,
                "danger" => AlertTone.Danger,
                _ => AlertTone.Info
            };
        }
    }

    private sealed class BadgeView : A2UIComponentView<Badge>
    {
        protected override Badge Create() => new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            View.Content = Node.GetString("text");
            View.Tone = Node.GetString("tone") switch
            {
                "success" => BadgeTone.Success,
                "info" => BadgeTone.Info,
                "warning" => BadgeTone.Warning,
                "danger" => BadgeTone.Danger,
                _ => BadgeTone.Neutral
            };
        }
    }

    private sealed class AvatarView : A2UIComponentView<Avatar>
    {
        protected override Avatar Create() => new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            View.DisplayName = Node.GetString("name");
            var options = Context.Renderer.Options;
            View.Source = options.UrlPolicy.ResolveMedia(Node.GetString("imageUrl"), options.BaseUri) is { } uri ? ImageSource.FromUri(uri) : null;
        }
    }

    private sealed class MarkdownView : A2UIComponentView<Markdown>
    {
        protected override Markdown Create()
        {
            // Links go through the renderer (UrlPolicy, UrlOpening, the app's opener), never straight to the launcher.
            var markdown = new Markdown { OpensLinks = false };
            markdown.LinkClicked += (_, e) =>
            {
                e.Handled = true;
                var options = Context.Renderer.Options;
                var resolved = Uri.TryCreate(e.Url, UriKind.Absolute, out var uri)
                               || options.BaseUri is { } baseUri && Uri.TryCreate(baseUri, e.Url, out uri);
                if (resolved && uri is not null && options.UrlPolicy.AllowsLink(uri))
                {
                    Context.Renderer.OpenUrl(uri, Context.Surface);
                }
            };
            return markdown;
        }

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            var text = Node.GetString("text") ?? string.Empty;
            if (!string.Equals(View.Text, text, StringComparison.Ordinal))
            {
                View.Text = text;
            }
        }
    }

    private sealed class CodeBlockView : A2UIComponentView<CodeBlock>
    {
        protected override CodeBlock Create() => new();

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            View.Code = Node.GetString("code") ?? string.Empty;
            View.Language = Node.GetString("language");
            View.FileName = Node.GetString("fileName");
        }
    }

    private sealed class AccordionView : A2UIComponentView<Accordion>
    {
        private bool _expandedSet;

        protected override Accordion Create() => new();

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            View.ExpandMode = Node.GetBoolean("multiple") == true ? AccordionExpandMode.Multiple : AccordionExpandMode.Single;
            var specs = Node.Get("items") as JsonArray ?? new JsonArray();
            var items = View.Items;
            while (items.Count > specs.Count)
            {
                items.RemoveAt(items.Count - 1);
            }

            for (var i = 0; i < specs.Count; i++)
            {
                if (items.Count <= i)
                {
                    items.Add(new AccordionItem());
                }

                var item = (AccordionItem)items[i]!;
                item.Header = A2UIJson.ToDisplayString((specs[i] as JsonObject)?["title"]);
                var child = Node.GetChild($"items/{i}/child") is { } node ? Context.BuildChild(node) : null;
                if (!ReferenceEquals(item.Content, child))
                {
                    if (child is not null)
                    {
                        A2UIBasicViews.Unparent(child);
                    }

                    item.Content = child;
                }
            }

            // The agent's initial choice only; afterwards the user opens and closes sections.
            if (!_expandedSet && Node.GetNumber("expanded") is { } expanded)
            {
                _expandedSet = true;
                View.ExpandedIndex = (int)expanded;
            }
        }
    }

    private sealed class SwitchView : A2UIComponentView<Switch>
    {
        protected override Switch Create()
        {
            var toggle = new Switch { HorizontalAlignment = HorizontalAlignment.Left };
            toggle.IsCheckedChanged += (_, _) =>
            {
                if (!IsApplying)
                {
                    Node.Write("value", JsonValue.Create(toggle.IsChecked == true));
                }
            };
            return toggle;
        }

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            View.Content = Node.GetString("label");
            var value = Node.GetBoolean("value") == true;
            if (View.IsChecked != value)
            {
                View.IsChecked = value;
            }

            ToolTip.SetTip(View, A2UIBasicViews.CheckMessage(Node));
        }
    }

    private sealed class SegmentedView : A2UIComponentView<StackPanel>
    {
        private readonly SegmentedControl _control = new() { HorizontalAlignment = HorizontalAlignment.Left };
        private readonly TextBlock _message = new() { IsVisible = false };
        private string _options = string.Empty;
        private List<string> _values = [];

        protected override StackPanel Create()
        {
            ArcTokens.Bind(_message, TextBlock.ForegroundProperty, TKCode.Arc.DesignTokens.ArcDesignTokenKind.Danger);
            ArcTokens.Bind(_message, TextBlock.FontSizeProperty, TKCode.Arc.DesignTokens.ArcDesignTokenKind.TypeXS);
            _control.SelectionChanged += (_, _) =>
            {
                if (!IsApplying && _control.SelectedIndex >= 0 && _control.SelectedIndex < _values.Count)
                {
                    Node.Write("value", JsonValue.Create(_values[_control.SelectedIndex]));
                }
            };
            return new StackPanel { Spacing = 4, Children = { _control, _message } };
        }

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            var options = (Node.Get("options") as JsonArray ?? new JsonArray()).OfType<JsonObject>()
                .Select(o => (Value: A2UIJson.ToDisplayString(o["value"]), Label: A2UIJson.ToDisplayString(o["label"]))).ToList();
            var signature = string.Join("\u001f", options.Select(o => o.Value + "\u001e" + o.Label));
            if (signature != _options)
            {
                _options = signature;
                _values = options.Select(o => o.Value).ToList();
                _control.Items.Clear();
                foreach (var (value, label) in options)
                {
                    _control.Items.Add(new SegmentedOption(value, label));
                }
            }

            var index = _values.IndexOf(Node.GetString("value") ?? string.Empty);
            if (_control.SelectedIndex != index)
            {
                _control.SelectedIndex = index;
            }

            _message.Text = A2UIBasicViews.CheckMessage(Node);
            _message.IsVisible = _message.Text is not null;
        }
    }

    private sealed class RatingView : A2UIComponentView<Rating>
    {
        protected override Rating Create()
        {
            var rating = new Rating();
            rating.ValueChanged += (_, e) =>
            {
                if (!IsApplying)
                {
                    Node.Write("value", JsonValue.Create(e.Value));
                }
            };
            return rating;
        }

        protected override void Apply(A2UINodeChangedEventArgs change)
        {
            View.Label = Node.GetString("label");
            View.Maximum = Math.Clamp((int)(Node.GetNumber("max") ?? 5), 1, 10);
            View.AllowHalf = Node.GetBoolean("allowHalf") == true;
            View.IsReadOnly = Node.GetBoolean("readOnly") == true;
            var value = Math.Clamp(Node.GetNumber("value") ?? 0, 0, View.Maximum);
            if (View.Value != value)
            {
                View.Value = value;
            }

            View.Description = A2UIBasicViews.CheckMessage(Node);
        }
    }
}
