# Suwageval.AgentSdk

What a .NET agent under test (.NET 9 or later) needs to be evaluated by [Suwageval](https://github.com/Manomenu/suwalski-agent-eval):

```csharp
builder.Services.AddSingleton<IAgentContractSource, MyContractSource>();
builder.Services.AddSingleton<ITurnHandler, MyTurnHandler>();
app.MapSuwagevalAgent();   // GET /suwageval/contract, POST /suwageval/turn
```

- the contract types (`AgentContract`, `TurnRequest`, `TurnResponse`, the `ConversationItem`s) and `ContractJson.Options`,
  the JSON options the server reads them with;
- `AgentContractRules.Problems(contract)`: what the server would reject in your contract, for a unit test.
- `AgentContractHash.Of(contract)`: the hash the server knows your version by (the same function the server registers it with).
- `request.Correlation` (`RunId`, `Scenario`, `Sample`): the `Suwageval-*` headers of the turn, for traces and logs; a member is null when its header is absent.
- `request.HistoryTurns()`: the conversation before the last item as `HistoryTurn`s (user message, null for what was logged before the first one; the items that followed it in order, the tool calls
  paired with their results, the assistant message); a history that cannot be one (a tool call id twice, a result without its call, tool calls no assistant message closes) is a `TurnRejectedException`.
- `request.FixturesAs<T>()`: the scenario's fixtures as your type, strict about members it does not declare; null when there are none.
- `ContractSchema.ObjectSchema(properties, required)` and `EnumSchema<TEnum>()`: the JSON schemas of event data and fixtures in your contract.
- `TurnRejectedException(reason)`: thrown by your handler for a conversation it cannot answer; the turn is answered 400 with the reason.
- Monitoring of a deployed agent: `builder.Services.AddSuwagevalMonitoring(builder.Configuration)` (the section `Suwageval`: `Url`,
  `Agent`, `Environment`, optionally `AccessClientId`/`AccessClientSecret`), then `IConversationRecorder.Record(conversationId, items)`
  after each real turn; the turns go to Suwageval in the background. Without `Suwageval:Url` the recorder does nothing.

The HTTP contract stays the source of truth; this package saves a .NET agent from re-declaring it.

Releases: `just agent-publish <version>`; see AGENTS.md, section 12.
