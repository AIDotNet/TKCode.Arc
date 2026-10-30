using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One row of the enrich column's table: the row header (logo, name, domain) and the AI cell (status icon, the cell's
/// content, the row's run or retry control). It follows the <see cref="EnrichColumnRowItem"/> in its data context, so
/// the virtualizing list can recycle it; a cell change swaps only this row's content, one crossfade in a shared grid cell
/// (<see cref="ContentSwapMode.Label"/>: the old content out in 100ms, the new in 160ms rising 2px; nothing changes size).
/// A recycled row taking a new record shows it at once.
/// </summary>
[TemplatePart(LogoPart, typeof(EnrichColumnLogo))]
[TemplatePart(NamePart, typeof(TextBlock))]
[TemplatePart(StatusPart, typeof(EnrichColumnStatusGlyph))]
[TemplatePart(CellPart, typeof(ArcContentSwap))]
[TemplatePart(ActionPart, typeof(EnrichColumnButton))]
[TemplatePart(ActionGlyphPart, typeof(ArcGlyph))]
[PseudoClasses(CompactPseudoClass, NarrowPseudoClass, TouchPseudoClass, BusyPseudoClass)]
internal sealed class EnrichColumnRowView : TemplatedControl
{
    internal const string LogoPart = "PART_Logo";
    internal const string NamePart = "PART_Name";
    internal const string StatusPart = "PART_Status";
    internal const string CellPart = "PART_Cell";
    internal const string ActionPart = "PART_Action";
    internal const string ActionGlyphPart = "PART_ActionGlyph";

    internal const string CompactPseudoClass = ":compact";
    internal const string NarrowPseudoClass = ":narrow";
    internal const string TouchPseudoClass = ":touch";
    internal const string BusyPseudoClass = ":busy";

    private EnrichColumnRowItem? _item;
    private EnrichColumnLogo? _logo;
    private TextBlock? _name;
    private EnrichColumnStatusGlyph? _status;
    private ArcContentSwap? _cell;
    private EnrichColumnButton? _action;
    private ArcGlyph? _actionGlyph;
    private ToolCallText? _running;
    private string? _shownKey;
    private string? _nameKey;

    static EnrichColumnRowView()
    {
        FocusableProperty.OverrideDefaultValue<EnrichColumnRowView>(false);
    }

    /// <summary>The row shown now.</summary>
    internal EnrichColumnRowItem? Item => _item;

    /// <summary>The cell's content on screen (tests read it).</summary>
    internal Control? CellContent => _cell?.Content as Control;

    internal EnrichColumnButton? Action => _action;

    internal TextBlock? NameText => _name;

    private EnrichColumnDensity Density => EnrichColumn.GetDensity(this);

    private EnrichColumnLabels Labels => _item?.Owner.Labels ?? EnrichColumnLabels.Default;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_action is not null)
        {
            _action.Click -= OnAction;
        }
        _logo = e.NameScope.Find<EnrichColumnLogo>(LogoPart);
        _name = e.NameScope.Find<TextBlock>(NamePart);
        _status = e.NameScope.Find<EnrichColumnStatusGlyph>(StatusPart);
        _cell = e.NameScope.Find<ArcContentSwap>(CellPart);
        _action = e.NameScope.Find<EnrichColumnButton>(ActionPart);
        _actionGlyph = e.NameScope.Find<ArcGlyph>(ActionGlyphPart);
        if (_action is not null)
        {
            _action.Click += OnAction;
        }
        _shownKey = null;
        _nameKey = null;
        _running = null;
        UpdateDensityClasses();
        Apply(instant: true);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        var item = DataContext as EnrichColumnRowItem;
        if (ReferenceEquals(item, _item))
        {
            return;
        }
        if (_item is not null)
        {
            _item.Changed -= OnItemChanged;
        }
        _item = item;
        if (_item is not null)
        {
            _item.Changed += OnItemChanged;
        }
        Apply(instant: true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_item is not null)
        {
            _item.Changed -= OnItemChanged;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_item is not null)
        {
            _item.Changed -= OnItemChanged;
            _item.Changed += OnItemChanged;
            Apply(instant: true);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EnrichColumn.DensityProperty)
        {
            UpdateDensityClasses();
            _shownKey = null;
            _nameKey = null;
            Apply(instant: true);
        }
        else if (change.Property == EnrichColumn.IsTouchProperty)
        {
            PseudoClasses.Set(TouchPseudoClass, EnrichColumn.GetIsTouch(this));
        }
    }

    private void UpdateDensityClasses()
    {
        var density = Density;
        PseudoClasses.Set(CompactPseudoClass, density != EnrichColumnDensity.Regular);
        PseudoClasses.Set(NarrowPseudoClass, density == EnrichColumnDensity.Narrow);
        PseudoClasses.Set(TouchPseudoClass, EnrichColumn.GetIsTouch(this));
    }

    private void OnItemChanged(EnrichColumnRowItem item)
    {
        if (ReferenceEquals(item, _item))
        {
            Apply(instant: false);
        }
    }

    private void OnAction(object? sender, RoutedEventArgs e)
    {
        if (_item is { } item)
        {
            item.Owner.RunRow(item.Row.Id);
        }
    }

    /// <summary>Brings the row in line with its item; parts that already show the value are left alone.</summary>
    private void Apply(bool instant)
    {
        if (_cell is null || _item is not { } item)
        {
            return;
        }
        var row = item.Row;
        var cell = item.Cell;
        var labels = Labels;
        var density = Density;

        if (_logo is not null)
        {
            _logo.Row = row;
        }
        UpdateName(row, density);

        if (_status is not null)
        {
            if (instant)
            {
                _status.SetInstantly(cell.Status);
            }
            else
            {
                _status.Status = cell.Status;
            }
        }

        var busy = cell.Status is EnrichColumnCellStatus.Queued or EnrichColumnCellStatus.Running;
        PseudoClasses.Set(BusyPseudoClass, busy);
        if (_action is not null)
        {
            _action.IsVisible = !busy;
            var failed = cell.Status == EnrichColumnCellStatus.Failed;
            AutomationProperties.SetName(_action, failed ? labels.RetryRow(row.Name) : labels.RunRow(row.Name));
            Tooltip.SetTip(_action, failed ? labels.RetryRow(row.Name) : labels.RunRow(row.Name));
            if (_actionGlyph is not null)
            {
                _actionGlyph.Data = failed ? ArcIcons.RotateCcw : cell.HasValue ? ArcIcons.RefreshCw : ArcIcons.Play;
            }
        }

        UpdateCell(item, instant);
        AutomationProperties.SetName(this, row.Name + ": " + Spoken(cell, row, labels));
    }

    private void UpdateName(EnrichColumnRow row, EnrichColumnDensity density)
    {
        if (_name is null)
        {
            return;
        }
        var domain = density == EnrichColumnDensity.Regular ? row.Domain : null;
        var key = row.Name + "\n" + domain;
        if (key == _nameKey)
        {
            return;
        }
        _nameKey = key;
        var inlines = new InlineCollection { new Run(row.Name) { FontWeight = FontWeight.Medium } };
        if (!string.IsNullOrEmpty(domain))
        {
            var run = new Run("  " + domain) { FontWeight = FontWeight.Normal };
            ArcTokens.Bind(run, TextElement.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            inlines.Add(run);
        }
        _name.Inlines = inlines;
        AutomationProperties.SetName(_name, row.Name);
    }

    /// <summary>What a screen reader hears for the cell.</summary>
    internal static string Spoken(EnrichColumnCell cell, EnrichColumnRow row, EnrichColumnLabels labels) => cell.Status switch
    {
        EnrichColumnCellStatus.Queued => labels.Queued,
        EnrichColumnCellStatus.Running => cell.Activity ?? labels.Working(row),
        EnrichColumnCellStatus.Done => cell.Value ?? string.Empty,
        EnrichColumnCellStatus.Failed => labels.Failed + ", " + (cell.Error ?? labels.UnknownError),
        _ => labels.NotRun,
    };

    private void UpdateCell(EnrichColumnRowItem item, bool instant)
    {
        var cell = item.Cell;
        var density = Density;
        // A running cell keeps its label: a new activity trades places inside the shimmering text.
        if (cell.Status == EnrichColumnCellStatus.Running && _running is not null && _shownKey == "running")
        {
            _running.Text = cell.Activity ?? Labels.Working(item.Row);
            return;
        }
        var key = cell.Status switch
        {
            EnrichColumnCellStatus.Running => "running",
            EnrichColumnCellStatus.Done => "done\n" + cell.Value + "\n" + cell.Source?.Label + "\n" + cell.Source?.Href + "\n" + density,
            EnrichColumnCellStatus.Failed => "failed\n" + cell.Error + "\n" + density,
            _ => cell.Status.ToString(),
        };
        if (key == _shownKey)
        {
            return;
        }
        _shownKey = key;
        var content = Build(item, density);
        var swap = _cell!;
        if (instant)
        {
            var mode = swap.Mode;
            swap.Mode = ContentSwapMode.None;
            swap.Content = content;
            swap.Mode = mode;
        }
        else
        {
            swap.Content = content;
        }
    }

    private Control Build(EnrichColumnRowItem item, EnrichColumnDensity density)
    {
        var cell = item.Cell;
        var labels = Labels;
        _running = null;
        switch (cell.Status)
        {
            case EnrichColumnCellStatus.Queued:
                return Text(labels.Queued, ArcDesignTokenKind.TextMuted);
            case EnrichColumnCellStatus.Running:
            {
                var text = new ToolCallText
                {
                    Text = cell.Activity ?? labels.Working(item.Row),
                    IsShimmering = true,
                    Swap = ToolCallTextSwap.Fade,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                ArcTokens.Bind(text, ToolCallText.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
                ArcTokens.Bind(text, ToolCallText.MutedForegroundProperty, ArcDesignTokenKind.TextMuted);
                _running = text;
                return text;
            }
            case EnrichColumnCellStatus.Done:
                return BuildDone(item, density);
            case EnrichColumnCellStatus.Failed:
                return BuildFailed(cell, density, labels);
            default:
            {
                var dash = Text("—", ArcDesignTokenKind.TextMuted);
                AutomationProperties.SetName(dash, labels.NotRun);
                return dash;
            }
        }
    }

    private Control BuildDone(EnrichColumnRowItem item, EnrichColumnDensity density)
    {
        var cell = item.Cell;
        var value = Text(cell.Value ?? string.Empty, ArcDesignTokenKind.Foreground);
        if (density == EnrichColumnDensity.Narrow)
        {
            value.TextWrapping = TextWrapping.Wrap;
            value.MaxLines = 2;
        }
        Tooltip.SetTip(value, cell.Value);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6 };
        grid.Children.Add(value);
        if (cell.Source is { } source)
        {
            var link = SourceLink(item, source, density);
            Grid.SetColumn(link, 1);
            grid.Children.Add(link);
        }
        return grid;
    }

    private Control SourceLink(EnrichColumnRowItem item, EnrichColumnSource source, EnrichColumnDensity density)
    {
        var labels = Labels;
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        if (density == EnrichColumnDensity.Regular)
        {
            panel.Children.Add(new TextBlock
            {
                Text = source.Label,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 160,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        panel.Children.Add(new ArcGlyph { Data = ArcIcons.ArrowUpRight, Size = 12, VerticalAlignment = VerticalAlignment.Center });
        var link = new EnrichColumnButton { Content = panel, VerticalAlignment = VerticalAlignment.Center };
        link.Classes.Add("source");
        AutomationProperties.SetName(link, labels.OpenSource(source.Label));
        Tooltip.SetTip(link, source.Href);
        link.Click += (_, _) => item.Owner.OpenSource(item.Row, source);
        return link;
    }

    private static Control BuildFailed(EnrichColumnCell cell, EnrichColumnDensity density, EnrichColumnLabels labels)
    {
        var reason = cell.Error ?? labels.UnknownError;
        var failed = Text(labels.Failed, ArcDesignTokenKind.Danger);
        failed.FontWeight = FontWeight.Medium;
        if (density == EnrichColumnDensity.Narrow)
        {
            // The reason hides behind the label.
            Tooltip.SetTip(failed, reason);
            AutomationProperties.SetName(failed, labels.Failed + ", " + reason);
            return failed;
        }
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 6 };
        grid.Children.Add(failed);
        var text = Text(reason, ArcDesignTokenKind.TextMuted);
        Tooltip.SetTip(text, reason);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return grid;
    }

    private static TextBlock Text(string text, ArcDesignTokenKind brush)
    {
        var block = new TextBlock
        {
            Text = text,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, brush);
        return block;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    private sealed class RowPeer(EnrichColumnRowView owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataItem;
    }
}
