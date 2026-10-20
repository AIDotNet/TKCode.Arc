using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace TKCode.Arc.Gallery.Pages;

public static class AccordionPage
{
    private sealed record Faq(string Question, string Answer);

    private static readonly Faq[] Questions =
    [
        new("What is included in the free plan?", "Unlimited projects, three collaborators and a week of version history. Upgrade any time to add more people and keep history for a year."),
        new("Can I change plans later?", "Yes. Plan changes apply immediately and the difference is prorated on your next invoice."),
        new("How do refunds work?", "Annual plans can be refunded within 30 days. Monthly plans can be cancelled at any time and stay active until the end of the period."),
        new("Do you offer discounts for nonprofits?", "Registered nonprofits and schools get 50% off every paid plan. Contact support with your registration details.")
    ];

    public static Control Create()
    {
        var basic = new Accordion();
        foreach (var faq in Questions)
        {
            basic.Items.Add(new AccordionItem { Header = faq.Question, Content = faq.Answer });
        }

        var large = new Accordion
        {
            Size = AccordionSize.Large,
            ExpandedIndex = -1,
            ItemsSource = Questions,
            ItemTemplate = new FuncDataTemplate<Faq>((f, _) => new TextBlock { Text = f.Question, TextWrapping = Avalonia.Media.TextWrapping.Wrap }),
            ContentTemplate = new FuncDataTemplate<Faq>((f, _) => new TextBlock { Text = f.Answer, TextWrapping = Avalonia.Media.TextWrapping.Wrap })
        };

        var multiple = new Accordion { ExpandMode = AccordionExpandMode.Multiple };
        foreach (var faq in Questions[..3])
        {
            multiple.Items.Add(new AccordionItem { Header = faq.Question, Content = faq.Answer });
        }

        return GalleryUi.Column(20,
            GalleryUi.Card("Accordion", "One row open at a time; clicking the open row closes it. The height glides on a spring that never overshoots and the answer settles down out of a soft blur. Arrow keys move between rows, Home/End jump, Enter or Space toggle.", basic),
            GalleryUi.Card("Large", "Page-level FAQs: questions at the large text size, answers at body size. Starts closed (ExpandedIndex = -1); built from ItemsSource.", large),
            GalleryUi.Card("Multiple", "ExpandMode = Multiple lets rows open independently (an addition to Arc).", multiple));
    }
}
