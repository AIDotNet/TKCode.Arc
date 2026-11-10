using System.Globalization;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DateReelPreviewDemo : UserControl
{
    public DateReelPreviewDemo()
    {
        InitializeComponent();
    }

    private void OnDateChanged(object? sender, DateReelSelectedDateChangedEventArgs e)
    {
        if (Readout is not null)
        {
            Readout.Text = e.NewDate.ToString("D", CultureInfo.GetCultureInfo("en-US"));
        }
    }
}
