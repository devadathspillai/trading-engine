namespace TradingEngine.Models;

public class Order
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Symbol { get; init; } = string.Empty;
    public OrderSide Side { get; init; }
    public OrderType Type { get; init; }
    public decimal Quantity { get; init; }
    public decimal? Price { get; init; }       // null for market orders
    public decimal? StopPrice { get; init; }   // trigger price for Stop and StopLimit orders
    public TimeInForce TimeInForce { get; init; } = TimeInForce.GTC;
    public string ParticipantId { get; init; } = string.Empty;
    public OrderStatus Status { get; set; } = OrderStatus.Open;
    public decimal RemainingQuantity { get; set; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    public Order(
        string symbol,
        OrderSide side,
        OrderType type,
        decimal quantity,
        decimal? price = null,
        decimal? stopPrice = null,
        TimeInForce timeInForce = TimeInForce.GTC,
        string participantId = "")
    {
        Symbol = symbol;
        Side = side;
        Type = type;
        Quantity = quantity;
        Price = price;
        StopPrice = stopPrice;
        TimeInForce = timeInForce;
        ParticipantId = participantId;
        RemainingQuantity = quantity;
    }
}
