using TradingEngine.Models;

namespace TradingEngine;

public class MatchingEngine
{
    public List<Trade> Match(Order incoming, OrderBook book)
    {
        // FOK: verify the full quantity can be filled before touching the book
        if (incoming.TimeInForce == TimeInForce.FOK && !CanFillFully(incoming, book))
        {
            incoming.Status = OrderStatus.Cancelled;
            return [];
        }

        var trades = ExecuteMatch(incoming, book);

        if (incoming.RemainingQuantity > 0)
        {
            if (incoming.TimeInForce == TimeInForce.IOC)
            {
                // cancel whatever wasn't filled immediately
                incoming.Status = OrderStatus.Cancelled;
            }
            else if (incoming.Type == OrderType.Limit && incoming.TimeInForce == TimeInForce.GTC)
            {
                // limit GTC orders rest in the book at their price level
                book.AddLimitOrder(incoming);
            }
            // market orders that can't fill (empty book) simply expire
        }

        return trades;
    }

    private List<Trade> ExecuteMatch(Order incoming, OrderBook book)
    {
        var trades = new List<Trade>();
        var opposingSide = incoming.Side == OrderSide.Buy ? book.Asks : book.Bids;

        while (incoming.RemainingQuantity > 0 && opposingSide.Count > 0)
        {
            decimal bestPrice = opposingSide.Keys.First();

            if (!PricesCross(incoming, bestPrice))
                break;

            var queue = opposingSide[bestPrice];

            while (incoming.RemainingQuantity > 0 && queue.Count > 0)
            {
                var resting = queue.Peek();
                decimal fillQty = Math.Min(incoming.RemainingQuantity, resting.RemainingQuantity);

                // trade price is always the resting order's price (maker sets the price)
                trades.Add(new Trade(
                    book.Symbol,
                    buyOrderId:  incoming.Side == OrderSide.Buy ? incoming.Id : resting.Id,
                    sellOrderId: incoming.Side == OrderSide.Buy ? resting.Id  : incoming.Id,
                    price: bestPrice,
                    quantity: fillQty
                ));

                incoming.RemainingQuantity -= fillQty;
                resting.RemainingQuantity  -= fillQty;

                SetStatus(incoming);
                SetStatus(resting);

                if (resting.RemainingQuantity == 0)
                    queue.Dequeue();
            }

            if (queue.Count == 0)
                opposingSide.Remove(bestPrice);
        }

        return trades;
    }

    // checks whether available liquidity at valid prices can satisfy the full order quantity
    private bool CanFillFully(Order order, OrderBook book)
    {
        var opposingSide = order.Side == OrderSide.Buy ? book.Asks : book.Bids;
        decimal available = 0;

        foreach (var (price, queue) in opposingSide)
        {
            if (!PricesCross(order, price))
                break;

            available += queue.Sum(o => o.RemainingQuantity);

            if (available >= order.Quantity)
                return true;
        }

        return false;
    }

    private static bool PricesCross(Order incoming, decimal restingPrice)
    {
        if (incoming.Type == OrderType.Market)
            return true;

        return incoming.Side == OrderSide.Buy
            ? restingPrice <= incoming.Price!.Value  // buy: only match asks at or below our limit
            : restingPrice >= incoming.Price!.Value; // sell: only match bids at or above our limit
    }

    private static void SetStatus(Order order)
    {
        order.Status = order.RemainingQuantity == 0
            ? OrderStatus.Filled
            : OrderStatus.PartiallyFilled;
    }
}
