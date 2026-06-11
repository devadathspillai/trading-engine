using TradingEngine.Models;

namespace TradingEngine;

public class PositionTracker
{
    // keyed by (participantId, symbol); positive = long, negative = short
    private readonly Dictionary<(string participantId, string symbol), decimal> _positions = [];

    public void Update(string participantId, string symbol, OrderSide side, decimal quantity)
    {
        var key = (participantId, symbol);
        _positions.TryGetValue(key, out decimal current);
        _positions[key] = side == OrderSide.Buy ? current + quantity : current - quantity;
    }

    public decimal GetPosition(string participantId, string symbol)
    {
        _positions.TryGetValue((participantId, symbol), out decimal position);
        return position;
    }

    public IReadOnlyDictionary<(string participantId, string symbol), decimal> All => _positions;
}
