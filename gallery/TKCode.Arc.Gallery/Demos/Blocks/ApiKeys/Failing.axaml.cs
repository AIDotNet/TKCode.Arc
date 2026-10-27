using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ApiKeysFailingDemo : UserControl
{
    private bool _failedOnce;

    public ApiKeysFailingDemo()
    {
        InitializeComponent();
        Block.Keys.AddRange(ApiKeysDemoData.Keys());
        Block.Scopes.AddRange(ApiKeysDemoData.Scopes());

        // The first generate is refused after a second; every later one goes through.
        Block.GenerateAction = async (request, cancellation) =>
        {
            await Task.Delay(1000, cancellation);
            if (!_failedOnce)
            {
                _failedOnce = true;
                throw new InvalidOperationException("The key service is busy right now. Try again in a moment.");
            }
            return ApiKeysDemoData.Create(request);
        };

        // Revoking the idle ledger key fails: the row comes back and the error is announced.
        Block.RevokeAction = async (item, cancellation) =>
        {
            await Task.Delay(600, cancellation);
            if (item.Key == "ledger")
            {
                throw new InvalidOperationException("Ledger sync is used by a scheduled export. Pause the export first.");
            }
        };

        Block.KeyCreateFailed += (_, e) => Log.Text = $"Attempt {e.Request.Attempt} failed: {e.Exception.Message}";
        Block.KeyCreated += (_, e) => Log.Text = $"Created {e.Item.Name} on attempt {e.Request.Attempt}.";
        Block.KeyRevokeFailed += (_, e) => Log.Text = $"{e.Item.Name} is still active: {e.Exception.Message}";
        Block.KeyRevoked += (_, e) => Log.Text = $"Revoked {e.Item.Name}.";
    }
}
