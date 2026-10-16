namespace Suwageval.Cli.Files;

/// <summary>The agent's name, from <c>agent.yaml</c> in a folder: what the commands that talk to the server about one agent need.</summary>
internal static class AgentName
{
    /// <summary>The name, or null after writing what is wrong with the folder or the file to <paramref name="error"/>.</summary>
    public static string? Read(string dir, TextWriter error)
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
        return agent?.Definition.Name;
    }
}
