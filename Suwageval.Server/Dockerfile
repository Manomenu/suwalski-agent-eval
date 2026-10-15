# Build context is the SOLUTION ROOT, not this directory: global.json, Directory.Build.props
# and Directory.Packages.props (the package versions) live up there.
#
#   docker build -f Suwageval.Server/Dockerfile -t suwageval-server .

FROM mcr.microsoft.com/dotnet/sdk:10.0.402-noble AS builder

WORKDIR /src

# Manifests and lockfiles first — this layer is the expensive one and it only rebuilds when a
# dependency moves. Locked mode: a lockfile out of date with the packages fails the image.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY Suwageval.Server/Suwageval.Server.csproj Suwageval.Server/packages.lock.json Suwageval.Server/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore Suwageval.Server --locked-mode

# Then the sources, which change every commit.
COPY .editorconfig ./
COPY Suwageval.Server/ Suwageval.Server/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish Suwageval.Server --no-restore -c Release -o /app


# The runtime image matches the SDK band above; both are pinned like every image here.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.13-noble

COPY --from=builder /app /app

# The port ASP.NET Core listens on (the chart sets the same value from server.port).
ENV ASPNETCORE_HTTP_PORTS=6230

# The image's own non-root user (uid 1654).
USER app
WORKDIR /app
EXPOSE 6230

ENTRYPOINT ["dotnet", "Suwageval.Server.dll"]
