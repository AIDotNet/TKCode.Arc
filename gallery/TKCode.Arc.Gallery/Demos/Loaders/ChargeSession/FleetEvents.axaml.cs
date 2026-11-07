using System.Globalization;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ChargeSessionFleetEventsDemo : UserControl
{
    private static readonly CultureInfo s_german = CultureInfo.GetCultureInfo("de-DE");
    private readonly List<string> _entries = new();

    public ChargeSessionFleetEventsDemo()
    {
        InitializeComponent();
    }

    private void OnStateChanged(object? sender, ChargeSessionStateChangedEventArgs e)
    {
        var d = e.Detail;
        var what = e.IsNewSession ? "new session" : e.State.ToString().ToLowerInvariant();
        Write($"{what}: {d.Charge.ToString("0.0", s_german)}%, {d.EnergyAdded.ToString("0.0", s_german)} kWh, " +
              $"{d.Cost.ToString("0.00", s_german)} €, limit {d.Limit:0}%");
    }

    private void OnLimitChanged(object? sender, ChargeSessionLimitChangedEventArgs e) => Write($"limit set to {e.Limit:0}%");

    private void Write(string text)
    {
        _entries.Add(text);
        if (_entries.Count > 4)
        {
            _entries.RemoveAt(0);
        }
        Log.Text = string.Join("  ·  ", _entries);
    }
}
