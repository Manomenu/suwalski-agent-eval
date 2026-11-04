using Suwageval.Server.Api;
using Suwageval.Server.LiveCheck;

// Why: the entry point only starts the app, and the daily model check is the one other command, run by the chart's CronJob from the same image
if (args is ["live-check"])
{
    return await JudgeLiveCheck.RunAsync([]);
}
await ServerApp.Build(args).RunAsync();
return 0;
