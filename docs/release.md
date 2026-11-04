# Releasing Suwageval.AgentSdk

The owner releases it, from a commit of `master` whose CI is green: `just agent-publish 0.1.0` runs `.github/workflows/agent-sdk.yml` on
`master` (`gh workflow run`) and says how to follow the run; it stops first on a bad version, another branch, uncommitted changes or a
`master` that differs from the remote master. The workflow refuses anything but `master`, runs the tests that protect the SDK (the classes
it names, with a PostgreSQL for the one that needs it: the whole gate already ran on that commit in `ci.yml`, and `AgentSdkWorkflowTests`
fails for a class that no longer exists), packs that version (`-p:Version=`, built with `ContinuousIntegrationBuild`, so the symbols hold no local paths)
and pushes it with its `.snupkg` to nuget.org. A version on nuget.org can never be deleted, only unlisted, so a bad release is followed by
the next version, never replaced. The csproj says `0.0.0-local`, which is what `just agent-pack` produces. Agents never publish.

**Why in CI, with no key.** Trusted Publishing is how nuget.org gives a key to a CI job instead of to a person: the job's OIDC token is
exchanged (the NuGet login action) for a key that lives one hour, so the repository holds no key to leak or rotate, and only the job that pushes,
which runs no code of the repository, can ask for one. It can only work from a workflow, which is why publishing is in CI again; an
earlier workflow was removed in `b61ef9a`, when the releases went to a feed named in `.env` rather than to nuget.org.

**One-time setup.** On nuget.org, *Trusted Publishing*: a policy with publisher GitHub Actions, owner `Manomenu`, repository
`suwalski-agent-eval`, workflow file `agent-sdk.yml` (the file's name, so renaming the file breaks releases), no environment, scope to push
new packages and package versions, glob `Suwageval.AgentSdk`. In this repository, the secret `NUGET_USER`: the nuget.org profile name of
the policy's package owner, which is not the GitHub owner and not an email address.

**The fallback, `just agent-publish-local <version>`,** packs and pushes from the machine with a key from the gitignored `.env`, and is not
the normal way. It stays for two cases. If nuget.org refuses the workflow's first push of the package (its documentation mentions a
scope for new packages but does not plainly say a first publish works), set `AGENT_SDK_NUGET_API_KEY` to a key from nuget.org, API Keys,
Create: scope Push (new packages and package versions), glob `Suwageval.AgentSdk`, and remove the key once that release is out. And to
publish to another feed, set both `AGENT_SDK_FEED_URL` and `AGENT_SDK_FEED_API_KEY` in `.env` (a private feed's address is the owner's infrastructure, docs/deployment.md); that push leaves the symbols out. Run it on a clean `master` too: it does not check that for you.
