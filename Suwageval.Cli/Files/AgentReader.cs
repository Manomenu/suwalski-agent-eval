namespace Suwageval.Cli.Files;

/// <summary>The agent of <c>agent.yaml</c> in a folder: what the commands that talk to the server about one agent need.</summary>
internal static class AgentReader
{
    /// <summary>The agent, or null after writing what is wrong with the folder or the file to <paramref name="error"/>.</summary>
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

    /// <summary>The agent of <c>agent.yaml</c> in a folder, or null when it is missing or does not read; nothing is reported.</summary>
    public static LoadedAgent? Quietly(string dir)
    {
        var file = Path.Combine(dir, "agent.yaml");
        return File.Exists(file) ? AgentFileReader.Read(file, File.ReadAllText(file), []) : null;
    }

    /// <summary>The <c>suwagevalUrl</c> of <c>agent.yaml</c> in a folder, or null when there is none, the file is missing or the value is not an address; nothing is reported.</summary>
    public static string? SuwagevalUrl(string dir)
    {
        var file = Path.Combine(dir, "agent.yaml");
        return File.Exists(file) ? AgentFileReader.SuwagevalUrl(file, File.ReadAllText(file)) : null;
    }
}
