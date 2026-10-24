using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class LoginLoginSplitEventsDemo : UserControl
{
    private readonly List<string> _lines = new();

    public LoginLoginSplitEventsDemo()
    {
        InitializeComponent();
        Split.Accounts.AddRange(LoginSplitDemoData.Team());
        Split.StepChanged += (_, e) => Add($"StepChanged    {e.OldStep} → {e.NewStep}");
        Split.SignedIn += (_, e) => Add($"SignedIn       {e.Account.Name} <{e.Account.Email}> with {e.Provider?.Name ?? e.Method.ToString()}");
        Split.SignedOut += (_, e) => Add($"SignedOut      {e.Account.Email}");
        Split.ResetLinkSent += (_, e) => Add($"ResetLinkSent  {e.Account.Email}{(e.IsResend ? " (again)" : "")}");
        Split.Navigate += (_, e) =>
        {
            // Handled: the block neither opens an address nor shows its "in your app" status.
            e.Handled = true;
            Add($"Navigate       {e.Link}");
        };
    }

    private void Add(string line)
    {
        _lines.Insert(0, line);
        if (_lines.Count > 7)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        Log.Text = string.Join(Environment.NewLine, _lines);
    }
}
