namespace Suwageval.Server.Monitoring.Conversations;

/// <summary>A row of <c>monitoring_check_results</c> with its check's key; <see cref="Evidence"/> is a JSON array of conversation items.</summary>
internal sealed record ConversationResultRow(
    string Key,
    int Revision,
    bool Passed,
    bool NotJudged,
    string Message,
    string Evidence,
    string? JudgeModel
);
