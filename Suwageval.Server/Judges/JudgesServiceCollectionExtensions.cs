using Suwageval.Server.LanguageModels;

namespace Suwageval.Server.Judges;

internal static class JudgesServiceCollectionExtensions
{
    /// <summary>The judge and its setting (with the models it needs); a judge model that is not configured stops the server at start.</summary>
    public static IServiceCollection AddJudges(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModels(configuration);
        services.AddOptions<JudgeOptions>().Bind(configuration.GetSection(JudgeOptions.Section)).ValidateOnStart();
        services.AddSingleton<Judge>().AddSingleton<ArgumentJudge>();
        return services;
    }
}
