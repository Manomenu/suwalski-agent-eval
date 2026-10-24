using Suwageval.Server.Api;
using Suwageval.Server.LiveCheck;

// The entry point only starts the app; what the app is lives in Api/ServerApp.cs. The one other command is the
// daily check of the model, which the chart's CronJob runs from the same image (LiveCheck/JudgeLiveCheck.cs).
if (args is ["live-check"])
{
    return await JudgeLiveCheck.RunAsync([]);
}
await ServerApp.Build(args).RunAsync();
return 0;
