using Suwageval.Contracts.Refusals;

namespace Suwageval.Server.Agents;

internal sealed record DeletionRefusal(RefusalCode Code, string Title, string Detail);
