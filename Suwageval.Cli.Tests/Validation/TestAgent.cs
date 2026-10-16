using Suwageval.Cli.Files;

namespace Suwageval.Cli.Tests.Validation;

/// <summary>A small agent the validation tests check scenarios against, and a way to read what they report.</summary>
internal static class TestAgent
{
    public const string Yaml = """
        name: shop-agent
        endpointUrl: http://localhost:7010
        version:
          model:
            name: some-model
          systemPrompt: Be helpful.
          tools:
            - name: get_order
              description: Look an order up.
              parameters: { type: object }
            - name: refund_order
              description: Refund an order.
              parameters: { type: object }
          context:
            usesRetrieval: false
            events:
              - type: state.changed
                dataSchema: { type: object }
        """;

    public const string WithRetrieval = """
        name: shop-agent
        endpointUrl: http://localhost:7010
        version:
          model:
            name: some-model
          systemPrompt: Be helpful.
          context:
            usesRetrieval: true
        """;

    public const string Scenario = """
        key: refund-happy-path
        tags: [refunds]
        conversation:
          - user: "Refund A-17, please."
        checks:
          - key: refund-called
            type: tool-called
            tool: refund_order
        """;

    /// <summary>Each problem as <c>path: message</c>, in the order found.</summary>
    public static string[] Lines(IEnumerable<Problem> problems) => [.. problems.Select(p => $"{p.Path}: {p.Message}")];
}
