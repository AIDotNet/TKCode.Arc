using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Demos;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// The demo's sign-in page (uiarc.dev/demo/login): the full-screen <see cref="LoginSplit"/> block with the demo password,
/// under a floating "Demo account" card whose "Fill in" button types the demo address. Any well-formed address is
/// accepted and only the password is checked; single sign-on skips the password. When the block says the visitor is in,
/// the session starts and the overview opens.
/// </summary>
public sealed class DemoLoginView : Panel
{
    private const double FloatFrom = 760;

    private readonly LoginSplit _split;
    private readonly Border _hint;

    public DemoLoginView(Action<DemoAccount> signedIn)
    {
        _split = new LoginSplit
        {
            IsFullScreen = true,
            DemoPassword = DemoSession.DemoPassword,
            Photo = LoginSplitDemoData.CoastPhoto(),
            DefaultAccount = new LoginSplitAccount("Emma Collins", "emma@northwind.studio")
        };
        foreach (var name in new[] { "Emma Collins", "Marcus Johnson", "Ava Mitchell", "Jasmine Brooks" })
        {
            _split.Accounts.Add(new LoginSplitAccount(name, $"{name.Split(' ')[0].ToLowerInvariant()}@northstar.studio"));
        }
        _split.SignedIn += (_, e) => signedIn(new DemoAccount(e.Account.Name, e.Account.Email));
        Children.Add(_split);

        var fill = new Button { Variant = ButtonVariant.Secondary, Size = ControlSize.Small, Content = "Fill in", VerticalAlignment = VerticalAlignment.Center };
        fill.Click += (_, _) => _split.Email = DemoSession.DemoEmail;

        var credentials = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
        ArcTokens.Bind(credentials, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        credentials.Inlines!.Add(new Avalonia.Controls.Documents.Run(DemoSession.DemoEmail) { FontWeight = FontWeight.Medium });
        credentials.Inlines.Add(new Avalonia.Controls.Documents.Run(" with the password "));
        credentials.Inlines.Add(new Avalonia.Controls.Documents.Run(DemoSession.DemoPassword) { FontWeight = FontWeight.Medium });
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(DemoUi.Text("Demo account", ArcDesignTokenKind.Foreground, 14, FontWeight.Medium));
        text.Children.Add(credentials);

        var row = new DockPanel();
        DockPanel.SetDock(fill, Dock.Right);
        fill.Margin = new Thickness(12, 0, 0, 0);
        row.Children.Add(fill);
        row.Children.Add(text);
        _hint = new Border
        {
            Padding = new Thickness(14, 10, 10, 10),
            CornerRadius = new CornerRadius(18),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = row
        };
        ArcTokens.Bind(_hint, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(_hint, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(_hint, Border.BoxShadowProperty, ArcDesignTokenKind.ShadowRaised);
        Avalonia.Automation.AutomationProperties.SetName(_hint, "Demo account");
        Children.Add(_hint);
        SizeChanged += (_, e) => PlaceHint(e.NewSize.Width);
    }

    /// <summary>The block, so a sign-out can start it over.</summary>
    public LoginSplit Split => _split;

    /// <summary>
    /// Below 760 the card sits at the top; wider, it floats over the picture column (from 1100 the columns are 5 : 6).
    /// </summary>
    private void PlaceHint(double width)
    {
        if (width < FloatFrom)
        {
            _hint.Margin = new Thickness(12, 12, 12, 0);
            _hint.Width = double.NaN;
            _hint.HorizontalAlignment = HorizontalAlignment.Stretch;
            return;
        }
        _hint.HorizontalAlignment = HorizontalAlignment.Left;
        var (left, hintWidth) = width >= 1100
            ? (width * 5 / 11 + 32, Math.Min(384, width * 6 / 11 - 112))
            : (width / 2 + 20, Math.Min(352, width / 2 - 88));
        _hint.Margin = new Thickness(left, 20, 0, 0);
        _hint.Width = hintWidth;
    }
}
