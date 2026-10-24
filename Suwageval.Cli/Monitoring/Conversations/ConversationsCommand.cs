using System.CommandLine;
using System.Globalization;
using Suwageval.Cli.Output;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Monitoring;

namespace Suwageval.Cli.Monitoring.Conversations;

/// <summary>
/// <c>suwageval monitoring conversations</c>: the newest conversations the deployed agent named in <c>suwageval/agent.yaml</c> sent, one line
/// each (id, environment, state, turns, version, last turn, the agent's id of it); <c>--json</c> prints the page. <c>monitoring
/// conversation &lt;id&gt;</c> prints one with its turns. Exit 0, or 2 when the agent file or the server is wrong (monitoring off too).
/// </summary>
internal static class ConversationsCommand
{
    private const int DefaultLimit = 20;

    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    public static Command List(HttpClient? http = null)
    {
        var limit = new Option<int>("--limit")
        {
            Description = "How many of the newest conversations to list (at most 100).",
            DefaultValueFactory = _ => DefaultLimit,
        };
        limit.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int>() is < 1 or > 100)
            {
                result.AddError("--limit is from 1 to 100.");
            }
        });
        var environment = new Option<string?>("--environment")
        {
            Description = "Only the conversations of this environment (production, staging…).",
        };
        var version = new Option<Guid[]>("--version")
        {
            Description = "Only the conversations whose last turn this agent version answered (repeatable).",
            AllowMultipleArgumentsPerToken = false,
            DefaultValueFactory = _ => [],
        };
        var state = new Option<ConversationState?>("--state") { Description = "Only Open or only Closed conversations." };
        var json = CommandOptions.Json("Print the page of conversations as JSON.");
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();
        var command = new Command("conversations", "List the newest conversations of the deployed agent, the newest turn first.")
        {
            limit,
            environment,
            version,
            state,
            json,
            server,
            dir,
        };
        command.SetAction(
            (parsed, cancellationToken) =>
                ServerCall.ForAgentAsync(
                    parsed.GetRequiredValue(dir),
                    parsed.GetValue(server),
                    http,
                    parsed.InvocationConfiguration.Error,
                    async (client, agent, _) =>
                    {
                        var list = await client.ConversationsAsync(
                            agent,
                            parsed.GetValue(limit),
                            parsed.GetValue(environment),
                            parsed.GetValue(version) ?? [],
                            parsed.GetValue(state),
                            cancellationToken
                        );
                        var output = parsed.InvocationConfiguration.Output;
                        if (parsed.GetValue(json))
                        {
                            await output.WriteLineAsync(OutputJson.Of(list));
                            return 0;
                        }
                        if (list.Conversations.Count == 0)
                        {
                            await output.WriteLineAsync("No conversations.");
                            return 0;
                        }
                        foreach (var conversation in list.Conversations)
                        {
                            await output.WriteLineAsync(Line(conversation));
                        }
                        return 0;
                    }
                )
        );
        return command;
    }

    public static Command Show(HttpClient? http = null)
    {
        var id = new Argument<Guid>("id") { Description = "The conversation's id, as `monitoring conversations` lists it." };
        var json = CommandOptions.Json("Print the conversation with its turns as JSON.");
        var server = CommandOptions.Server();
        var command = new Command("conversation", "Print one monitored conversation with its turns.") { id, json, server };
        command.SetAction(
            (parsed, cancellationToken) =>
                ServerCall.RunAsync(
                    parsed.GetValue(server),
                    http,
                    parsed.InvocationConfiguration.Error,
                    async (client, _) =>
                    {
                        var detail = await client.ConversationAsync(parsed.GetValue(id), cancellationToken);
                        var output = parsed.InvocationConfiguration.Output;
                        await output.WriteLineAsync(parsed.GetValue(json) ? OutputJson.Of(detail) : Describe(detail));
                        return 0;
                    }
                )
        );
        return command;
    }

    internal static string Line(ConversationSummary conversation) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{conversation.Id}  {conversation.Environment}  {conversation.State}  {conversation.Turns} turns  version {conversation.AgentVersionId}{RunLabelText.Suffix(conversation.VersionLabel)}{(conversation.VersionChanged ? " (changed during it)" : "")}  last {conversation.LastTurnAt.UtcDateTime:yyyy-MM-dd HH:mm}Z  conversation {conversation.ConversationId}"
        );

    internal static string Describe(ConversationDetail detail)
    {
        var lines = new List<string> { Line(detail.Conversation) };
        for (var i = 0; i < detail.Turns.Count; i++)
        {
            var turn = detail.Turns[i];
            lines.Add("");
            lines.Add(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"turn {i + 1}  {turn.At.UtcDateTime:yyyy-MM-dd HH:mm:ss}Z{(turn.AgentVersionId == detail.Conversation.AgentVersionId ? "" : $"  version {turn.AgentVersionId}")}{(turn.DataVersion is null ? "" : $"  data {turn.DataVersion}")}"
                )
            );
            lines.AddRange(turn.Items.Select(item => "  " + ConversationText.Item(item)));
        }
        return string.Join(Environment.NewLine, lines);
    }
}
