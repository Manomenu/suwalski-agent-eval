namespace Suwageval.Gate.Repository;

public sealed class ModelSettingsDocsTests
{
    [Fact]
    [Trait("criterion", "AC-simpler-models-and-checks-4")]
    public void The_readme_and_the_env_example_say_the_fallback_model_is_enough()
    {
        var readme = RepositoryFiles.Read("README.md");
        var example = RepositoryFiles.Read(".env.example");

        Assert.Contains("**One model is\nenough**: `Models:fallback`", readme, StringComparison.Ordinal);
        Assert.Contains("all use it unless one has its own entry", example, StringComparison.Ordinal);
        Assert.Contains("Models__fallback__BaseUrl=", example, StringComparison.Ordinal);
        Assert.Contains("Models__fallback__Model=", example, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("criterion", "AC-simpler-models-and-checks-5")]
    [InlineData("deploy/chart/templates/server.yaml")]
    [InlineData("deploy/chart/templates/live-check.yaml")]
    public void The_server_and_the_live_check_take_the_secret_of_each_model_as_env_from(string template)
    {
        var helpers = RepositoryFiles.Read("deploy/chart/templates/_helpers.tpl");
        var rendered = RepositoryFiles.Read(template);

        Assert.Contains("define \"app.modelsEnvFrom\"", helpers, StringComparison.Ordinal);
        Assert.Contains("with $model.secret", helpers, StringComparison.Ordinal);
        Assert.Contains("envFrom:", rendered, StringComparison.Ordinal);
        Assert.Contains("include \"app.modelsEnvFrom\" .", rendered, StringComparison.Ordinal);
    }
}
