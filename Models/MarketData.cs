namespace TradingEngine.Models;

public class MarketData
{
    public string Symbol { get; }
    public decimal? Open { get; private set; }
    public decimal High { get; private set; }
    public decimal Low { get; private set; } = decimal.MaxValue;
    public decimal LastPrice { get; private set; }
    public decimal Volume { get; private set; }
    public int TradeCount { get; private set; }

    // VWAP = sum(price * quantity) / sum(quantity) over all trades today
    private decimal _vwapNumerator;
    public decimal? Vwap => Volume > 0 ? Math.Round(_vwapNumerator / Volume, 4) : null;

    public MarketData(string symbol)
    {
        Symbol = symbol;
    }

    public void RecordTrade(decimal price, decimal quantity)
    {
        Open ??= price;
        High = Math.Max(High, price);
        Low = Math.Min(Low, price);
        LastPrice = price;
        Volume += quantity;
        TradeCount++;
        _vwapNumerator += price * quantity;
    }

    public override string ToString() =>
        $"{Symbol} | last={LastPrice:F2} O={Open:F2} H={High:F2} L={Low:F2} " +
        $"vol={Volume} trades={TradeCount} vwap={Vwap:F4}";
}
