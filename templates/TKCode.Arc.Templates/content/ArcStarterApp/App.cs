using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ArcStarterApp.Views;

namespace ArcStarterApp;

/// <summary>
/// The application. Arc needs nothing here: no base class, no App.axaml and no styles. UseArc(...) in Program.cs
/// registers the themes, and Arc's base theme styles the window and the Avalonia controls Arc's templates use.
/// </summary>
public class App : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
