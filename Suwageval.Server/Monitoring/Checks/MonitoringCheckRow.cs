namespace Suwageval.Server.Monitoring.Checks;

internal sealed record MonitoringCheckRow(
    Guid Id,
    string Key,
    int Revision,
    string Content,
    DateTime CreatedAt,
    int Passed7,
    int Checked7,
    int Passed30,
    int Checked30
);
