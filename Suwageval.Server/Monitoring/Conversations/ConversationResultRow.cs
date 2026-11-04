namespace Suwageval.Server.Monitoring.Conversations;

internal sealed record ConversationResultRow(
    string Key,
    int Revision,
    bool Passed,
    bool NotJudged,
    string Message,
    string Evidence,
    string? JudgeModel
);
