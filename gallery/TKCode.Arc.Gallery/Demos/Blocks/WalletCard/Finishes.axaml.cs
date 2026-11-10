using Avalonia.Controls;
using System.Linq;

namespace TKCode.Arc.Gallery.Demos;

public partial class WalletCardFinishesDemo : UserControl
{
    public WalletCardFinishesDemo()
    {
        InitializeComponent();
        Ocean.Transactions.AddRange(WalletCardSamples.CreateTransactions(System.DateTimeOffset.Now).Take(3));
    }
}
