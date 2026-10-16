namespace Suwageval.Server.Judges;

/// <summary>
/// What the model must answer when judging <c>$judge</c> values, as JSON: exactly one entry per path it was given. The schema the
/// model is asked to follow, read strictly (<see cref="ArgumentJudge"/> also checks the paths).
/// </summary>
internal sealed record ArgumentAnswers(IReadOnlyList<ArgumentAnswers.Entry> Results)
{
    internal sealed record Entry(string Path, bool Correct, string Reason);
}
