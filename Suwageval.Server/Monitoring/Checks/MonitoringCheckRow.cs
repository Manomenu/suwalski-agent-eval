namespace Suwageval.Server.Monitoring.Checks;

/// <summary>A live monitoring check with its latest revision (<see cref="Content"/>: the check as pushed, JSON) and its rates.</summary>
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
