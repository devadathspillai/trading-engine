using TradingEngine.Models;

namespace TradingEngine.Events;

public class OrderEventArgs : EventArgs
{
    public Order Order { get; }
    public string Reason { get; }

    public OrderEventArgs(Order order, string reason = "")
    {
        Order = order;
        Reason = reason;
    }
}
