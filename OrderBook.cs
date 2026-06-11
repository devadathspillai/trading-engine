using TradingEngine.Models;

namespace TradingEngine;

public class OrderBook
{
    public string Symbol { get; }

    // bids sorted highest price first
    private readonly SortedDictionary<decimal, Queue<Order>> _bids =
        new(Comparer<decimal>.Create((a, b) => b.CompareTo(a)));

    // asks sorted lowest price first
    private readonly SortedDictionary<decimal, Queue<Order>> _asks =
        new(Comparer<decimal>.Create((a, b) => a.CompareTo(b)));

    private readonly List<Order> _stopOrders = [];

    // fast lookup by order ID for O(1) cancellation
    private readonly Dictionary<Guid, (Order order, bool isBid)> _orderIndex = [];

    public SortedDictionary<decimal, Queue<Order>> Bids => _bids;
    public SortedDictionary<decimal, Queue<Order>> Asks => _asks;
    public decimal? BestBid => _bids.Count > 0 ? _bids.Keys.First() : null;
    public decimal? BestAsk => _asks.Count > 0 ? _asks.Keys.First() : null;

    public OrderBook(string symbol)
    {
        Symbol = symbol;
    }

    public void AddLimitOrder(Order order)
    {
        var book = order.Side == OrderSide.Buy ? _bids : _asks;
        if (!book.TryGetValue(order.Price!.Value, out var queue))
        {
            queue = new Queue<Order>();
            book[order.Price.Value] = queue;
        }
        queue.Enqueue(order);
        _orderIndex[order.Id] = (order, order.Side == OrderSide.Buy);
    }

    public void AddStopOrder(Order order)
    {
        _stopOrders.Add(order);
    }

    public bool CancelOrder(Guid orderId)
    {
        if (!_orderIndex.TryGetValue(orderId, out var entry))
            return false;

        var (order, isBid) = entry;
        var book = isBid ? _bids : _asks;

        if (book.TryGetValue(order.Price!.Value, out var queue))
        {
            var remaining = queue.Where(o => o.Id != orderId).ToArray();
            queue.Clear();
            foreach (var o in remaining) queue.Enqueue(o);

            if (queue.Count == 0)
                book.Remove(order.Price.Value);
        }

        order.Status = OrderStatus.Cancelled;
        _orderIndex.Remove(orderId);
        return true;
    }

    public List<Order> TriggerStopOrders(decimal lastTradePrice)
    {
        var triggered = _stopOrders
            .Where(o => o.Side == OrderSide.Buy
                ? lastTradePrice >= o.StopPrice!.Value   // buy stop: fires when price rises to stop
                : lastTradePrice <= o.StopPrice!.Value)  // sell stop: fires when price falls to stop
            .ToList();

        foreach (var o in triggered)
            _stopOrders.Remove(o);

        return triggered;
    }

    public void PrintDepth(int levels = 5)
    {
        Console.WriteLine($"\n--- {Symbol} Order Book ---");

        var askLevels = _asks.Take(levels).Reverse().ToList();
        foreach (var (price, queue) in askLevels)
            Console.WriteLine($"  {queue.Sum(o => o.RemainingQuantity),8} @ {price,8:F2}  [ask]");

        decimal spread = BestAsk.HasValue && BestBid.HasValue ? BestAsk.Value - BestBid.Value : 0;
        Console.WriteLine($"  --- spread: {spread:F2} ---");

        foreach (var (price, queue) in _bids.Take(levels))
            Console.WriteLine($"  {queue.Sum(o => o.RemainingQuantity),8} @ {price,8:F2}  [bid]");

        Console.WriteLine();
    }
}
