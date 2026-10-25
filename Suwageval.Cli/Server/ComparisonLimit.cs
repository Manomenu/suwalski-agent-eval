namespace Suwageval.Cli.Server;

/// <summary>How many runs one comparison holds, which is also how many variants one sweep may have (its runs are compared).</summary>
internal static class ComparisonLimit
{
    /// <summary>The server's (<c>RunComparisonReport.MaxRuns</c>), the web's (<c>COMPARE_MAX_RUNS</c>, <c>PRINT_MAX_RUNS</c>); a server test compares them.</summary>
    public const int MaxRuns = 7;
}
