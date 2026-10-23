namespace Suwageval.Cli.Output;

internal static class RunTagsText
{
    public static string Of(IReadOnlyList<string> tags) => tags.Count == 0 ? "-" : string.Join(", ", tags);
}
