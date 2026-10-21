using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DataGridLedgerDemo : UserControl
{
    public DataGridLedgerDemo()
    {
        InitializeComponent();
        string[] accounts = ["Cash", "Receivables", "Revenue", "Payroll", "Hosting"];
        string[] periods = ["2026-01", "2026-02", "2026-03"];
        string[] memos = ["Invoice", "Refund", "Accrual", "Transfer", "Adjustment"];
        LedgerGrid.Columns[0].Options = accounts;
        LedgerGrid.Columns[1].Options = periods;
        var random = new Random(7);
        var rows = new List<DataGridRow>();
        for (var i = 0; i < 240; i++)
        {
            var month = random.Next(3);
            var amount = Math.Round((decimal)(random.NextDouble() * 4000 + 50), 2);
            var debit = random.Next(2) == 0;
            rows.Add(new DataGridRow($"line-{i + 1}")
            {
                { "account", accounts[random.Next(accounts.Length)] },
                { "period", periods[month] },
                { "date", new DateOnly(2026, month + 1, 1 + random.Next(28)) },
                { "memo", $"{memos[random.Next(memos.Length)]} {1000 + i}" },
                { "debit", debit ? amount : null },
                { "credit", debit ? null : amount }
            });
        }
        LedgerGrid.ItemsSource = rows;
        LedgerGrid.GroupBy = ["account", "period"];
    }
}
