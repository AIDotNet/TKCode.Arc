using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>An app-fed stack: events declared in XAML, and Push for one that just happened.</summary>
public partial class SocialProofToastYourEventsDemo : UserControl
{
    private static readonly (string Name, string Action, string Meta, SocialProofActor Actor)[] s_purchases =
    [
        ("Amara", "bought the Pro plan", "Nairobi", SocialProofActor.Person),
        ("Harbor & Pine", "upgraded to Business", "26 seats", SocialProofActor.Company),
        ("Kenji", "switched to yearly billing", "Osaka", SocialProofActor.Person),
        ("Northwind AI", "added 10 seats", "Business plan", SocialProofActor.Company),
    ];

    private int _count;

    public SocialProofToastYourEventsDemo()
    {
        InitializeComponent();
    }

    private void OnPush(object? sender, RoutedEventArgs e)
    {
        var (name, action, meta, actor) = s_purchases[_count % s_purchases.Length];
        _count++;
        Stack.Push(new SocialProofEvent("push-" + _count, name, action)
        {
            Meta = meta,
            Actor = actor,
            Time = DateTimeOffset.Now,
            VerifiedLabel = "Verified purchase",
        });
    }
}
