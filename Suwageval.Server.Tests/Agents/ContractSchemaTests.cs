using System.Text.Json;
using System.Text.Json.Nodes;
using Suwageval.AgentSdk.Agents;

namespace Suwageval.Server.Tests.Agents;

public sealed class ContractSchemaTests
{
    private enum Mode
    {
        Fast,
        Slow,
    }

    [Fact]
    public void An_object_schema_closes_the_object_and_nests_an_enum_schema()
    {
        var schema = ContractSchema.ObjectSchema(new JsonObject { ["mode"] = ContractSchema.EnumSchema<Mode>() }, "mode");

        Assert.Equal(
            /*lang=json,strict*/
            """{"type":"object","properties":{"mode":{"type":"string","enum":["Fast","Slow"]}},"required":["mode"],"additionalProperties":false}""",
            JsonSerializer.Serialize(schema)
        );
    }
}
