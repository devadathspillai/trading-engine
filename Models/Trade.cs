namespace TradingEngine.Models;

public class Trade
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Symbol { get; init; } = string.Empty;
    public Guid BuyOrderId { get; init; }
    public Guid SellOrderId { get; init; }
    public decimal Price { get; init; }
    public decimal Quantity { get; init; }
    public DateTime ExecutedAt { get; } = DateTime.UtcNow;

    public Trade(string symbol, Guid buyOrderId, Guid sellOrderId, decimal price, decimal quantity)
    {
        Symbol = symbol;
        BuyOrderId = buyOrderId;
        SellOrderId = sellOrderId;
        Price = price;
        Quantity = quantity;
    }

    public override string ToString() =>
        $"TRADE {Symbol} | {Quantity} @ {Price:F2} | buy={BuyOrderId.ToString()[..8]}... sell={SellOrderId.ToString()[..8]}...";
}
