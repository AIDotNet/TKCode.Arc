using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class IntegrationsFailingDemo : UserControl
{
    public IntegrationsFailingDemo()
    {
        InitializeComponent();
        IntegrationsDemoData.Fill(Directory);

        // A stand-in for the real authorization: Ledgerline refuses its first attempt, everything else goes through.
        Directory.ConnectAction = async (request, cancellation) =>
        {
            await Task.Delay(1400, cancellation);
            if (request.Item.Key == "ledgerline" && request.Attempt == 1)
            {
                return IntegrationConnectResult.Failed("Ledgerline did not grant access. Try again.");
            }
            return IntegrationConnectResult.Connected();
        };

        Directory.StatusChanged += (_, e) =>
            Log.Text = $"{e.Item.Name}: {e.OldStatus} → {e.NewStatus}" + (e.NewStatus == IntegrationStatus.Error ? $" ({e.Item.ErrorText})" : "");
    }
}
