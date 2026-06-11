using TradingEngine;
using TradingEngine.Models;

var risk = new RiskEngine
{
    MaxOrderQuantity = 5_000,
    MaxOrderNotional = 750_000m,
    MaxPositionSize  = 10_000
};

var exchange = new Exchange(risk);

exchange.TradeExecuted  += (_, e) => Console.WriteLine($"  [FILL]   {e.Trade}");
exchange.OrderRejected  += (_, e) => Console.WriteLine($"  [REJECT] {e.Order.Side} {e.Order.Quantity} {e.Order.Type} -- {e.Reason}");
exchange.OrderCancelled += (_, e) => Console.WriteLine($"  [CANCEL] {e.Order.Side} {e.Order.RemainingQuantity} remaining {e.Order.Type}");

static void Section(string title)
{
    Console.WriteLine($"\n{new string('=', 62)}");
    Console.WriteLine($"  {title}");
    Console.WriteLine(new string('=', 62));
}


// --- AAPL scenarios 1-5 ---

Section("1. Building the AAPL order book");

exchange.SubmitOrder(new Order("AAPL", OrderSide.Sell, OrderType.Limit, 100, price: 150.10m));
exchange.SubmitOrder(new Order("AAPL", OrderSide.Sell, OrderType.Limit, 300, price: 150.20m));
exchange.SubmitOrder(new Order("AAPL", OrderSide.Sell, OrderType.Limit, 200, price: 150.30m));
exchange.SubmitOrder(new Order("AAPL", OrderSide.Buy,  OrderType.Limit, 200, price: 149.90m));
exchange.SubmitOrder(new Order("AAPL", OrderSide.Buy,  OrderType.Limit, 500, price: 149.80m));
exchange.SubmitOrder(new Order("AAPL", OrderSide.Buy,  OrderType.Limit, 150, price: 149.70m));

Console.WriteLine("No fills -- all orders rest in the book.");
exchange.GetOrCreateBook("AAPL").PrintDepth();


Section("2. Limit order crossing the spread");
Console.WriteLine("Buy 150 @ 150.10: 100 available at that ask -- 100 fills, 50 rests as a new bid.");

exchange.SubmitOrder(new Order("AAPL", OrderSide.Buy, OrderType.Limit, 150, price: 150.10m));
exchange.GetOrCreateBook("AAPL").PrintDepth();


Section("3. Market order walking multiple price levels");
Console.WriteLine("Buy 400 @ market: eats all 300 @ 150.20 then 100 of the 200 @ 150.30.");

exchange.SubmitOrder(new Order("AAPL", OrderSide.Buy, OrderType.Market, 400));
exchange.GetOrCreateBook("AAPL").PrintDepth();


Section("4. IOC (Immediate-Or-Cancel)");

exchange.SubmitOrder(new Order("AAPL", OrderSide.Sell, OrderType.Limit, 80, price: 150.40m));
Console.WriteLine("Buy 200 IOC @ 150.40: only 180 shares available at or below 150.40");
Console.WriteLine("(100 @ 150.30 + 80 @ 150.40). Fills 180, cancels the remaining 20.");

var iocOrder = new Order("AAPL", OrderSide.Buy, OrderType.Limit, 200,
    price: 150.40m, timeInForce: TimeInForce.IOC);
exchange.SubmitOrder(iocOrder);
Console.WriteLine($"  IOC status: {iocOrder.Status}  remaining: {iocOrder.RemainingQuantity}");
exchange.GetOrCreateBook("AAPL").PrintDepth();


Section("5a. FOK (Fill-Or-Kill) -- rejected: not enough liquidity");

exchange.SubmitOrder(new Order("AAPL", OrderSide.Sell, OrderType.Limit, 50, price: 150.50m));
Console.WriteLine("Buy 300 FOK @ 150.50: only 50 available. Entire order cancelled, no fills.");

var fokFail = new Order("AAPL", OrderSide.Buy, OrderType.Limit, 300,
    price: 150.50m, timeInForce: TimeInForce.FOK);
exchange.SubmitOrder(fokFail);
Console.WriteLine($"  FOK status: {fokFail.Status}  (book unchanged)");


Section("5b. FOK -- accepted: enough liquidity");

exchange.SubmitOrder(new Order("AAPL", OrderSide.Sell, OrderType.Limit, 400, price: 150.50m));
Console.WriteLine("Buy 300 FOK @ 150.50: 450 shares now available. Fills in full.");

var fokPass = new Order("AAPL", OrderSide.Buy, OrderType.Limit, 300,
    price: 150.50m, timeInForce: TimeInForce.FOK);
exchange.SubmitOrder(fokPass);
Console.WriteLine($"  FOK status: {fokPass.Status}");


// --- NVDA -- clean book, stop-limit scenario ---

Section("6. Stop-Limit order (NVDA -- fresh book)");

exchange.SubmitOrder(new Order("NVDA", OrderSide.Sell, OrderType.Limit, 100, price: 500.00m));
exchange.SubmitOrder(new Order("NVDA", OrderSide.Sell, OrderType.Limit, 100, price: 500.50m));
exchange.GetOrCreateBook("NVDA").PrintDepth();

Console.WriteLine("Place buy stop-limit: stop @ 500.00, limit @ 500.75, qty 80.");
Console.WriteLine("Meaning: 'enter a long if price breaks out above 500.00, limit to pay max 500.75.'");

exchange.SubmitOrder(new Order("NVDA", OrderSide.Buy, OrderType.StopLimit, 80,
    price: 500.75m, stopPrice: 500.00m));

Console.WriteLine("\nMarket buy 100 fills @ 500.00 -- last trade price hits the stop trigger.");
Console.WriteLine("Expected: fill @ 500.00, stop converts to buy limit @ 500.75,");
Console.WriteLine("          that limit immediately crosses the 500.50 ask -- second fill.");

exchange.SubmitOrder(new Order("NVDA", OrderSide.Buy, OrderType.Market, 100));
exchange.GetOrCreateBook("NVDA").PrintDepth();


// --- Risk checks ---

Section($"7. Pre-trade risk checks  (max qty={risk.MaxOrderQuantity}  max notional={risk.MaxOrderNotional:F0}  max pos={risk.MaxPositionSize})");

Console.WriteLine("\n7a. Order quantity exceeds maximum (6000 > 5000):");
exchange.SubmitOrder(new Order("AAPL", OrderSide.Buy, OrderType.Market, 6_000));

// ref price is last AAPL trade (150.50 after scenario 5b)
// participantId is required for the notional check to apply
Console.WriteLine("\n7b. Notional too large -- FIRM_A buys 4984 shares @ ref 150.50 = 750,092 > 750,000:");
exchange.SubmitOrder(new Order("AAPL", OrderSide.Buy, OrderType.Market, 4_984, participantId: "FIRM_A"));

Console.WriteLine("\n7c. Position limit breach for FUND1:");
// anonymous sells (no participantId) bypass notional limits -- they are market-maker liquidity
exchange.SubmitOrder(new Order("AAPL", OrderSide.Sell, OrderType.Limit, 5_000, price: 150.60m));
exchange.SubmitOrder(new Order("AAPL", OrderSide.Sell, OrderType.Limit, 5_000, price: 150.70m));

Console.WriteLine("  Buy 4000 (position 0 -> 4000, within 10000 limit):");
exchange.SubmitOrder(new Order("AAPL", OrderSide.Buy, OrderType.Market, 4_000, participantId: "FUND1"));
Console.WriteLine($"  FUND1 AAPL position: {exchange.GetPosition("FUND1", "AAPL")}");

Console.WriteLine("  Buy 4000 more (position 4000 -> 8000, within limit):");
exchange.SubmitOrder(new Order("AAPL", OrderSide.Buy, OrderType.Market, 4_000, participantId: "FUND1"));
Console.WriteLine($"  FUND1 AAPL position: {exchange.GetPosition("FUND1", "AAPL")}");

Console.WriteLine("  Buy 3000 more (would reach 11000 -- breaches 10000 limit, rejected):");
exchange.SubmitOrder(new Order("AAPL", OrderSide.Buy, OrderType.Market, 3_000, participantId: "FUND1"));
Console.WriteLine($"  FUND1 AAPL position unchanged: {exchange.GetPosition("FUND1", "AAPL")}");


// --- Summary ---

Section("8. Market data and positions");

foreach (var sym in new[] { "AAPL", "NVDA" })
{
    var md = exchange.GetMarketData(sym);
    if (md is not null) Console.WriteLine($"  {md}");
}

Console.WriteLine($"\n  FUND1 net position -- AAPL: {exchange.GetPosition("FUND1", "AAPL")} shares");
