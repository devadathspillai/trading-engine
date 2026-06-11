namespace TradingEngine.Models;

public enum TimeInForce
{
    GTC, // Good-Till-Cancelled: rests in the book until filled or cancelled
    IOC, // Immediate-Or-Cancel: fill what you can now, cancel the rest
    FOK  // Fill-Or-Kill: fill everything immediately or cancel the whole order
}
