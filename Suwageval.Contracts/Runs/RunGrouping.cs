namespace Suwageval.Contracts.Runs;

/// <summary>Which runs a list shows by whether they were started in a group (run --group): all of them, only those in no group, only those in any group.</summary>
public enum RunGrouping
{
    All,
    None,
    Any,
}
