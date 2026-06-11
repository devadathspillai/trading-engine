using TradingEngine.Events;
using TradingEngine.Models;

namespace TradingEngine;

public record SubmitResult(bool Accepted, List<Trade> Trades, string RejectReason = "");

public class Exchange
{
    private readonly Dictionary<string, OrderBook> _books = [];
    private readonly MatchingEngine _engine = new();
    private readonly RiskEngine _risk;
    private readonly PositionTracker _positions = new();
    private readonly MarketDataFeed _feed = new();

    // store all submitted orders so we can look them up when updating positions
    private readonly Dictionary<Guid, Order> _allOrders = [];

    public event EventHandler<TradeEventArgs>? TradeExecuted;
    public event EventHandler<OrderEventArgs>? OrderRejected;
    public event EventHandler<OrderEventArgs>? OrderCancelled;

    public Exchange(RiskEngine? risk = null)
    {
        _risk = risk ?? new RiskEngine();
    }

    public SubmitResult SubmitOrder(Order order)
    {
        _allOrders[order.Id] = order;
        var book = GetOrCreateBook(order.Symbol);

        // use last traded price as the reference for notional risk checks
        decimal? refPrice = _feed.GetMarketData(order.Symbol)?.LastPrice ?? order.Price;

        var riskResult = _risk.Validate(order, _positions, refPrice);
        if (!riskResult.Approved)
        {
            order.Status = OrderStatus.Rejected;
            OrderRejected?.Invoke(this, new OrderEventArgs(order, riskResult.Reason));
            return new SubmitResult(false, [], riskResult.Reason);
        }

        var trades = order.Type switch
        {
            OrderType.Market    => _engine.Match(order, book),
            OrderType.Limit     => _engine.Match(order, book),
            OrderType.Stop      => HandleStop(order, book),
            OrderType.StopLimit => HandleStop(order, book),
            _ => throw new ArgumentException($"Unknown order type: {order.Type}")
        };

        ProcessTrades(trades, book);
        return new SubmitResult(true, trades);
    }

    public bool CancelOrder(string symbol, Guid orderId)
    {
        if (!_books.TryGetValue(symbol, out var book))
            return false;

        bool cancelled = book.CancelOrder(orderId);
        if (cancelled && _allOrders.TryGetValue(orderId, out var order))
            OrderCancelled?.Invoke(this, new OrderEventArgs(order));

        return cancelled;
    }

    public OrderBook GetOrCreateBook(string symbol)
    {
        if (!_books.TryGetValue(symbol, out var book))
        {
            book = new OrderBook(symbol);
            _books[symbol] = book;
        }
        return book;
    }

    public MarketData? GetMarketData(string symbol) =>
        _feed.GetMarketData(symbol);

    public decimal GetPosition(string participantId, string symbol) =>
        _positions.GetPosition(participantId, symbol);

    private void ProcessTrades(List<Trade> trades, OrderBook book)
    {
        foreach (var trade in trades)
        {
            _feed.RecordTrade(trade.Symbol, trade.Price, trade.Quantity);
            UpdatePositions(trade);
            TradeExecuted?.Invoke(this, new TradeEventArgs(trade));
        }

        if (trades.Count == 0)
            return;

        // check whether any stop orders were triggered by the last trade price
        decimal lastPrice = trades[^1].Price;
        var triggered = book.TriggerStopOrders(lastPrice);

        foreach (var stop in triggered)
        {
            // stop: converts to market; stop-limit: converts to limit at the specified price
            var converted = stop.Type == OrderType.StopLimit
                ? new Order(stop.Symbol, stop.Side, OrderType.Limit,  stop.RemainingQuantity, price: stop.Price, participantId: stop.ParticipantId)
                : new Order(stop.Symbol, stop.Side, OrderType.Market, stop.RemainingQuantity, participantId: stop.ParticipantId);

            _allOrders[converted.Id] = converted;
            var stopTrades = _engine.Match(converted, book);
            ProcessTrades(stopTrades, book);
        }
    }

    private void UpdatePositions(Trade trade)
    {
        if (_allOrders.TryGetValue(trade.BuyOrderId, out var buy) && !string.IsNullOrEmpty(buy.ParticipantId))
            _positions.Update(buy.ParticipantId, trade.Symbol, OrderSide.Buy, trade.Quantity);

        if (_allOrders.TryGetValue(trade.SellOrderId, out var sell) && !string.IsNullOrEmpty(sell.ParticipantId))
            _positions.Update(sell.ParticipantId, trade.Symbol, OrderSide.Sell, trade.Quantity);
    }

    private List<Trade> HandleStop(Order order, OrderBook book)
    {
        book.AddStopOrder(order);
        return [];
    }
}
