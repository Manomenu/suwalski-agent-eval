using Suwageval.Cli.Tests.Validation;

namespace Suwageval.Cli.Tests.Evaluation.Validate;

// Why: the working directory belongs to the whole process, and the root help reads the agent folder there, so this runs while nothing else does
[CollectionDefinition(nameof(ValidateWorkingDirectoryTests), DisableParallelization = true)]
[Collection(nameof(ValidateWorkingDirectoryTests))]
public sealed class ValidateWorkingDirectoryTests
{
    [Fact]
    public void Without_a_path_the_suwageval_folder_of_the_working_directory_is_used()
    {
        using var agent = TestAgent.AgentServing(TestAgent.Contract);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var previous = Environment.CurrentDirectory;
        Environment.CurrentDirectory = Path.Combine(AppContext.BaseDirectory, "Sample");
        try
        {
            Assert.Equal(
                0,
                CliApp.Run(["validate"], new StringReader(""), output, error, agent, interrupt: TestContext.Current.CancellationToken)
            );
        }
        finally
        {
            Environment.CurrentDirectory = previous;
        }
    }
}
