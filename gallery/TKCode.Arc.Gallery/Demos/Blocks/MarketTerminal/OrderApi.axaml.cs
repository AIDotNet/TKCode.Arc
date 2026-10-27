using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MarketTerminalOrderApiDemo : UserControl
{
    private int _orders;

    public MarketTerminalOrderApiDemo()
    {
        InitializeComponent();
        MarketTerminalDemoData.Fill(Terminal);

        // A real app would call its broker here. The button stays busy while the task runs; an exception's message is
        // written under the button and the order is not placed.
        Terminal.PlaceOrderAction = async (request, cancellation) =>
        {
            await Task.Delay(800, cancellation);
            if (++_orders % 3 == 0)
            {
                throw new InvalidOperationException($"The broker turned down {request.Shares} {request.Ticker}: try a smaller order.");
            }
        };
        Terminal.CancelOrderAction = (_, cancellation) => Task.Delay(400, cancellation);

        Terminal.OrderPlaced += (_, e) => Log.Text = $"Placed: {e.Order.Side} {e.Order.Shares} {e.Order.Ticker} ({e.Order.Status})";
        Terminal.OrderFilled += (_, e) => Log.Text = $"Filled: {e.Order.Side} {e.Order.Shares} {e.Order.Ticker} at {e.Order.Price:0.00}";
        Terminal.OrderCanceled += (_, e) => Log.Text = $"Canceled: {e.Order.Side} {e.Order.Shares} {e.Order.Ticker}";
    }
}
