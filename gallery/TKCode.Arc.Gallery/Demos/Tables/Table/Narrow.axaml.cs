using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc.Gallery.Demos;

public partial class TableNarrowDemo : UserControl
{
    private sealed record Project(string Name, string Owner, string Status, decimal Budget, DateOnly Due);

    private static readonly CultureInfo s_en = CultureInfo.GetCultureInfo("en-US");

    private static readonly Project[] s_projects =
    [
        new("Atlas redesign", "Mina Park", "Active", 42000, new DateOnly(2026, 11, 14)),
        new("Billing migration", "Theo Walsh", "Review", 18500, new DateOnly(2026, 10, 30)),
        new("Checkout v3", "Ana Ruiz", "Active", 126000, new DateOnly(2027, 1, 9)),
        new("Docs search", "Kai Moreno", "Paused", 9600, new DateOnly(2026, 12, 2)),
        new("Edge caching", "Lee Chen", "Done", 31250, new DateOnly(2026, 9, 18)),
        new("Field reports", "Ivy Brooks", "Active", 57800, new DateOnly(2026, 11, 28))
    ];

    public TableNarrowDemo()
    {
        InitializeComponent();
        Table.ItemsSource = s_projects;
        AddProjectColumns(Table);
    }

    private static void AddProjectColumns(Table table)
    {
        table.Columns.Add(DataTableColumn.Create<Project>("Project", p => p.Name));
        table.Columns.Add(DataTableColumn.Create<Project>("Owner", p => p.Owner));
        table.Columns.Add(new DataTableColumn("Status", p => ((Project)p).Status) { CellTemplate = new FuncDataTemplate<Project>((_, _) => new StatusPill()) });
        table.Columns.Add(new DataTableColumn("Budget", p => ((Project)p).Budget) { Formatter = Money });
        table.Columns.Add(new DataTableColumn("Due", p => ((Project)p).Due) { Formatter = v => v is DateOnly d ? d.ToString("MMM d, yyyy", s_en) : "–" });
    }

    private static string Money(object? value) => value is decimal amount ? amount.ToString("C0", s_en) : "–";

    /// <summary>A status badge that follows its data context (cells are recycled between rows).</summary>
    private sealed class StatusPill : Border
    {
        private readonly TextBlock _text = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        private IDisposable? _fill;
        private IDisposable? _fore;

        public StatusPill()
        {
            Child = _text;
            Padding = new Thickness(8, 2);
            CornerRadius = new CornerRadius(999);
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Center;
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            var status = (DataContext as Project)?.Status ?? string.Empty;
            _text.Text = status;
            var (fill, fore) = status switch
            {
                "Active" => (ArcDesignTokenKind.SuccessSubtle, ArcDesignTokenKind.Success),
                "Paused" => (ArcDesignTokenKind.WarningSubtle, ArcDesignTokenKind.Warning),
                "Review" => (ArcDesignTokenKind.AccentSubtle, ArcDesignTokenKind.AccentStrong),
                _ => (ArcDesignTokenKind.SurfaceMuted, ArcDesignTokenKind.TextSecondary)
            };
            _fill?.Dispose();
            _fore?.Dispose();
            _fill = ArcTokens.Bind(this, BackgroundProperty, fill);
            _fore = ArcTokens.Bind(_text, TextBlock.ForegroundProperty, fore);
        }
    }
}
