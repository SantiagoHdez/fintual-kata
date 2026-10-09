namespace PortfolioRebalancing.Cli;

/// <summary>The command line behavior, with its streams passed in so it can be tested.</summary>
internal static class CliApp
{
    private const string Usage = """
        Usage: PortfolioRebalancing.Cli [file.json]

        Reads a portfolio as JSON from the file, or from stdin when no file is given,
        and writes the orders that rebalance it as JSON to stdout.

        Input: { "holdings": [{ "symbol": "META", "quantity": 10 }],
                 "prices": { "META": 500 },
                 "allocation": { "META": 1 } }
        """;

    /// <returns>0 on success, 1 for invalid input or an unreadable file, 2 for wrong usage.</returns>
    public static int Execute(string[] args, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        if (args is ["-h"] or ["--help"])
        {
            stdout.WriteLine(Usage);
            return 0;
        }

        if (args.Length > 1)
        {
            stderr.WriteLine(Usage);
            return 2;
        }

        try
        {
            var json = args.Length == 1 ? File.ReadAllText(args[0]) : stdin.ReadToEnd();

            stdout.WriteLine(RebalanceRunner.Run(json));
            return 0;
        }
        catch (Exception exception) when (exception is InputException or IOException or UnauthorizedAccessException)
        {
            stderr.WriteLine($"Error: {exception.Message}");
            return 1;
        }
    }
}
