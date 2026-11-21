using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Suwageval.AgentSdk.Agents;
using Suwageval.AgentSdk.Conversations;
using Suwageval.AgentSdk.Monitoring;
using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Monitoring;
using Suwageval.Contracts.Refusals;
using Suwageval.Server.Agents;
using Suwageval.Server.Problems;

namespace Suwageval.Server.Monitoring.Conversations;

internal static class ConversationEndpoints
{
    private const int DefaultPageSize = 20;

    public const int MaxPageSize = 100;

    public const int MaxTurns = 500;

    public const int MaxConversationIdLength = 200;

    private const string SendAsMonitoringSays =
        "Send the turns as docs/scenario-format.md (Monitoring) describes, or let the Suwageval SDK send them.";

    public static IEndpointRouteBuilder MapConversations(this IEndpointRouteBuilder app)
    {
        app.MapPost("/turns", TakeTurnsAsync)
            .Produces<TurnBatchAnswer>()
            .ProducesRefusal(StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity);
        app.MapGet("/agents/{agent}/conversations", ListAsync)
            .Produces<ConversationList>()
            .ProducesRefusal(StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity);
        app.MapGet("/conversations/{id:guid}", GetAsync).Produces<ConversationDetail>().ProducesRefusal(StatusCodes.Status404NotFound);
        return app;
    }

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
            return ProblemResults.AgentNotFound(batch.Agent);
        }
        if (agent.Features.Monitoring is not { } monitoring)
        {
            return ProblemResults.MonitoringOff(agent.Name);
        }
        if (FeatureRules.EnvironmentProblem(batch.Environment) is { } environmentProblem)
        {
            return ProblemResults.Invalid("environment", environmentProblem, FeatureRules.EnvironmentFix);
        }
        if (!FeatureRules.Accepts(monitoring, batch.Environment))
        {
            return ProblemResults.Conflict(
                RefusalCode.EnvironmentNotTaken,
                "Environment not taken",
                $"Agent '{agent.Name}' takes conversations from {string.Join(", ", monitoring.Environments ?? [])} only, not from '{batch.Environment}': add it to features.monitoring.environments in its suwageval/agent.yaml and push."
            );
        }
        var errors = TurnProblems(batch.Turns);
        if (errors.Count > 0)
        {
            return ProblemResults.Invalid(errors, SendAsMonitoringSays);
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
                return ProblemResults.Invalid(
                    contractProblems.Select(problem => new FieldError("contract", problem)),
                    "Correct the agent's contract, then send its turns again."
                );
            }
            if (AgentContractHash.Of(contract) is var hash && hash != batch.ContractHash)
            {
                return ProblemResults.Invalid(
                    "contractHash",
                    $"The contract sent hashes to {hash}, not to the contractHash given.",
                    SendAsMonitoringSays
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

    private static async Task<IResult> ListAsync(
        string agent,
        [FromQuery] string? environment,
        [FromQuery] Guid[]? version,
        [FromQuery] ConversationState? state,
        [FromQuery] string? failedCheck,
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
            return ProblemResults.Invalid(
                "pageSize",
                $"The page size is not between 1 and {MaxPageSize}.",
                $"Ask for 1 to {MaxPageSize} conversations a page."
            );
        }
        if (page is < 1)
        {
            return ProblemResults.Invalid("page", "The page is below 1.", "Ask for page 1 or later.");
        }
        var found = await agents.GetAsync(agent, cancellationToken);
        if (found is null)
        {
            return ProblemResults.AgentNotFound(agent);
        }
        var filter = (
            Environment: string.IsNullOrEmpty(environment) ? null : environment,
            Versions: version ?? [],
            State: state?.ToString()
        );
        var failing = string.IsNullOrEmpty(failedCheck) ? null : failedCheck;
        var total = await conversations.CountAsync(found.Id, filter.Environment, filter.Versions, filter.State, failing, cancellationToken);
        var shownPage = Math.Min(page ?? 1, Math.Max(1, (total + size - 1) / size));
        var rows = await conversations.ListAsync(
            found.Id,
            filter.Environment,
            filter.Versions,
            filter.State,
            (shownPage - 1) * size,
            size,
            failing,
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
            return ProblemResults.ConversationNotFound(id);
        }
        var turns = await conversations.TurnsAsync(id, cancellationToken);
        var results = await conversations.ResultsAsync(id, cancellationToken);
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
                Checks =
                [
                    .. results.Select(result => new ConversationCheckResult
                    {
                        Key = result.Key,
                        Revision = result.Revision,
                        Passed = result.Passed,
                        NotJudged = result.NotJudged,
                        Message = result.Message,
                        Evidence = JsonSerializer.Deserialize<ConversationItem[]>(result.Evidence, ContractContent.Lenient) ?? [],
                        JudgeModel = result.JudgeModel,
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
            CheckedAt = row.CheckedAt,
            FailedChecks = row.FailedChecks,
            NotJudgedChecks = row.NotJudgedChecks,
        };
}
