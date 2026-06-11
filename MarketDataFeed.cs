using TradingEngine.Models;

namespace TradingEngine;

public class MarketDataFeed
{
    private readonly Dictionary<string, MarketData> _data = [];

    public void RecordTrade(string symbol, decimal price, decimal quantity)
    {
        if (!_data.TryGetValue(symbol, out var data))
        {
            data = new MarketData(symbol);
            _data[symbol] = data;
        }
        data.RecordTrade(price, quantity);
    }

    public MarketData? GetMarketData(string symbol)
    {
        _data.TryGetValue(symbol, out var data);
        return data;
    }
}
