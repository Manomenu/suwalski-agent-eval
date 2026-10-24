using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Suwageval.AgentSdk.Agents;
using Suwageval.AgentSdk.Conversations;
using Suwageval.AgentSdk.Monitoring;
using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Monitoring;
using Suwageval.Server.Agents;
using Suwageval.Server.Problems;

namespace Suwageval.Server.Monitoring.Conversations;

/// <summary>The routes of monitored conversations: a deployed agent sends its turns; people and the CLI read the conversations.</summary>
internal static class ConversationEndpoints
{
    private const int DefaultPageSize = 20;

    public const int MaxPageSize = 100;

    /// <summary>How many turns one request may carry.</summary>
    public const int MaxTurns = 500;

    /// <summary>How long an agent's id of a conversation may be.</summary>
    public const int MaxConversationIdLength = 200;

    public static IEndpointRouteBuilder MapConversations(this IEndpointRouteBuilder app)
    {
        app.MapPost("/turns", TakeTurnsAsync).Produces<TurnBatchAnswer>();
        app.MapGet("/agents/{agent}/conversations", ListAsync).Produces<ConversationList>();
        app.MapGet("/conversations/{id:guid}", GetAsync).Produces<ConversationDetail>();
        return app;
    }

    /// <summary>
    /// Stores turns of a monitored agent (<see cref="TurnBatch"/>). 404: no such agent; 409: its monitoring is off (with <c>feature</c>), or
    /// the environment is not one it takes; 422: a bad environment name, turn or contract. An unknown contract hash without the contract
    /// stores nothing and answers <see cref="TurnBatchAnswer.SendContract"/>; with the contract, the version is registered as a run does.
    /// </summary>
    private static async Task<IResult> TakeTurnsAsync(
        TurnBatch batch,
        AgentStore agents,
        ConversationStore conversations,
        CancellationToken cancellationToken
    )
    {
        var agent = await agents.GetAsync(batch.Agent, cancellationToken);
        if (agent is null)
        {
            return ProblemResults.NotFound("Agent", batch.Agent);
        }
        if (agent.Features.Monitoring is not { } monitoring)
        {
            return ProblemResults.FeatureOff(agent.Name, "monitoring");
        }
        if (FeatureRules.EnvironmentProblem(batch.Environment) is { } environmentProblem)
        {
            return ProblemResults.Invalid("environment", environmentProblem);
        }
        if (!FeatureRules.Accepts(monitoring, batch.Environment))
        {
            return ProblemResults.Conflict(
                "Environment not taken",
                $"Agent '{agent.Name}' takes conversations from {string.Join(", ", monitoring.Environments ?? [])} only, not from '{batch.Environment}' (features.monitoring.environments in its suwageval/agent.yaml)."
            );
        }
        var errors = TurnProblems(batch.Turns);
        if (errors.Count > 0)
        {
            return ProblemResults.Invalid(errors);
        }

        var version = await agents.FindVersionAsync(agent.Id, batch.ContractHash, cancellationToken);
        if (version is null)
        {
            if (batch.Contract is not { } contract)
            {
                return Results.Ok(new TurnBatchAnswer { Stored = 0, SendContract = true });
            }
            var contractProblems = AgentContractRules.Problems(contract);
            if (contractProblems.Count > 0)
            {
                return ProblemResults.Invalid(contractProblems.Select(problem => new FieldError("contract", problem)));
            }
            // The hash it should be is in the message: an agent written in another language finds its mistake by comparing.
            if (AgentContractHash.Of(contract) is var hash && hash != batch.ContractHash)
            {
                return ProblemResults.Invalid(
                    "contractHash",
                    $"The contract sent hashes to {hash}, not to the contractHash given (docs/scenario-format.md, Monitoring)."
                );
            }
            version = (
                await agents.RegisterVersionAsync(agent.Id, AgentContractHash.Content(contract), contract.Label, cancellationToken)
            ).Version;
        }
        var stored = await conversations.AddTurnsAsync(agent.Id, batch.Environment, version.Id, batch.Turns, cancellationToken);
        return Results.Ok(new TurnBatchAnswer { Stored = stored });
    }

    private static List<FieldError> TurnProblems(IReadOnlyList<MonitoredTurn> turns)
    {
        var errors = new List<FieldError>();
        if (turns.Count is 0 or > MaxTurns)
        {
            errors.Add(new FieldError("turns", $"A request carries from 1 to {MaxTurns} turns."));
        }
        for (var i = 0; i < turns.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(turns[i].ConversationId) || turns[i].ConversationId.Length > MaxConversationIdLength)
            {
                errors.Add(
                    new FieldError(
                        $"turns[{i}].conversationId",
                        $"A conversation id is not empty and at most {MaxConversationIdLength} characters."
                    )
                );
            }
            if (turns[i].Items.Count == 0)
            {
                errors.Add(new FieldError($"turns[{i}].items", "A turn has at least one item."));
            }
        }
        return errors;
    }

    /// <summary>
    /// A page of the agent's monitored conversations, the newest last turn first. <c>environment</c>, <c>version</c> (repeatable: the
    /// version of the last turn) and <c>state</c> filter them; <c>page</c> (1-based) and <c>pageSize</c> (1 to 100, default 20) as for runs.
    /// 404: no such agent; 422: a bad page or page size.
    /// </summary>
    private static async Task<IResult> ListAsync(
        string agent,
        [FromQuery] string? environment,
        [FromQuery] Guid[]? version,
        [FromQuery] ConversationState? state,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        AgentStore agents,
        ConversationStore conversations,
        CancellationToken cancellationToken
    )
    {
        var size = pageSize ?? DefaultPageSize;
        if (size is < 1 or > MaxPageSize)
        {
            return ProblemResults.Invalid("pageSize", $"The page size is between 1 and {MaxPageSize}.");
        }
        if (page is < 1)
        {
            return ProblemResults.Invalid("page", "The page is 1 or more.");
        }
        var found = await agents.GetAsync(agent, cancellationToken);
        if (found is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        var filter = (
            Environment: string.IsNullOrEmpty(environment) ? null : environment,
            Versions: version ?? [],
            State: state?.ToString()
        );
        var total = await conversations.CountAsync(found.Id, filter.Environment, filter.Versions, filter.State, cancellationToken);
        var shownPage = Math.Min(page ?? 1, Math.Max(1, (total + size - 1) / size));
        var rows = await conversations.ListAsync(
            found.Id,
            filter.Environment,
            filter.Versions,
            filter.State,
            (shownPage - 1) * size,
            size,
            cancellationToken
        );
        return Results.Ok(
            new ConversationList
            {
                Conversations = [.. rows.Select(Summary)],
                Page = shownPage,
                TotalCount = total,
                Environments = await conversations.EnvironmentsAsync(found.Id, cancellationToken),
            }
        );
    }

    private static async Task<IResult> GetAsync(Guid id, ConversationStore conversations, CancellationToken cancellationToken)
    {
        var row = await conversations.GetAsync(id, cancellationToken);
        if (row is null)
        {
            return ProblemResults.NotFound("Conversation", id.ToString());
        }
        var turns = await conversations.TurnsAsync(id, cancellationToken);
        return Results.Ok(
            new ConversationDetail
            {
                Conversation = Summary(row),
                Turns =
                [
                    .. turns.Select(turn => new ConversationTurn
                    {
                        Id = turn.Id,
                        At = turn.At,
                        AgentVersionId = turn.AgentVersionId,
                        DataVersion = turn.DataVersion,
                        Items = JsonSerializer.Deserialize<ConversationItem[]>(turn.Items, ContractContent.Lenient) ?? [],
                    }),
                ],
            }
        );
    }

    private static ConversationSummary Summary(ConversationRow row) =>
        new()
        {
            Id = row.Id,
            Agent = row.AgentName,
            Environment = row.Environment,
            ConversationId = row.ConversationKey,
            AgentVersionId = row.AgentVersionId,
            VersionLabel = row.VersionLabel,
            VersionChanged = row.VersionChanged,
            DataVersion = row.DataVersion,
            State = Enum.Parse<ConversationState>(row.State),
            Turns = row.Turns,
            StartedAt = row.StartedAt,
            LastTurnAt = row.LastTurnAt,
            ClosedAt = row.ClosedAt,
        };
}
