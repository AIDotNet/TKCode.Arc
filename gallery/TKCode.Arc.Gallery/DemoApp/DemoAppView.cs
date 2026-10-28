using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Animation;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// The Northstar dashboard demo, a copy of uiarc.dev/demo, at <c>/demo/&lt;page&gt;</c>: the signed-in app
/// (<see cref="DemoFrame"/>) or the sign-in page. <c>/demo</c> opens the overview, or the sign-in page after a sign-out;
/// an app page opens the sign-in page while nobody is signed in, and the sign-in page opens the overview while someone is.
/// Pages navigate through the gallery shell, so the address bar and Back follow the demo.
/// </summary>
public sealed class DemoAppView : UserControl
{
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(240);

    private DemoFrame? _frame;
    private DemoLoginView? _login;

    public DemoAppView(GalleryShell shell)
    {
        Shell = shell;
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.Background);
        Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = FadeDuration }];
    }

    public GalleryShell Shell { get; }

    /// <summary>The app, once a page of it has been shown.</summary>
    public DemoFrame? Frame => _frame;

    /// <summary>The page an address leads to, given who is signed in.</summary>
    public DemoPage Resolve(string? slug)
    {
        var page = DemoPages.Find(slug);
        if (!DemoSession.IsSignedIn)
        {
            return DemoPages.Login;
        }
        return page is null || page == DemoPages.Login ? DemoPages.Overview : page;
    }

    /// <summary>Shows <paramref name="page"/> (already resolved).</summary>
    public void Show(DemoPage page)
    {
        Opacity = 1;
        if (page == DemoPages.Login)
        {
            _login ??= new DemoLoginView(OnSignedIn);
            Content = _login;
            return;
        }
        _frame ??= new DemoFrame(this);
        Content = _frame;
        _frame.Show(page);
    }

    /// <summary>Opens the page <paramref name="slug"/> through the shell, so the address follows.</summary>
    public void Open(string slug) => Shell.Navigate(GalleryRoute.Demo(slug));

    /// <summary>The app fades out, the session ends and the sign-in page opens, starting over.</summary>
    public async Task SignOutAsync()
    {
        Opacity = 0;
        await Task.Delay(FadeDuration);
        DemoSession.SignOut();
        _login?.Split.Reset();
        Open(DemoPages.LoginSlug);
    }

    private async void OnSignedIn(DemoAccount account)
    {
        Opacity = 0;
        await Task.Delay(FadeDuration);
        DemoSession.SignIn(account);
        Open(DemoPages.OverviewSlug);
    }
}
