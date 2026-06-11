using TradingEngine.Models;

namespace TradingEngine;

public class RiskEngine
{
    public decimal MaxOrderQuantity { get; init; } = 10_000;
    public decimal MaxOrderNotional { get; init; } = 1_000_000;

    // maximum net long or short position per participant per symbol
    public decimal MaxPositionSize { get; init; } = 50_000;

    public RiskCheckResult Validate(Order order, PositionTracker positions, decimal? referencePrice)
    {
        if (order.Quantity <= 0)
            return RiskCheckResult.Reject("Order quantity must be positive.");

        // quantity limit applies to all orders regardless of participant
        if (order.Quantity > MaxOrderQuantity)
            return RiskCheckResult.Reject(
                $"Order size {order.Quantity} exceeds maximum allowed {MaxOrderQuantity}.");

        // notional and position limits are per-participant; anonymous orders skip these checks
        if (string.IsNullOrEmpty(order.ParticipantId))
            return RiskCheckResult.Pass();

        if (referencePrice.HasValue)
        {
            decimal notional = order.Quantity * referencePrice.Value;
            if (notional > MaxOrderNotional)
                return RiskCheckResult.Reject(
                    $"Notional value {notional:F2} exceeds maximum allowed {MaxOrderNotional:F2}.");
        }

        decimal current = positions.GetPosition(order.ParticipantId, order.Symbol);
        decimal projected = order.Side == OrderSide.Buy
            ? current + order.Quantity
            : current - order.Quantity;

        if (Math.Abs(projected) > MaxPositionSize)
            return RiskCheckResult.Reject(
                $"Position limit breach: projected position {projected} would exceed max {MaxPositionSize}.");

        return RiskCheckResult.Pass();
    }
}

public record RiskCheckResult(bool Approved, string Reason)
{
    public static RiskCheckResult Pass() => new(true, string.Empty);
    public static RiskCheckResult Reject(string reason) => new(false, reason);
}
