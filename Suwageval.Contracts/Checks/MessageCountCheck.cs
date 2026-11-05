namespace Suwageval.Contracts.Checks;

/// <summary>On how many messages are in scope: at least one bound is given, all given must hold.</summary>
public sealed record MessageCountCheck : CheckInput
{
    /// <summary>Only messages of this role count; not set: both a user's and an assistant's.</summary>
    public MessageRole? Role { get; init; }

    /// <summary>At least this many, from 1: at least 0 always holds.</summary>
    public int? Min { get; init; }

    /// <summary>At most this many, from 0.</summary>
    public int? Max { get; init; }

    /// <summary>Exactly this many, from 0; stands alone, without <see cref="Min"/> and <see cref="Max"/>.</summary>
    public int? Exactly { get; init; }
}
