using Avalonia.Automation;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SiteFooterPreviewDemo : UserControl
{
    public SiteFooterPreviewDemo()
    {
        InitializeComponent();
        Show(SiteFooterVariant.Columns);
    }

    private void OnLayoutChanged(object? sender, SelectionChangedEventArgs e)
    {
        // SelectedValue is applied while the XAML loads, before the named fields are assigned; the constructor shows the first layout.
        if (Frame is null || sender is not SegmentedControl layouts)
        {
            return;
        }

        Show((layouts.SelectedValue as string) switch
        {
            "minimal" => SiteFooterVariant.Minimal,
            "logo" => SiteFooterVariant.Logo,
            _ => SiteFooterVariant.Columns
        });
    }

    // The frame draws the border, so the footer drops its own top border (Arc: ".frame .footer { border-top: 0 }").
    private void Show(SiteFooterVariant variant)
    {
        var footer = new SiteFooter { Variant = variant, BorderThickness = new Avalonia.Thickness(0) };
        footer.Navigate += (_, args) =>
        {
            Note.Text = $"Opened {args.Link.Label}";
            AutomationProperties.SetName(Note, Note.Text);
        };
        Frame.Content = footer;
    }
}
