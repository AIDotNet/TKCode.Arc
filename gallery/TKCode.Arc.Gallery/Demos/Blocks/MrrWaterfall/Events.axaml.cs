using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MrrWaterfallEventsDemo : UserControl
{
    private readonly List<string> _lines = new();

    public MrrWaterfallEventsDemo()
    {
        InitializeComponent();
        Bridge.Periods.AddRange(MrrWaterfallDemoData.Periods());
        Bridge.Labels.Hint = "Click a bar, or press an arrow key, to open its accounts";

        Churn.Click += (_, _) => Bridge.Select(5);
        Wrap.Click += (_, _) => Bridge.Select((Bridge.SelectedIndex + 1) % 7);
        Percent.Click += (_, _) => Bridge.ValueMode =
            Bridge.ValueMode == MrrValueMode.Currency ? MrrValueMode.PercentOfStart : MrrValueMode.Currency;

        Bridge.SelectionChanged += (_, e) => Write($"Bar {e.OldIndex} → {e.NewIndex}: {e.Name}" +
                                                    (e.Movement is { } m ? $" ({m.Kind}, {m.Accounts.Count} named accounts)" : " (a total)"));
        Bridge.PeriodChanged += (_, e) => Write($"Period: {e.OldPeriod?.Label} → {e.NewPeriod?.Label}");
        Bridge.ValueModeChanged += (_, e) => Write($"Unit: {e.OldMode} → {e.NewMode}");
    }

    private void Write(string line)
    {
        _lines.Insert(0, line);
        if (_lines.Count > 5)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        Log.Text = string.Join(Environment.NewLine, _lines);
    }
}
