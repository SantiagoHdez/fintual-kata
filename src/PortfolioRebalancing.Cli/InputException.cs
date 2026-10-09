namespace PortfolioRebalancing.Cli;

/// <summary>The input cannot be rebalanced. The message says why and is meant to be shown to the user.</summary>
internal sealed class InputException : Exception
{
    public InputException(string message)
        : base(message)
    {
    }

    public InputException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
