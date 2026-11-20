using Suwageval.Server.Api;
using Suwageval.Server.LiveCheck;

if (args is ["live-check"])
{
    return await JudgeLiveCheck.RunAsync([]);
}
await ServerApp.Build(args).RunAsync();
return 0;
