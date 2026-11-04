using Suwageval.Server.LanguageModel;

namespace Suwageval.Server.Judges;

internal static class JudgesServiceCollectionExtensions
{
    public static IServiceCollection AddJudges(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModels(configuration);
        services.AddOptions<JudgeOptions>().Bind(configuration.GetSection(JudgeOptions.Section)).ValidateOnStart();
        services.AddSingleton<Judge>().AddSingleton<ArgumentJudge>();
        return services;
    }
}
