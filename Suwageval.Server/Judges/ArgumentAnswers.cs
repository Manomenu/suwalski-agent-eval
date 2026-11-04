namespace Suwageval.Server.Judges;

internal sealed record ArgumentAnswers(IReadOnlyList<ArgumentAnswers.Entry> Results)
{
    internal sealed record Entry(string Path, bool Correct, string Reason);
}
