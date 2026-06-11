# Trading Engine

A limit order book and matching engine implemented in C# (.NET 10). Demonstrates the core mechanics of an electronic exchange: price-time priority matching, multiple order types, time-in-force policies, pre-trade risk controls, position tracking, and real-time market data aggregation.

> Built as a learning and portfolio project. Accurate in mechanics, not optimized for production throughput.

---

## Features

- **Four order types**: Market, Limit, Stop, Stop-Limit
- **Three time-in-force policies**: GTC, IOC, FOK
- **Price-time priority** matching with partial fills
- **Pre-trade risk engine**: configurable per-participant order size, notional, and position limits
- **Position tracker**: net long/short per participant and symbol
- **Market data feed**: OHLCV, VWAP, trade count per symbol, updated after every fill
- **Event-driven architecture**: subscribe to fills, rejections, and cancellations
- **Multi-symbol** support with independent order books

---

## Quick Start

```bash
git clone https://github.com/devadathspillai/trading-engine
cd trading-engine
dotnet run
```

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

---

## Architecture

```
                        ┌───────────────┐
  Order ───────────────►│   Exchange    │
                        └───────┬───────┘
                                │
           ┌────────────────────┼─────────────────────┐
           │                    │                      │
   ┌───────▼──────┐    ┌────────▼────────┐    ┌───────▼───────┐
   │  RiskEngine  │    │   OrderBook     │    │   Position    │
   │              │    │  (per symbol)   │    │   Tracker     │
   └──────────────┘    └────────┬────────┘    └───────────────┘
                                │
                       ┌────────▼────────┐
                       │    Matching     │
                       │    Engine       │
                       └────────┬────────┘
                                │
                       ┌────────▼────────┐
                       │  Market Data    │
                       │     Feed        │
                       └─────────────────┘
```

The `Exchange` is the single entry point. It runs risk checks, routes orders to the correct `OrderBook`, delegates matching to the `MatchingEngine`, then updates positions and market data from the resulting trades. C# events expose fills, rejections, and cancellations to any subscriber.

---

## Order Types

| Type | Behaviour |
|------|-----------|
| **Market** | Fills immediately at the best available price. Never rests in the book. |
| **Limit** | Fills at the specified price or better. Rests in the book if not immediately matched (GTC only). |
| **Stop** | Dormant until the last trade price reaches the stop price, then converts to a Market order. |
| **Stop-Limit** | Same trigger as Stop, but converts to a Limit order at the specified limit price rather than a Market order. |

---

## Time-in-Force

| Policy | Behaviour |
|--------|-----------|
| **GTC** | Good-Till-Cancelled. The order rests in the book until fully filled or explicitly cancelled. Default. |
| **IOC** | Immediate-Or-Cancel. Fill whatever quantity is available right now; cancel any unfilled remainder immediately. |
| **FOK** | Fill-Or-Kill. The entire order must be fillable immediately, otherwise the whole order is cancelled with no fills. |

---

## How Matching Works

The engine uses **price-time priority** (also called FIFO matching):

1. **Price priority**: the best-priced resting order matches first: highest bid, lowest ask.
2. **Time priority**: at the same price level, the earliest-arriving order is matched first (FIFO queue per level).

When an incoming order arrives:
- For a **buy**: walk up the ask side, consuming the lowest asks first.
- For a **sell**: walk down the bid side, consuming the highest bids first.
- The execution price is always the **resting order's price** (the passive side sets the price).

### Example: market order walking multiple levels

```
Ask book before:
  100 @ 150.10
  300 @ 150.20
  200 @ 150.30

Incoming: BUY 450 @ market

Execution:
  Fill 1: 100 @ 150.10  (first level exhausted)
  Fill 2: 300 @ 150.20  (second level exhausted)
  Fill 3:  50 @ 150.30  (partial fill of third level)

Ask book after:
  150 @ 150.30  (200 - 50 = 150 remaining)
```

### Example: stop-limit triggering on a breakout

```
Resting stop-limit: BUY 80, stop @ 500.00, limit @ 500.75
Ask book: 100 @ 500.00, 100 @ 500.50

Incoming: BUY 100 @ market
  Fill 1: 100 @ 500.00  -- last trade price now 500.00

Stop triggers (500.00 >= stop 500.00):
  Converted to: BUY 80 LIMIT @ 500.75
  Matches against 100 @ 500.50 (500.50 <= 500.75):
  Fill 2: 80 @ 500.50
```

---

## Risk Controls

Risk limits apply to identified participants (orders with a `ParticipantId`). Anonymous orders (market makers providing liquidity) are subject only to the order quantity limit.

| Check | Scope | Description |
|-------|-------|-------------|
| Max order quantity | All orders | Rejects any single order above a size threshold |
| Max notional value | Participant orders | Rejects orders where `quantity * reference price` exceeds the limit |
| Max position size | Participant orders | Rejects orders that would push a participant's net position beyond the configured limit |

The reference price for notional checks is the last traded price for the symbol.

---

## Project Structure

```
TradingEngine/
  Models/
    Order.cs              order with all fields and lifecycle state
    Trade.cs              a completed fill between two orders
    MarketData.cs         OHLCV and VWAP for one symbol
    OrderSide.cs          Buy | Sell
    OrderType.cs          Market | Limit | Stop | StopLimit
    OrderStatus.cs        Open | PartiallyFilled | Filled | Cancelled | Rejected
    TimeInForce.cs        GTC | IOC | FOK
  Events/
    OrderEventArgs.cs     event payload for order-level events
    TradeEventArgs.cs     event payload for fill events
  OrderBook.cs            sorted bid/ask data structure for one symbol
  MatchingEngine.cs       price-time priority matching; enforces IOC and FOK policies
  RiskEngine.cs           pre-trade checks: quantity, notional, and position limits
  PositionTracker.cs      net position per (participant, symbol)
  MarketDataFeed.cs       OHLCV and VWAP aggregation across all symbols
  Exchange.cs             top-level coordinator; exposes the public API and events
  Program.cs              runnable demo covering all order types and scenarios
```

---

## Subscribing to Events

```csharp
var exchange = new Exchange();

exchange.TradeExecuted  += (_, e) => Console.WriteLine($"Fill: {e.Trade}");
exchange.OrderRejected  += (_, e) => Console.WriteLine($"Rejected: {e.Reason}");
exchange.OrderCancelled += (_, e) => Console.WriteLine($"Cancelled: {e.Order.Id}");
```

---

## Extending

Some natural next steps:

- **Persistence**: replay an order log from a CSV or append-only event store
- **Networking**: expose a TCP or WebSocket endpoint to accept orders from external clients
- **Pro-rata matching**: allocate fills proportionally across all orders at a price level, rather than FIFO
- **Circuit breakers**: halt trading if price moves exceed a percentage threshold
- **Unit tests**: property-based testing works well for matching engine invariants; any sequence of orders should leave the book in a consistent state
