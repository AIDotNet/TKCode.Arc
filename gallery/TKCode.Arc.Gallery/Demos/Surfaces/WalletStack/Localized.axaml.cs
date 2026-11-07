using System.Globalization;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class WalletStackLocalizedDemo : UserControl
{
    public WalletStackLocalizedDemo()
    {
        InitializeComponent();
        Wallet.Culture = CultureInfo.GetCultureInfo("de-DE");
        Wallet.Labels = new WalletStackLabels
        {
            CountAfter = total => $" von {total}",
            AllCards = "Alle Karten",
            ValidThru = "Gültig bis",
            KeyboardHint = "Pfeiltasten wechseln die Karte, Eingabe öffnet sie, Escape legt sie zurück.",
            CardName = (card, position, total, balance) =>
                $"{card.Issuer} {card.Product}, endet auf {card.LastFour}, {position} von {total}, {card.BalanceLabel} {balance}",
            FrontAnnouncement = (card, position, total) => $"{card.Issuer} {card.Product} vorne, {position} von {total}",
            OpenAnnouncement = card => $"{card.Issuer} {card.Product} geöffnet",
            ActivityName = card => $"Umsätze {card.Issuer}",
            TransactionsName = card => $"Letzte Umsätze mit {card.Issuer}",
        };
    }
}
