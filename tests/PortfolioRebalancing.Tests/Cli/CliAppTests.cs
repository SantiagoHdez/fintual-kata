using PortfolioRebalancing.Cli;

namespace PortfolioRebalancing.Tests.Cli;

public class CliAppTests
{
    private const string SpecExample = """
        {
          "holdings": [{ "symbol": "META", "quantity": 10 }, { "symbol": "AAPL", "quantity": 5 }],
          "prices": { "META": 500, "AAPL": 200 },
          "allocation": { "META": 0.4, "AAPL": 0.6 }
        }
        """;

    private static (int ExitCode, string Output, string Error) Execute(string[] args, string stdin = "")
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = CliApp.Execute(args, new StringReader(stdin), output, error);

        return (exitCode, output.ToString(), error.ToString());
    }

    [Fact]
    public void Execute_ReadsFromStdinWhenThereIsNoArgument()
    {
        var (exitCode, output, error) = Execute([], SpecExample);

        Assert.Equal(0, exitCode);
        Assert.Equal(RebalanceRunner.Run(SpecExample) + Environment.NewLine, output);
        Assert.Empty(error);
    }

    [Fact]
    public void Execute_ReadsFromTheFileGivenAsTheArgument()
    {
        var path = Path.GetTempFileName();

        try
        {
            File.WriteAllText(path, SpecExample);

            var (exitCode, output, error) = Execute([path]);

            Assert.Equal(0, exitCode);
            Assert.Equal(RebalanceRunner.Run(SpecExample) + Environment.NewLine, output);
            Assert.Empty(error);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Execute_ReportsAMissingFileOnStderr()
    {
        var path = Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid() + ".json");

        var (exitCode, output, error) = Execute([path]);

        Assert.Equal(1, exitCode);
        Assert.Empty(output);
        Assert.StartsWith("Error:", error);
        Assert.Contains("does-not-exist-", error);
    }

    [Fact]
    public void Execute_ReportsInvalidInputOnStderrAndWritesNothingToStdout()
    {
        var (exitCode, output, error) = Execute([], "{ \"holdings\": [] }");

        Assert.Equal(1, exitCode);
        Assert.Empty(output);
        Assert.StartsWith("Error:", error);
    }

    [Fact]
    public void Execute_ShowsUsageAndFailsWithTooManyArguments()
    {
        var (exitCode, output, error) = Execute(["a.json", "b.json"]);

        Assert.Equal(2, exitCode);
        Assert.Empty(output);
        Assert.Contains("Usage:", error);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Execute_ShowsUsageOnStdoutWhenAskedForHelp(string flag)
    {
        var (exitCode, output, error) = Execute([flag]);

        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", output);
        Assert.Empty(error);
    }
}
