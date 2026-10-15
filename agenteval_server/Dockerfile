# Build context is the SOLUTION ROOT, not this directory: global.json, Directory.Build.props
# and Directory.Packages.props (the package versions) live up there.
#
#   podman build -f agenteval_server/Dockerfile -t agenteval-server .

FROM mcr.microsoft.com/dotnet/sdk:10.0.402-noble AS builder

WORKDIR /src

# Manifests and lockfiles first — this layer is the expensive one and it only rebuilds when a
# dependency moves. Locked mode: a lockfile out of date with the packages fails the image.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY agenteval_server/AgentEval.Server/AgentEval.Server.csproj agenteval_server/AgentEval.Server/packages.lock.json agenteval_server/AgentEval.Server/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore agenteval_server/AgentEval.Server --locked-mode

# Then the sources, which change every commit.
COPY .editorconfig ./
COPY agenteval_server/AgentEval.Server/ agenteval_server/AgentEval.Server/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish agenteval_server/AgentEval.Server --no-restore -c Release -o /app


# The runtime image matches the SDK band above; both are pinned like every image here.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.13-noble

COPY --from=builder /app /app

# Settings derive the solution root from the project directory in development. Here the binary
# runs from /app with no sources around it, so the root is stated outright.
ENV AGENTEVAL_SOLUTION_ROOT=/app

# The image's own non-root user (uid 1654).
USER app
WORKDIR /app
EXPOSE 6220

ENTRYPOINT ["dotnet", "AgentEval.Server.dll"]
