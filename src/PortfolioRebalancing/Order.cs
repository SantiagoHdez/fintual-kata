namespace PortfolioRebalancing;

/// <summary>An instruction to buy or sell some shares of a stock.</summary>
/// <param name="Stock">The stock to trade.</param>
/// <param name="Side">Whether to buy or sell.</param>
/// <param name="Quantity">The number of shares. Always positive.</param>
public sealed record Order(Stock Stock, Side Side, decimal Quantity);
