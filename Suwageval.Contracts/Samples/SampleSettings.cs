namespace Suwageval.Contracts.Samples;

public sealed record SampleSettings
{
    public const int Default = 3;

    public const int Min = 1;

    public const int Max = 20;

    public static int Effective(int? check, int? scenario, int? agent) => check ?? scenario ?? agent ?? Default;
}
