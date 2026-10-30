using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Whole suggestions from a task, as from a completion endpoint that does not stream.</summary>
public partial class GhostTextSingleLineDemo : UserControl
{
    public GhostTextSingleLineDemo()
    {
        InitializeComponent();
        Field.Suggest = (text, cancellationToken) => Complete(text, cancellationToken);
    }

    private static async Task<string?> Complete(string text, CancellationToken cancellationToken)
    {
        await Task.Delay(400, cancellationToken);
        foreach (var message in Messages)
        {
            if (message.Length > text.Length && message.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            {
                return message[text.Length..];
            }
        }
        return text.EndsWith(' ') ? "and update the tests" : " and update the tests";
    }

    private static readonly string[] Messages =
    [
        "Fix flaky checkout test by waiting for the webhook",
        "Add backoff to the payment retry helper",
        "Remove the sleep that hid the payment race",
    ];
}
