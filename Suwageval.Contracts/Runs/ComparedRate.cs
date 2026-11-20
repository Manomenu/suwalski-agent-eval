namespace Suwageval.Contracts.Runs;

public sealed record ComparedRate(double? PassRate = null, RateChange? Change = null, int? ScenariosRan = null, int? ScenariosOfTag = null);
