using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

// Builds the table from the data: the column header, the total row, one disclosure row per department with its lines in
// a height spring, and the legend. The rows are code-built from internal parts that carry their own ControlThemes
// (BudgetVarianceTheme.axaml); text colors and sizes of plain text blocks are bound to ArcDesign tokens here.
public partial class BudgetVariance
{
    private readonly List<BudgetVarianceRowPanel> _rowPanels = new();
    private readonly List<BudgetDepartmentView> _departments = new();
    private readonly List<Control> _columnHeaders = new();
    private BudgetFigure? _total;
    private Control? _headerLead;
    private BudgetVarianceScale? _scale;

    /// <summary>The views of one figure (the total, a department or a line) that a period change updates.</summary>
    private sealed class BudgetFigure
    {
        public required string Name { get; init; }
        public required Func<string, (double Budget, double Actual)> Amounts { get; init; }
        public required BudgetVarianceRowPanel Panel { get; init; }
        public required BudgetBulletBar Bar { get; init; }
        public required FinanceCountingText Actual { get; init; }
        public required FinanceCountingText Budget { get; init; }
        public required FinanceCountingText Variance { get; init; }
        public required FinanceCountingText Share { get; init; }
        public required FinanceCountingText StackedActual { get; init; }
        public required FinanceCountingText StackedBudget { get; init; }
        public required Control StackedLine { get; init; }
        public Control? Sub { get; init; }
        public Control? SentenceTarget { get; private set; }

        /// <summary>The element that carries the figure's sentence (the disclosure button, or the row itself).</summary>
        public void SetTarget(Control target) => SentenceTarget = target;

        public void Apply(double budget, double actual, double scale, double low, double high, BudgetVarianceKind kind, string sentence,
            bool animate)
        {
            Bar.Budget = 1 / scale;
            Bar.Low = low / scale;
            Bar.High = high / scale;
            Bar.Fraction = budget > 0 ? actual / budget / scale : actual > 0 ? 1 : 0;
            Set(Actual, actual, animate);
            Set(Budget, budget, animate);
            Set(Variance, actual - budget, animate);
            Set(Share, budget > 0 ? (actual - budget) / budget : 0, animate);
            Set(StackedActual, actual, animate);
            Set(StackedBudget, budget, animate);
            Variance.Classes.Set("over", kind == BudgetVarianceKind.Over);
            Variance.Classes.Set("under", kind == BudgetVarianceKind.Under);
            Variance.Classes.Set("onplan", kind == BudgetVarianceKind.OnPlan);
            if (SentenceTarget is not null)
            {
                AutomationProperties.SetName(SentenceTarget, sentence);
            }
        }

        public void ApplyStacked(bool stacked)
        {
            if (Sub is not null)
            {
                Sub.IsVisible = !stacked;
            }
            StackedLine.IsVisible = stacked;
            Actual.IsVisible = !stacked;
            Budget.IsVisible = !stacked;
        }

        private static void Set(FinanceCountingText text, double value, bool animate)
        {
            text.Value = value;
            if (!animate)
            {
                text.Settle();
            }
        }
    }

    /// <summary>A department: its disclosure row, its lines and the panel and spring that hold them.</summary>
    private sealed class BudgetDepartmentView
    {
        public required BudgetDepartment Department { get; init; }
        public required BudgetFigure Figure { get; init; }
        public required List<BudgetFigure> Lines { get; init; }
        public required BudgetVarianceItemHost Host { get; init; }
        public required BudgetVarianceRowButton Button { get; init; }
        public required BudgetVarianceChevron Chevron { get; init; }
        public required SpringSizeDecorator Disclosure { get; init; }
        public required BudgetVarianceLinesHost LinesHost { get; init; }
    }

    private enum FigureLevel
    {
        Total,
        Department,
        Line,
    }

    private void BuildTable()
    {
        var table = _table!;
        table.Children.Clear();
        _rowPanels.Clear();
        _departments.Clear();
        _columnHeaders.Clear();
        _total = null;
        var labels = Labels;

        // Column header: the names of the columns and the scale over the charts.
        var header = NewRowPanel("header");
        _headerLead = Caption(labels.Department, HorizontalAlignment.Left);
        BudgetVarianceRowPanel.SetRole(_headerLead, BudgetVarianceRole.Lead);
        _scale = new BudgetVarianceScale { Maximum = ScaleMaximum };
        ArcTokens.Bind(_scale, BudgetVarianceScale.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_scale, BudgetVarianceScale.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(_scale, BudgetVarianceScale.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        BudgetVarianceRowPanel.SetRole(_scale, BudgetVarianceRole.Chart);
        header.Children.Add(_headerLead);
        header.Children.Add(_scale);
        foreach (var (text, role) in new[] { (labels.Actual, BudgetVarianceRole.Actual), (labels.Budget, BudgetVarianceRole.Budget), (labels.Variance, BudgetVarianceRole.Variance) })
        {
            var caption = Caption(text, HorizontalAlignment.Right);
            BudgetVarianceRowPanel.SetRole(caption, role);
            header.Children.Add(caption);
            _columnHeaders.Add(caption);
        }
        AutomationProperties.SetAccessibilityView(header, AccessibilityView.Raw);
        table.Children.Add(header);

        // The total row.
        var totalPanel = NewRowPanel("total");
        var total = BuildFigure(FigureLevel.Total, labels.Total, labels.DepartmentCount(Departments.Count), null, TotalIn, totalPanel);
        _total = total;
        var totalHost = new BudgetVarianceTotalHost { Child = totalPanel };
        total.SetTarget(totalHost);
        table.Children.Add(totalHost);

        // The departments.
        var list = new StackPanel { Spacing = 2 };
        foreach (var department in Departments)
        {
            list.Children.Add(BuildDepartment(department));
        }
        table.Children.Add(list);
    }

    private Control BuildDepartment(BudgetDepartment department)
    {
        var panel = NewRowPanel("department");
        var avatar = new Avatar { DisplayName = department.Owner ?? department.Name, Size = AvatarSize.Small, Source = department.OwnerAvatar };
        AutomationProperties.SetAccessibilityView(avatar, AccessibilityView.Raw);
        var figure = BuildFigure(FigureLevel.Department, department.Name, department.Owner, avatar, department.In, panel);
        var chevron = new BudgetVarianceChevron { Data = ArcIcons.ChevronDown };
        BudgetVarianceRowPanel.SetRole(chevron, BudgetVarianceRole.Chevron);
        panel.Children.Add(chevron);

        var button = new BudgetVarianceRowButton { Content = panel };
        figure.SetTarget(button);

        var lines = new List<BudgetFigure>();
        var linesPanel = new StackPanel();
        foreach (var line in department.Lines)
        {
            var linePanel = NewRowPanel("line");
            var lineFigure = BuildFigure(FigureLevel.Line, line.Name, null, null, line.In, linePanel);
            var lineHost = new BudgetVarianceLineHost { Child = linePanel };
            lineFigure.SetTarget(lineHost);
            linesPanel.Children.Add(lineHost);
            lines.Add(lineFigure);
        }
        var linesHost = new BudgetVarianceLinesHost { Child = linesPanel, IsHitTestVisible = false };
        AutomationProperties.SetName(linesHost, Labels.LinesRegion(department.Name));
        var disclosure = new SpringSizeDecorator
        {
            Axis = SpringSizeAxis.Height,
            Spring = ArcSpring.Smooth,
            ShrinkSpring = ArcSpring.Quick,
            IsExpanded = false,
            Child = linesHost,
        };
        var host = new BudgetVarianceItemHost
        {
            Child = new StackPanel { Children = { button, disclosure } },
        };
        var view = new BudgetDepartmentView
        {
            Department = department,
            Figure = figure,
            Lines = lines,
            Host = host,
            Button = button,
            Chevron = chevron,
            Disclosure = disclosure,
            LinesHost = linesHost,
        };
        button.Click += (_, _) => ToggleDepartment(department.Id);
        _departments.Add(view);
        if (department.Id == ExpandedDepartment)
        {
            SetExpanded(view, true);
        }
        return host;
    }

    private BudgetFigure BuildFigure(FigureLevel level, string name, string? sub, Avatar? avatar,
        Func<string, (double Budget, double Actual)> amounts, BudgetVarianceRowPanel panel)
    {
        var currency = Currency;
        var labels = Labels;
        var threshold = OnPlanThreshold;

        var nameText = new TextBlock { Text = name, TextWrapping = TextWrapping.Wrap };
        Bind(nameText, ArcDesignTokenKind.TypeSM, level == FigureLevel.Line ? ArcDesignTokenKind.TextSecondary : ArcDesignTokenKind.Foreground);
        if (level != FigureLevel.Line)
        {
            ArcTokens.Bind(nameText, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        }

        TextBlock? subText = null;
        if (!string.IsNullOrEmpty(sub))
        {
            subText = new TextBlock { Text = sub, TextTrimming = TextTrimming.CharacterEllipsis };
            Bind(subText, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted);
        }

        var stackedActual = Count(v => FinanceFormat.Compact(v, currency), "xs muted left");
        var stackedBudget = Count(v => FinanceFormat.Compact(v, currency), "xs muted left");
        var (before, middle, after) = Split(labels.ActualOfBudget("\u0001", "\u0002"));
        var stackedLine = new StackPanel { Orientation = Orientation.Horizontal, IsVisible = false };
        AddPiece(stackedLine, before);
        stackedLine.Children.Add(stackedActual);
        AddPiece(stackedLine, middle);
        stackedLine.Children.Add(stackedBudget);
        AddPiece(stackedLine, after);

        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(nameText);
        if (subText is not null)
        {
            texts.Children.Add(subText);
        }
        texts.Children.Add(stackedLine);

        Control lead = texts;
        if (avatar is not null)
        {
            var row = new DockPanel();
            avatar.Margin = new Thickness(0, 0, 12, 0);
            avatar.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(avatar, Dock.Left);
            row.Children.Add(avatar);
            row.Children.Add(texts);
            lead = row;
        }
        BudgetVarianceRowPanel.SetRole(lead, BudgetVarianceRole.Lead);

        var bar = new BudgetBulletBar { Classes = { LevelClass(level) } };
        BudgetVarianceRowPanel.SetRole(bar, BudgetVarianceRole.Chart);

        var size = level == FigureLevel.Total ? "base" : string.Empty;
        var actual = Count(v => FinanceFormat.Compact(v, currency), size);
        BudgetVarianceRowPanel.SetRole(actual, BudgetVarianceRole.Actual);
        var budget = Count(v => FinanceFormat.Compact(v, currency), "secondary");
        BudgetVarianceRowPanel.SetRole(budget, BudgetVarianceRole.Budget);

        var variance = Count(v => FinanceFormat.SignedCompact(Math.Round(v), currency), size);
        var share = Count(v => FinanceFormat.SignedPercent(v) + " " + (Math.Abs(v) < threshold ? labels.OnPlan : v > 0 ? labels.Over : labels.Under),
            "xs muted");
        var varianceCell = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        varianceCell.Children.Add(variance);
        varianceCell.Children.Add(share);
        BudgetVarianceRowPanel.SetRole(varianceCell, BudgetVarianceRole.Variance);

        panel.Children.Add(lead);
        panel.Children.Add(bar);
        panel.Children.Add(actual);
        panel.Children.Add(budget);
        panel.Children.Add(varianceCell);
        foreach (var child in panel.Children)
        {
            AutomationProperties.SetAccessibilityView(child, AccessibilityView.Raw);
        }

        return new BudgetFigure
        {
            Name = name,
            Amounts = amounts,
            Panel = panel,
            Bar = bar,
            Actual = actual,
            Budget = budget,
            Variance = variance,
            Share = share,
            StackedActual = stackedActual,
            StackedBudget = stackedBudget,
            StackedLine = stackedLine,
            Sub = subText,
        };
    }

    private static string LevelClass(FigureLevel level) => level switch
    {
        FigureLevel.Total => "total",
        FigureLevel.Department => "department",
        _ => "line",
    };

    private BudgetVarianceRowPanel NewRowPanel(string kind)
    {
        var panel = new BudgetVarianceRowPanel { Classes = { kind }, IsTable = !_isStacked };
        _rowPanels.Add(panel);
        return panel;
    }

    private static FinanceCountingText Count(Func<double, string> formatter, string classes)
    {
        var text = new FinanceCountingText { Formatter = formatter };
        foreach (var name in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            text.Classes.Add(name);
        }
        return text;
    }

    private static (string Before, string Middle, string After) Split(string pattern)
    {
        var a = pattern.IndexOf('\u0001');
        var b = pattern.IndexOf('\u0002');
        if (a < 0 || b < a)
        {
            return (string.Empty, " / ", string.Empty);
        }
        return (pattern[..a], pattern[(a + 1)..b], pattern[(b + 1)..]);
    }

    private static void AddPiece(Panel panel, string text)
    {
        if (text.Length == 0)
        {
            return;
        }
        var piece = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        Bind(piece, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted);
        panel.Children.Add(piece);
    }

    private static TextBlock Caption(string text, HorizontalAlignment alignment)
    {
        var caption = new TextBlock { Text = text, HorizontalAlignment = alignment, VerticalAlignment = VerticalAlignment.Center };
        Bind(caption, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted);
        return caption;
    }

    private static void Bind(TextBlock text, ArcDesignTokenKind size, ArcDesignTokenKind color)
    {
        ArcTokens.Bind(text, TextBlock.FontSizeProperty, size);
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, color);
    }

    private void BuildLegend()
    {
        if (_legend is null)
        {
            return;
        }
        _legend.Children.Clear();
        _legend.IsVisible = IsLegendVisible;
        var labels = Labels;
        _legend.Children.Add(LegendItem(new BudgetVarianceSwatch { Classes = { "actual" } }, labels.LegendActual));
        _legend.Children.Add(LegendItem(new BudgetVarianceSwatch { Classes = { "budget" } }, labels.LegendBudget));
        _legend.Children.Add(LegendItem(new BudgetVarianceSwatch { Classes = { "over" } }, labels.LegendOver));
        var low = (RangeLow * 100).ToString("0.#", FinanceFormat.Culture);
        var high = (RangeHigh * 100).ToString("0.#", FinanceFormat.Culture);
        _legend.Children.Add(LegendItem(new BudgetVarianceSwatch { Classes = { "band" } }, labels.LegendBand(low, high)));
    }

    private static Control LegendItem(Control swatch, string text)
    {
        var caption = Caption(text, HorizontalAlignment.Left);
        swatch.VerticalAlignment = VerticalAlignment.Center;
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 20, 4),
            Children = { swatch, caption },
        };
    }
}
