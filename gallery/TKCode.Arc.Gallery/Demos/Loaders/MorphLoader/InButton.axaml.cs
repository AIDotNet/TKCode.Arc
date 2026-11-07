using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

public partial class MorphLoaderInButtonDemo : UserControl
{
    private readonly DispatcherTimer _work = new() { Interval = TimeSpan.FromMilliseconds(1400) };
    private readonly DispatcherTimer _rest = new() { Interval = TimeSpan.FromMilliseconds(1600) };
    private int _attempt;
    private bool _busy;

    public MorphLoaderInButtonDemo()
    {
        InitializeComponent();
        _work.Tick += (_, _) => Finish();
        _rest.Tick += (_, _) => Reset();
        DetachedFromVisualTree += (_, _) =>
        {
            _work.Stop();
            _rest.Stop();
        };
    }

    private void OnUpload(object? sender, RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }
        _busy = true;
        _attempt++;
        Glyph.IsVisible = false;
        Loader.Status = MorphLoaderStatus.Loading;
        Loader.IsVisible = true;
        SetLabel("Uploading");
        _work.Start();
    }

    private void Finish()
    {
        _work.Stop();
        var ok = _attempt % 2 == 1;
        Loader.Status = ok ? MorphLoaderStatus.Success : MorphLoaderStatus.Error;
        SetLabel(ok ? "Uploaded" : "Upload failed");
        _rest.Start();
    }

    private void Reset()
    {
        _rest.Stop();
        Loader.IsVisible = false;
        Loader.Status = MorphLoaderStatus.Loading;
        Glyph.IsVisible = true;
        SetLabel("Upload report");
        _busy = false;
    }

    private void SetLabel(string text)
    {
        Label.Text = text;
        AutomationProperties.SetName(Upload, text);
    }

    private void OnSettled(object? sender, MorphLoaderSettledEventArgs e)
    {
        Log.Children.Add(new TextBlock
        {
            Text = $"Upload {_attempt}: settled on {e.Status}",
            FontSize = 13
        });
        if (Log.Children.Count > 5)
        {
            Log.Children.RemoveAt(1);
        }
    }
}
