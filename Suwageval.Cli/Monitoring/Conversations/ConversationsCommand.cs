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
    /// <param name="openBrowser">Opens a page for <c>--open</c>; none means the system's default browser (the tests record the page instead).</param>
    public static Command List(HttpClient? http = null, Action<Uri>? openBrowser = null)
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
        var failedCheck = new Option<string?>("--failed-check")
        {
            Description = "Only the conversations that failed this monitoring check (its key).",
        };
        var json = CommandOptions.Json("Print the page of conversations as JSON.");
        var open = CommandOptions.Open(
            "the agent's Monitoring page, narrowed to --environment and --failed-check (the other options do not apply there)"
        );
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();
        var command = new Command("conversations", "List the newest conversations of the deployed agent, the newest turn first.")
        {
            limit,
            environment,
            version,
            state,
            failedCheck,
            json,
            open,
            server,
            dir,
        };
        CommandOptions.RefuseJsonWithOpen(command, json, open);
        command.SetAction(
            (parsed, cancellationToken) =>
                ServerCall.ForAgentAsync(
                    parsed.GetRequiredValue(dir),
                    parsed.GetValue(server),
                    http,
                    parsed.InvocationConfiguration.Error,
                    async (client, agent, site) =>
                    {
                        if (parsed.GetValue(open))
                        {
                            await WebPage.ShowAsync(
                                ServerAddress.MonitoringPage(site, agent, parsed.GetValue(environment), parsed.GetValue(failedCheck)),
                                parsed.InvocationConfiguration.Output,
                                parsed.InvocationConfiguration.Error,
                                openBrowser
                            );
                            return 0;
                        }
                        var list = await client.ConversationsAsync(
                            agent,
                            parsed.GetValue(limit),
                            parsed.GetValue(environment),
                            parsed.GetValue(version) ?? [],
                            parsed.GetValue(state),
                            cancellationToken,
                            parsed.GetValue(failedCheck)
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

    public static Command Show(HttpClient? http = null, Action<Uri>? openBrowser = null)
    {
        var id = new Argument<Guid>("id") { Description = "The conversation's id, as `monitoring conversations` lists it." };
        var json = CommandOptions.Json("Print the conversation with its turns as JSON.");
        var open = CommandOptions.Open("the conversation's page");
        var server = CommandOptions.Server();
        var command = new Command("conversation", "Print one monitored conversation with its turns (or --open its page on the web).")
        {
            id,
            json,
            open,
            server,
        };
        CommandOptions.RefuseJsonWithOpen(command, json, open);
        command.SetAction(
            (parsed, cancellationToken) =>
                ServerCall.RunAsync(
                    parsed.GetValue(server),
                    http,
                    parsed.InvocationConfiguration.Error,
                    async (client, site) =>
                    {
                        var detail = await client.ConversationAsync(parsed.GetValue(id), cancellationToken);
                        var output = parsed.InvocationConfiguration.Output;
                        if (parsed.GetValue(open))
                        {
                            await WebPage.ShowAsync(
                                ServerAddress.ConversationPage(site, detail.Conversation.Id),
                                parsed.InvocationConfiguration.Output,
                                parsed.InvocationConfiguration.Error,
                                openBrowser
                            );
                            return 0;
                        }
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
            $"{conversation.Id}  {conversation.Environment}  {conversation.State}  {conversation.Turns} turns  version {conversation.AgentVersionId}{RunLabelText.Suffix(conversation.VersionLabel)}{(conversation.VersionChanged ? " (changed during it)" : "")}  last {conversation.LastTurnAt.UtcDateTime:yyyy-MM-dd HH:mm}Z  {Checks(conversation)}  conversation {conversation.ConversationId}"
        );

    /// <summary>"2 checks failed", "checks passed", "1 check not judged" (the judge's model did not answer), or "not checked" while it was not.</summary>
    private static string Checks(ConversationSummary conversation)
    {
        if (conversation.CheckedAt is null)
        {
            return "not checked";
        }
        var parts = new List<string>();
        if (conversation.FailedChecks > 0)
        {
            parts.Add($"{conversation.FailedChecks} check{(conversation.FailedChecks == 1 ? "" : "s")} failed");
        }
        if (conversation.NotJudgedChecks > 0)
        {
            parts.Add($"{conversation.NotJudgedChecks} check{(conversation.NotJudgedChecks == 1 ? "" : "s")} not judged");
        }
        return parts.Count == 0 ? "checks passed" : string.Join(", ", parts);
    }

    internal static string Describe(ConversationDetail detail)
    {
        var lines = new List<string> { Line(detail.Conversation) };
        foreach (var check in detail.Checks)
        {
            lines.Add($"{(check.Passed ? "✓" : check.NotJudged ? "?" : "✗")} {check.Key}: {check.Message}");
        }
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
