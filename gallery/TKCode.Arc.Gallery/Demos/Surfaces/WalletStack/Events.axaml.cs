using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class WalletStackEventsDemo : UserControl
{
    private readonly List<string> _entries = new();

    public WalletStackEventsDemo()
    {
        InitializeComponent();
    }

    private void Write(string text)
    {
        _entries.Add(text);
        if (_entries.Count > 3)
        {
            _entries.RemoveAt(0);
        }
        Log.Text = string.Join("  ·  ", _entries);
    }

    private void OnPositionChanged(object? sender, WalletStackPositionChangedEventArgs e)
        => Write($"Front: {e.Card?.Issuer} ({e.OldPosition + 1} → {e.NewPosition + 1})");

    private void OnSelectedCardChanged(object? sender, WalletStackSelectedCardChangedEventArgs e)
        => Write(e.NewCard is { } card ? $"Opened {card.Issuer}" : $"Closed {e.OldCard?.Issuer}");

    private void OnPrevious(object? sender, RoutedEventArgs e) => Wallet.Previous();

    private void OnNext(object? sender, RoutedEventArgs e) => Wallet.Next();

    private void OnOpen(object? sender, RoutedEventArgs e) => Wallet.Open(Wallet.Position);

    private void OnClose(object? sender, RoutedEventArgs e) => Wallet.Close();
}
