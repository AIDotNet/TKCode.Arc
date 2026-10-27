using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ApiKeysEventLogDemo : UserControl
{
    private readonly List<string> _lines = new();

    public ApiKeysEventLogDemo()
    {
        InitializeComponent();
        Block.Keys.AddRange(ApiKeysDemoData.Keys());
        Block.Scopes.AddRange(ApiKeysDemoData.Scopes());

        // A server that takes 900ms; every event and the bound state are written below the block.
        Block.GenerateAction = async (request, cancellation) =>
        {
            Write($"GenerateAction: \"{request.Name}\", {request.Environment}, {string.Join(", ", request.Scopes)}");
            await Task.Delay(900, cancellation);
            return ApiKeysDemoData.Create(request);
        };
        Block.RevokeAction = async (item, cancellation) =>
        {
            Write($"RevokeAction: {item.Name}");
            await Task.Delay(400, cancellation);
        };

        Block.PanelStateChanged += (_, e) =>
            Write(e.CloseReason is { } reason ? $"Panel: {e.OldState} → {e.NewState} ({reason})" : $"Panel: {e.OldState} → {e.NewState}");
        Block.KeyCreated += (_, e) => Write($"KeyCreated: {e.Item.Name} ({e.Item.MaskedText})");
        Block.SecretCopied += (_, e) => Write($"SecretCopied: {e.Item.Name}");
        Block.ExpandedKeyChanged += (_, e) => Write($"ExpandedKeyChanged: {e.OldItem?.Name ?? "none"} → {e.NewItem?.Name ?? "none"}");
        Block.KeyRevoking += (_, e) => Write($"KeyRevoking: {e.Item.Name}");
        Block.KeyRevoked += (_, e) => Write($"KeyRevoked: {e.Item.Name} (was row {e.Index + 1})");

        Block.PropertyChanged += (_, e) =>
        {
            if (e.Property == ApiKeys.PanelStateProperty || e.Property == ApiKeys.ExpandedKeyProperty
                || e.Property == ApiKeys.ActiveCountProperty)
            {
                ShowState();
            }
        };
        ShowState();
    }

    private void ShowState()
    {
        State.Text = $"PanelState: {Block.PanelState}";
        Expanded.Text = $"ExpandedKey: {Block.ExpandedKey?.Name ?? "none"}";
        Count.Text = $"ActiveCount: {Block.ActiveCount}";
    }

    private void Write(string line)
    {
        _lines.Insert(0, line);
        if (_lines.Count > 8)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        Log.Text = string.Join(Environment.NewLine, _lines);
    }
}
