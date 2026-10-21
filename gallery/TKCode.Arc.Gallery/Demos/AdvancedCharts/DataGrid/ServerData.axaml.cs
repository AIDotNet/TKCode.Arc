using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DataGridServerDataDemo : UserControl
{
    public DataGridServerDataDemo()
    {
        InitializeComponent();
        OrdersGrid.Columns[2].Options = OrdersSource.Statuses;
        OrdersGrid.RowKeySelector = row => ((DataGridRow)row).Key;
        OrdersGrid.DataSource = new OrdersSource(100_000);
    }

    /// <summary>
    /// A stand-in for a server: 100,000 orders filtered, sorted and searched off the UI thread with the grid's public
    /// helpers, then returned one block at a time after a short delay.
    /// </summary>
    private sealed class OrdersSource(int count) : IDataGridDataSource
    {
        public static readonly string[] Statuses = ["Pending", "Paid", "Shipped", "Refunded"];
        private static readonly string[] Customers = ["Alder", "Basalt", "Cobalt", "Delta", "Ember", "Fjord", "Garnet", "Harbor"];
        private readonly Dictionary<string, int[]> _queries = new();

        public int BlockSize => 200;

        private static object? Value(int index, string key) => key switch
        {
            "order" => $"SO-{100000 + index}",
            "customer" => $"{Customers[index % Customers.Length]} {index % 97 + 1}",
            "status" => Statuses[index * 7 % Statuses.Length],
            "placed" => new DateOnly(2026, 1, 1).AddDays(index % 300),
            "amount" => Math.Round(20m + index * 37 % 5000 + index % 100 / 100m, 2),
            _ => null
        };

        public async Task<DataGridRowsResult> GetRowsAsync(DataGridRowsRequest request, CancellationToken cancellation)
        {
            await Task.Delay(350, cancellation);
            var order = await Task.Run(() => Query(request), cancellation);
            var rows = new List<object>();
            for (var i = request.Start; i < Math.Min(request.End, order.Length); i++)
            {
                var index = order[i];
                var row = new DataGridRow($"o{index}");
                foreach (var key in new[] { "order", "customer", "status", "placed", "amount" })
                {
                    row[key] = Value(index, key);
                }
                rows.Add(row);
            }
            var total = DataGridAggregates.Compute(order.Select(i => Value(i, "amount")), DataGridAggregate.Sum);
            return new DataGridRowsResult(rows, order.Length, new Dictionary<string, decimal?> { ["amount"] = total });
        }

        private int[] Query(DataGridRowsRequest request)
        {
            var key = string.Join("|", request.Sort.Select(s => $"{s.Key}{s.Direction}")) + "#" +
                      string.Join("|", request.Filters.Select(f => $"{f.Key}={f.Value}")) + "#" + request.Search;
            lock (_queries)
            {
                if (_queries.TryGetValue(key, out var cached))
                {
                    return cached;
                }
            }
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            IEnumerable<int> rows = Enumerable.Range(0, count);
            foreach (var (column, filter) in request.Filters)
            {
                if (filter.Values is { } keep)
                {
                    rows = rows.Where(i => keep.Contains(Value(i, column) as string ?? string.Empty));
                }
                else if (column == "amount" && DataGridFilterExpression.TryParseNumber(filter.Expression, culture, out var number) && number is not null)
                {
                    rows = rows.Where(i => Value(i, column) is decimal d && number(d));
                }
                else if (column == "placed" && DataGridFilterExpression.TryParseDate(filter.Expression, culture, out var date) && date is not null)
                {
                    rows = rows.Where(i => Value(i, column) is DateOnly d && date(d));
                }
                else if (DataGridFilterExpression.CreateText(filter.Expression, culture) is { } text)
                {
                    rows = rows.Where(i => text(Value(i, column)?.ToString() ?? string.Empty));
                }
            }
            if (request.Search.Length > 0)
            {
                rows = rows.Where(i => $"{Value(i, "order")} {Value(i, "customer")} {Value(i, "status")}"
                    .Contains(request.Search, StringComparison.OrdinalIgnoreCase));
            }
            var result = DataGridSorting.Sort(rows, request.Sort, Value, culture: culture).ToArray();
            lock (_queries)
            {
                _queries[key] = result;
            }
            return result;
        }
    }
}
