namespace Suwageval.Contracts.Checks;

/// <summary>A check's latest revision.</summary>
public sealed record CheckDetail(int Revision, DateTimeOffset CreatedAt, CheckInput Check);
