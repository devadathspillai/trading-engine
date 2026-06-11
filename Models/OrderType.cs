namespace TradingEngine.Models;

public enum OrderType
{
    Market,
    Limit,
    Stop,
    StopLimit // stop triggers a Limit order instead of a Market order
}
