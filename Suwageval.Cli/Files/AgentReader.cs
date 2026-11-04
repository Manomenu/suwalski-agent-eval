namespace Suwageval.Cli.Files;

internal static class AgentReader
{
    public static LoadedAgent? Read(string dir, TextWriter error)
    {
        var file = Path.Combine(dir, "agent.yaml");
        if (!File.Exists(file))
        {
            error.WriteLine($"{file} not found: run this in an agent's repository, or point --dir at its suwageval folder.");
            return null;
        }
        var problems = new List<Problem>();
        var agent = AgentFileReader.Read(file, File.ReadAllText(file), problems);
        foreach (var problem in problems)
        {
            error.WriteLine($"{problem.File}: {problem.Message}");
        }
        return agent;
    }

    public static LoadedAgent? Quietly(string dir)
    {
        var file = Path.Combine(dir, "agent.yaml");
        return File.Exists(file) ? AgentFileReader.Read(file, File.ReadAllText(file), []) : null;
    }

    public static string? SuwagevalUrl(string dir)
    {
        var file = Path.Combine(dir, "agent.yaml");
        return File.Exists(file) ? AgentFileReader.SuwagevalUrl(file, File.ReadAllText(file)) : null;
    }
}
