namespace Suwageval.Contracts.Checks;

public sealed record MessageCountCheck : CheckInput
{
    public MessageRole? Role { get; init; }

    public int? Min { get; init; }

    public int? Max { get; init; }

    public int? Exactly { get; init; }
}
