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
- `TurnRejectedException(reason)`: thrown by your handler for a conversation it cannot answer; the turn is answered 400 with the reason.

The HTTP contract stays the source of truth; this package saves a .NET agent from re-declaring it.

Releases: `just agent-publish <version>`; see AGENTS.md, section 12.
