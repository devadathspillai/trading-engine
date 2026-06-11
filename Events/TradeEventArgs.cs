using TradingEngine.Models;

namespace TradingEngine.Events;

public class TradeEventArgs : EventArgs
{
    public Trade Trade { get; }

    public TradeEventArgs(Trade trade)
    {
        Trade = trade;
    }
}
