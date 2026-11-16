using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.iOS;
using Avalonia.Layout;
using Avalonia.Threading;
using ObjCRuntime;
using TKCode.Arc.Mobile;
using UIKit;

namespace TKCode.Arc.TextEditMobileSample;

/// <summary>
/// Sends the system edit actions through the real responder chain to a <see cref="TextField"/> and prints
/// <c>TEXTEDIT-SAMPLE: PASS ...</c> or <c>TEXTEDIT-SAMPLE: FAIL ...</c>, then opens the system edit menu over the field
/// (<c>TEXTEDIT-SAMPLE: MENU</c>) and leaves it open for a screenshot.
/// </summary>
public sealed class App : Avalonia.Application
{
    private readonly TextField _field = new() { Text = "sk-abc123", PlaceholderText = "API key" };
    private readonly TextField _password = new() { Text = "hunter2", PasswordChar = '•', PlaceholderText = "Password" };

    public override void OnFrameworkInitializationCompleted()
    {
        var root = new StackPanel
        {
            Margin = new Thickness(16, 160, 16, 16),
            Spacing = 16,
            VerticalAlignment = VerticalAlignment.Top,
            Children = { _field, _password },
        };
        if (ApplicationLifetime is ISingleViewApplicationLifetime single)
        {
            single.MainView = root;
        }
        DispatcherTimer.RunOnce(Run, TimeSpan.FromSeconds(2));
        base.OnFrameworkInitializationCompleted();
    }

    private async void Run()
    {
        var failures = new List<string>();
        void Check(bool condition, string what)
        {
            if (!condition)
            {
                failures.Add(what);
            }
        }

        try
        {
            var app = UIApplication.SharedApplication;
            var view = (TopLevel.GetTopLevel(_field)?.TryGetPlatformHandle() as UIViewControlHandle)?.View;
            Check(view is not null, "no AvaloniaView");
            Check(TextEditMenu.Platform is not null, "TextEditMenu.Platform not set");

            _field.Focus();
            await Task.Delay(800);
            Check(ArcIosTextEditing.FocusedTextBox == _field, "focused text box not followed");

            // paste: through the responder chain, as the keyboard's paste suggestion sends it.
            UIPasteboard.General.String = "PASTED";
            _field.CaretIndex = _field.Text!.Length;
            var target = view?.GetTargetForAction(new Selector("paste:"), null);
            Check(target is ArcAppDelegate<App>, $"paste: target is {target?.GetType().Name ?? "null"}");
            Check(app.SendAction(new Selector("paste:"), null, null, null), "paste: not handled");
            Check(_field.Text == "sk-abc123PASTED", $"after paste: text is '{_field.Text}'");

            // copy: of a selection.
            _field.SelectionStart = 0;
            _field.SelectionEnd = 3;
            Check(app.SendAction(new Selector("copy:"), null, null, null), "copy: not handled");
            await Task.Delay(300);
            Check(UIPasteboard.General.String == "sk-", $"after copy: pasteboard is '{UIPasteboard.General.String}'");

            // A password field: no copy or cut, paste still allowed.
            _password.Focus();
            await Task.Delay(500);
            _password.SelectionStart = 0;
            _password.SelectionEnd = 3;
            Check(ArcIosTextEditing.CanPerform(new Selector("copy:")) == false, "copy: allowed in a password field");
            Check(ArcIosTextEditing.CanPerform(new Selector("cut:")) == false, "cut: allowed in a password field");
            Check(ArcIosTextEditing.CanPerform(new Selector("paste:")) == true, "paste: refused in a password field");
            Check(ArcIosTextEditing.CanPerform(new Selector("delete:")) is null, "other selectors not passed on");
        }
        catch (Exception e)
        {
            failures.Add(e.GetType().Name + ": " + e.Message);
        }

        Console.WriteLine(failures.Count == 0 ? "TEXTEDIT-SAMPLE: PASS" : "TEXTEDIT-SAMPLE: FAIL " + string.Join("; ", failures));

        // The system edit menu over the API key field, as a long press opens it.
        _field.Focus();
        await Task.Delay(500);
        _field.SelectionStart = 3;
        _field.SelectionEnd = 9;
        _field.RaiseEvent(new ContextRequestedEventArgs());
        Console.WriteLine("TEXTEDIT-SAMPLE: MENU");
    }
}
