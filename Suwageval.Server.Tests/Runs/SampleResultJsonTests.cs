using System.Text.Json;
using System.Text.Json.Serialization;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Runs;

namespace Suwageval.Server.Tests.Runs;

/// <summary>A client older than simulations refuses unknown fields, so a sample of one turn carries none of theirs.</summary>
public sealed class SampleResultJsonTests
{
    // The server's response options (Api/ServerApp.cs): the contract's rules, an unset field absent.
    private static readonly JsonSerializerOptions Response = new(ContractJson.Options)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    public void A_sample_of_one_turn_has_no_simulation_fields()
    {
        var json = JsonSerializer.Serialize(new SampleResult { Sample = 1, Status = SampleStatus.Done }, Response);

        Assert.DoesNotContain("endedByUser", json, StringComparison.Ordinal);
        Assert.DoesNotContain("turnDurationsMs", json, StringComparison.Ordinal);
    }

    [Fact]
    public void A_simulation_the_user_ended_says_so()
    {
        var json = JsonSerializer.Serialize(
            new SampleResult
            {
                Sample = 1,
                Status = SampleStatus.Done,
                EndedByUser = true,
            },
            Response
        );

        Assert.Contains("\"endedByUser\":true", json, StringComparison.Ordinal);
    }
}
