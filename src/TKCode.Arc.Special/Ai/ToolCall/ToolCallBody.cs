using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The fold of a long output: shows the first <see cref="FoldedHeight"/> of its content (ten JSON lines, five rows) and
/// all of it once unfolded. Toggling springs the height on <see cref="ArcSpring.Smooth"/>; other content changes and
/// width changes jump. Code-driven (<see cref="MotionValue{T}"/>) because its target is the measured content height.
/// </summary>
internal sealed class ToolCallFoldBox : Control
{
    private readonly RectangleGeometry _clip = new();
    private MotionValue<double>? _height;
    private Control? _child;
    private bool _folded = true;
    private bool _toggled;
    private double _natural;

    static ToolCallFoldBox()
    {
        ClipToBoundsProperty.OverrideDefaultValue<ToolCallFoldBox>(false);
    }

    /// <summary>What folds.</summary>
    public Control? Child
    {
        get => _child;
        set
        {
            if (_child is not null)
            {
                VisualChildren.Remove(_child);
                LogicalChildren.Remove(_child);
            }
            _child = value;
            if (value is not null)
            {
                LogicalChildren.Add(value);
                VisualChildren.Add(value);
            }
            InvalidateMeasure();
        }
    }

    /// <summary>The height shown while folded.</summary>
    public double FoldedHeight { get; set; }

    /// <summary>Called when the fold settles (folded, all hidden content may hide) or starts to open (it must show).</summary>
    public Action<bool>? FoldChanged { get; set; }

    /// <summary>Whether only the first part shows.</summary>
    public bool IsFolded
    {
        get => _folded;
        set
        {
            if (_folded == value)
            {
                return;
            }
            _folded = value;
            _toggled = true;
            if (!value)
            {
                FoldChanged?.Invoke(false);
            }
            InvalidateMeasure();
        }
    }

    /// <summary>The height shown now.</summary>
    internal double DisplayedHeight => _height?.Value ?? 0;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_child is null)
        {
            return default;
        }
        _child.Measure(availableSize.WithHeight(double.PositiveInfinity));
        _natural = _child.DesiredSize.Height;
        var target = _folded ? Math.Min(_natural, FoldedHeight) : _natural;
        if (_height is null)
        {
            _height = new MotionValue<double>(this, target, MotionVectors.Double);
            _height.Changed += _ =>
            {
                InvalidateMeasure();
                InvalidateVisual();
            };
        }
        else if (Math.Abs(_height.Target - target) > 0.5)
        {
            if (_toggled && IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full)
            {
                var folding = _folded;
                _ = _height.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth)).ContinueWith(t =>
                {
                    if (t.Result && folding)
                    {
                        Dispatcher.Post(() =>
                        {
                            if (_folded)
                            {
                                FoldChanged?.Invoke(true);
                            }
                        });
                    }
                }, TaskScheduler.Default);
            }
            else
            {
                _height.Jump(target);
                if (_toggled && _folded)
                {
                    FoldChanged?.Invoke(true);
                }
            }
        }
        _toggled = false;
        return new Size(_child.DesiredSize.Width, Math.Max(0, _height.Value));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _child?.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(_natural, finalSize.Height)));
        var shown = Math.Max(0, _height?.Value ?? 0);
        if (shown >= _natural - 0.5)
        {
            Clip = null;
        }
        else
        {
            _clip.Rect = new Rect(-4, 0, finalSize.Width + 8, shown);
            Clip = _clip;
        }
        return finalSize;
    }
}

/// <summary>
/// Tinted JSON, drawn line by line in the mono font at 12/20: keys in Foreground, strings in Success mixed 80% into
/// Foreground, numbers in Warning mixed 82% into Foreground, true / false / null in TextMuted and punctuation in
/// TextSecondary. One cached <see cref="TextLayout"/> per line (rebuilt only when the text or the theme changes); the
/// colors follow the theme through bound brushes.
/// </summary>
internal sealed class ToolCallJsonView : Control
{
    internal const double LineHeight = 20;
    internal const double TextSize = 12;

    private static readonly StyledProperty<IBrush?> KeyBrushProperty =
        AvaloniaProperty.Register<ToolCallJsonView, IBrush?>("KeyBrush");

    private static readonly StyledProperty<IBrush?> PunctuationBrushProperty =
        AvaloniaProperty.Register<ToolCallJsonView, IBrush?>("PunctuationBrush");

    private static readonly StyledProperty<IBrush?> LiteralBrushProperty =
        AvaloniaProperty.Register<ToolCallJsonView, IBrush?>("LiteralBrush");

    private static readonly StyledProperty<IBrush?> SuccessBrushProperty =
        AvaloniaProperty.Register<ToolCallJsonView, IBrush?>("SuccessBrush");

    private static readonly StyledProperty<IBrush?> WarningBrushProperty =
        AvaloniaProperty.Register<ToolCallJsonView, IBrush?>("WarningBrush");

    private static readonly StyledProperty<FontFamily> MonoProperty =
        AvaloniaProperty.Register<ToolCallJsonView, FontFamily>("Mono", FontFamily.Default);

    private ToolCallJsonToken[][] _lines = [];
    private TextLayout?[] _layouts = [];
    private bool _stale = true;
    private double _width;

    static ToolCallJsonView()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<ToolCallJsonView>(false);
    }

    public ToolCallJsonView()
    {
        ArcTokens.Bind(this, KeyBrushProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(this, PunctuationBrushProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(this, LiteralBrushProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(this, SuccessBrushProperty, ArcDesignTokenKind.Success);
        ArcTokens.Bind(this, WarningBrushProperty, ArcDesignTokenKind.Warning);
        ArcTokens.Bind(this, MonoProperty, ArcDesignTokenKind.MonoFontFamily);
    }

    /// <summary>The number of lines.</summary>
    internal int LineCount => _lines.Length;

    /// <summary>The pieces of each line.</summary>
    internal IReadOnlyList<ToolCallJsonToken[]> Lines => _lines;

    /// <summary>Shows <paramref name="json"/> (already indented).</summary>
    internal void SetText(string json)
    {
        _lines = ToolCallJson.Tokenize(json);
        _stale = true;
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KeyBrushProperty || change.Property == PunctuationBrushProperty ||
            change.Property == LiteralBrushProperty || change.Property == SuccessBrushProperty ||
            change.Property == WarningBrushProperty || change.Property == MonoProperty)
        {
            _stale = true;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <summary>The tint of a piece kind for the current theme.</summary>
    internal IBrush BrushOf(ToolCallJsonTokenKind kind)
    {
        var key = Color(KeyBrushProperty);
        return kind switch
        {
            ToolCallJsonTokenKind.Key => new ImmutableSolidColorBrush(key),
            ToolCallJsonTokenKind.String => new ImmutableSolidColorBrush(Oklch.MixOklab(key, Color(SuccessBrushProperty), 0.8)),
            ToolCallJsonTokenKind.Number => new ImmutableSolidColorBrush(Oklch.MixOklab(key, Color(WarningBrushProperty), 0.82)),
            ToolCallJsonTokenKind.Literal => new ImmutableSolidColorBrush(Color(LiteralBrushProperty)),
            _ => new ImmutableSolidColorBrush(Color(PunctuationBrushProperty))
        };
    }

    private Color Color(StyledProperty<IBrush?> property) => (GetValue(property) as ISolidColorBrush)?.Color ?? Colors.Gray;

    private void Build()
    {
        if (!_stale)
        {
            return;
        }
        _stale = false;
        foreach (var layout in _layouts)
        {
            layout?.Dispose();
        }
        var typeface = new Typeface(GetValue(MonoProperty));
        var brushes = new IBrush[5];
        for (var k = 0; k < brushes.Length; k++)
        {
            brushes[k] = BrushOf((ToolCallJsonTokenKind)k);
        }
        _layouts = new TextLayout?[_lines.Length];
        _width = 0;
        for (var i = 0; i < _lines.Length; i++)
        {
            var tokens = _lines[i];
            var text = string.Concat(tokens.Select(t => t.Text));
            var spans = new ValueSpan<TextRunProperties>[tokens.Length];
            var start = 0;
            for (var t = 0; t < tokens.Length; t++)
            {
                spans[t] = new ValueSpan<TextRunProperties>(start, tokens[t].Text.Length,
                    new GenericTextRunProperties(typeface, TextSize, null, brushes[(int)tokens[t].Kind]));
                start += tokens[t].Text.Length;
            }
            var layout = new TextLayout(text, typeface, TextSize, brushes[0], TextAlignment.Left, TextWrapping.NoWrap,
                TextTrimming.None, null, FlowDirection.LeftToRight, double.PositiveInfinity, double.PositiveInfinity, LineHeight, 0, 0,
                null, spans);
            _layouts[i] = layout;
            _width = Math.Max(_width, layout.WidthIncludingTrailingWhitespace);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Build();
        return new Size(Math.Ceiling(_width), _lines.Length * LineHeight);
    }

    public override void Render(DrawingContext context)
    {
        Build();
        for (var i = 0; i < _layouts.Length; i++)
        {
            _layouts[i]?.Draw(context, new Point(0, i * LineHeight));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);
}

/// <summary>
/// Builds a call's Input / Output panel (mounted only while the row is open, as in Arc): 38px in so the sections line up
/// with the title (12px when compact), sections 10px apart, each a 28px heading line with a copy button and a SurfaceMuted
/// well (radius 10). Input is a mono key / value list; Output depends on the status: the result (list, table, JSON or
/// text, or the owner's template), the reason under "Error", or a quiet placeholder while waiting, running or denied.
/// </summary>
internal static class ToolCallBodyBuilder
{
    /// <summary>Rows of a list or table shown before the fold; lines of JSON.</summary>
    internal const int FoldRows = 5;

    internal const int FoldLines = 10;

    internal const double ListRowHeight = 32;
    internal const double TableRowHeight = 30;
    internal const double SectionGap = 10;
    internal const double SectionHeadHeight = 28;

    /// <summary>What a body needs from its row.</summary>
    internal sealed record Context(ToolCallItem Item, ToolCallLabels Labels, IDataTemplate? OutputTemplate, Action<string> Announce);

    public static Control Build(Context context)
    {
        var item = context.Item;
        var labels = context.Labels;
        var panel = new StackPanel { Spacing = SectionGap };
        if (item.Input.Count > 0)
        {
            var copy = Copy(labels.CopyInput, labels, context.Announce,
                () => ToolCallJson.FormatObject(item.Input.Select(a => new KeyValuePair<string, string>(a.Key, a.Value))));
            panel.Children.Add(Section(labels.Input, Well(KeyValues(item), new Thickness(12, 10)), copy));
        }
        panel.Children.Add(OutputSection(context));
        return panel;
    }

    private static Control OutputSection(Context context)
    {
        var item = context.Item;
        var labels = context.Labels;
        switch (item.Status)
        {
            case ToolCallStatus.Error:
            {
                var reason = string.IsNullOrEmpty(item.Message) ? labels.FailedStatus(null) : item.Message!;
                var text = Text(reason, 14, 19.6, ArcDesignTokenKind.Foreground);
                text.TextWrapping = TextWrapping.Wrap;
                return Section(labels.Error, Well(text, new Thickness(12, 10)), Copy(labels.CopyError, labels, context.Announce, () => reason));
            }
            case ToolCallStatus.Approval:
                return Section(labels.Output, Placeholder(labels.ApprovalPlaceholder), null);
            case ToolCallStatus.Denied:
                return Section(labels.Output, Placeholder(labels.DeniedPlaceholder), null);
            case ToolCallStatus.Running:
                return Section(labels.Output, Placeholder(labels.RunningPlaceholder), null);
        }
        if (context.OutputTemplate is { } template && template.Match(item) && template.Build(item) is { } custom)
        {
            return Section(labels.Output, Well(custom, new Thickness(12, 10)), null);
        }
        var output = item.Output;
        var copyOutput = output is null ? null : Copy(labels.CopyOutput, labels, context.Announce, output.ToClipboardText);
        Control well = output switch
        {
            ToolCallListOutput list => ListWell(list, labels),
            ToolCallTableOutput table => TableWell(table, labels),
            ToolCallJsonOutput json => JsonWell(json, labels),
            ToolCallTextOutput text => TextWell(text.Text),
            _ => Placeholder(labels.EmptyOutput)
        };
        return Section(labels.Output, well, copyOutput);
    }

    private static Control Section(string heading, Control well, ToolCallCopyButton? copy)
    {
        var head = new Grid { Height = SectionHeadHeight, ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var label = Text(heading, 12, 16.8, ArcDesignTokenKind.TextMuted);
        label.VerticalAlignment = VerticalAlignment.Center;
        // The heading is read through the section's name.
        AutomationProperties.SetAccessibilityView(label, AccessibilityView.Raw);
        head.Children.Add(label);
        if (copy is not null)
        {
            copy.Margin = new Thickness(0, 0, -6, 0);
            copy.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(copy, 1);
            head.Children.Add(copy);
        }
        var section = new StackPanel { Children = { head, well } };
        AutomationProperties.SetName(section, heading);
        AutomationProperties.SetControlTypeOverride(section, AutomationControlType.Group);
        return section;
    }

    private static Border Well(Control content, Thickness padding)
    {
        var well = new Border { Padding = padding, Child = content };
        well.BindCornerRadius(ArcDesignTokenKind.RadiusStep10);
        ArcTokens.Bind(well, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        return well;
    }

    private static Control Placeholder(string text) => Well(Text(text, 14, 19.6, ArcDesignTokenKind.TextMuted), new Thickness(12, 10));

    private static ToolCallCopyButton Copy(string name, ToolCallLabels labels, Action<string> announce, Func<string?> text) => new()
    {
        Label = name,
        CopiedLabel = labels.Copied,
        FailedLabel = labels.CopyFailed,
        Announce = announce,
        Text = text
    };

    internal static TextBlock Text(string text, double size, double lineHeight, ArcDesignTokenKind color, bool mono = false)
    {
        var block = new TextBlock { Text = text, FontSize = size, LineHeight = lineHeight };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        if (mono)
        {
            ArcTokens.Bind(block, TextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        }
        return block;
    }

    /// <summary>The Input list: keys and values in mono 12/18, keys TextMuted, 16px apart, rows 4px apart; long values wrap.</summary>
    private static Control KeyValues(ToolCallItem item)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        for (var i = 0; i < item.Input.Count; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var argument = item.Input[i];
            var top = i == 0 ? 0 : 4;
            var key = Text(argument.Key, 12, 18, ArcDesignTokenKind.TextMuted, mono: true);
            key.Margin = new Thickness(0, top, 16, 0);
            var value = Text(argument.Value, 12, 18, ArcDesignTokenKind.Foreground, mono: true);
            value.Margin = new Thickness(0, top, 0, 0);
            value.TextWrapping = TextWrapping.Wrap;
            Grid.SetRow(key, i);
            Grid.SetRow(value, i);
            Grid.SetColumn(value, 1);
            grid.Children.Add(key);
            grid.Children.Add(value);
        }
        return grid;
    }

    /// <summary>A list: 32px rows of a mono lead (TextMuted), a 14px title (one line) and a TextSecondary tag, 12px apart.</summary>
    private static Control ListWell(ToolCallListOutput list, ToolCallLabels labels)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var rows = new List<Control[]>();
        for (var i = 0; i < list.Items.Count; i++)
        {
            var entry = list.Items[i];
            grid.RowDefinitions.Add(new RowDefinition(ListRowHeight, GridUnitType.Pixel));
            var cells = new List<Control>(3);
            if (!string.IsNullOrEmpty(entry.Lead))
            {
                var lead = Text(entry.Lead!, 12, 16.8, ArcDesignTokenKind.TextMuted, mono: true);
                lead.FontFeatures = TabularNumbers;
                lead.Margin = new Thickness(0, 9.1, 12, 0);
                Grid.SetRow(lead, i);
                cells.Add(lead);
            }
            var title = Text(entry.Title, 14, 19.6, ArcDesignTokenKind.Foreground);
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.Margin = new Thickness(0, 6.2, 0, 0);
            Grid.SetRow(title, i);
            Grid.SetColumn(title, 1);
            cells.Add(title);
            if (!string.IsNullOrEmpty(entry.Tag))
            {
                var tag = Text(entry.Tag!, 12, 16.8, ArcDesignTokenKind.TextSecondary);
                tag.Margin = new Thickness(12, 9.1, 0, 0);
                Grid.SetRow(tag, i);
                Grid.SetColumn(tag, 2);
                cells.Add(tag);
            }
            foreach (var cell in cells)
            {
                cell.VerticalAlignment = VerticalAlignment.Top;
                grid.Children.Add(cell);
            }
            rows.Add(cells.ToArray());
        }
        var hidden = list.Items.Count - FoldRows;
        return Foldable(grid, new Thickness(12, 4), hidden, FoldRows * ListRowHeight, labels.ShowMoreRows,
            labels, visible => SetRowsVisible(grid, rows, FoldRows, visible));
    }

    /// <summary>A table: a 30px header in TextMuted over a Border hairline, 30px rows of mono cells, columns 16px apart, the
    /// last one taking the rest; a table wider than its well scrolls inside it.</summary>
    private static Control TableWell(ToolCallTableOutput table, ToolCallLabels labels)
    {
        var columns = Math.Max(table.Columns.Count, table.Rows.Count == 0 ? 0 : table.Rows.Max(r => r.Count));
        var grid = new Grid();
        for (var c = 0; c < columns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(c == columns - 1 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto));
        }
        grid.RowDefinitions.Add(new RowDefinition(TableRowHeight, GridUnitType.Pixel));
        var rule = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Bottom };
        ArcTokens.Bind(rule, Border.BackgroundProperty, ArcDesignTokenKind.Border);
        Grid.SetColumnSpan(rule, Math.Max(1, columns));
        grid.Children.Add(rule);
        for (var c = 0; c < table.Columns.Count; c++)
        {
            var header = Text(table.Columns[c], 12, 16.8, ArcDesignTokenKind.TextMuted);
            header.VerticalAlignment = VerticalAlignment.Center;
            header.Margin = new Thickness(0, 0, c == columns - 1 ? 0 : 16, 0);
            Grid.SetColumn(header, c);
            grid.Children.Add(header);
        }
        var rows = new List<Control[]>();
        for (var r = 0; r < table.Rows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(TableRowHeight, GridUnitType.Pixel));
            var cells = new Control[columns];
            for (var c = 0; c < columns; c++)
            {
                var cell = Text(ToolCallTableOutput.CellText(table.Rows[r], c), 12, 16.8, ArcDesignTokenKind.Foreground, mono: true);
                cell.FontFeatures = TabularNumbers;
                cell.VerticalAlignment = VerticalAlignment.Center;
                cell.Margin = new Thickness(0, 0, c == columns - 1 ? 0 : 16, 0);
                Grid.SetRow(cell, r + 1);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
                cells[c] = cell;
            }
            rows.Add(cells);
        }
        var scroller = HorizontalScroller(grid);
        var hidden = table.Rows.Count - FoldRows;
        return Foldable(scroller, new Thickness(12, 4), hidden, (FoldRows + 1) * TableRowHeight, labels.ShowMoreRows,
            labels, visible => SetRowsVisible(grid, rows, FoldRows, visible));
    }

    /// <summary>JSON: 20px lines in a tinted mono font, folded after ten lines; wide lines scroll inside the well.</summary>
    private static Control JsonWell(ToolCallJsonOutput json, ToolCallLabels labels)
    {
        var view = new ToolCallJsonView();
        view.SetText(json.Json);
        AutomationProperties.SetName(view, json.Json);
        var hidden = view.LineCount - FoldLines;
        return Foldable(HorizontalScroller(view), new Thickness(12, 10, 12, hidden > 0 ? 6 : 10), hidden,
            FoldLines * ToolCallJsonView.LineHeight, labels.ShowMoreLines, labels, null);
    }

    private static Control TextWell(string text)
    {
        var block = Text(text, 14, 19.6, ArcDesignTokenKind.TextSecondary);
        block.TextWrapping = TextWrapping.Wrap;
        return Well(block, new Thickness(12, 10));
    }

    private static ScrollViewer HorizontalScroller(Control content) => new()
    {
        Content = content,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
    };

    private static void SetRowsVisible(Grid grid, List<Control[]> rows, int keep, bool visible)
    {
        for (var r = keep; r < rows.Count; r++)
        {
            foreach (var cell in rows[r])
            {
                cell.IsVisible = visible;
            }
        }
        // Hidden rows keep their fixed height so the folded height never needs a measure of the content.
        grid.InvalidateMeasure();
    }

    /// <summary>
    /// A well whose content folds when <paramref name="hidden"/> lines or rows exceed the fold: the fold box, then the
    /// "Show N more" button 4px under it, 6px in from the well's edge (Arc's padding 10 12 6 for JSON).
    /// </summary>
    private static Control Foldable(Control content, Thickness padding, int hidden, double foldedHeight, Func<int, string> showMore,
        ToolCallLabels labels, Action<bool>? setHiddenVisible)
    {
        if (hidden <= 0)
        {
            return Well(content, padding);
        }
        var box = new ToolCallFoldBox { Child = content, FoldedHeight = foldedHeight, FoldChanged = folded => setHiddenVisible?.Invoke(!folded) };
        setHiddenVisible?.Invoke(false);
        var label = new TextBlock { Text = showMore(hidden), FontSize = 12, LineHeight = 16.8, VerticalAlignment = VerticalAlignment.Center };
        var button = new ToolCallFoldButton
        {
            Content = label,
            Margin = new Thickness(-6, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        AutomationProperties.SetName(button, label.Text);
        button.Click += (_, _) =>
        {
            box.IsFolded = !box.IsFolded;
            button.IsExpanded = !box.IsFolded;
            label.Text = box.IsFolded ? showMore(hidden) : labels.ShowLess;
            AutomationProperties.SetName(button, label.Text);
        };
        return Well(new StackPanel { Children = { box, button } }, padding.WithBottom(6));
    }

    /// <summary>OpenType tabular digits.</summary>
    internal static readonly FontFeatureCollection TabularNumbers = new() { FontFeature.Parse("tnum") };

    /// <summary>The mono font of the tokens.</summary>
    internal static void Mono(TextBlock block) => ArcTokens.Bind(block, TextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
}

file static class ThicknessExtensions
{
    public static Thickness WithBottom(this Thickness thickness, double bottom) =>
        new(thickness.Left, thickness.Top, thickness.Right, bottom);
}
