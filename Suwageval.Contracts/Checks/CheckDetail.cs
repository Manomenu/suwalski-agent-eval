namespace Suwageval.Contracts.Checks;

public sealed record CheckDetail(int Revision, DateTimeOffset CreatedAt, CheckInput Check);
