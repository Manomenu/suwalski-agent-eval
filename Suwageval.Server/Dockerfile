FROM mcr.microsoft.com/dotnet/sdk:10.0.402-noble AS builder

WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY Suwageval.AgentSdk/Suwageval.AgentSdk.csproj Suwageval.AgentSdk/packages.lock.json Suwageval.AgentSdk/
COPY Suwageval.Contracts/Suwageval.Contracts.csproj Suwageval.Contracts/packages.lock.json Suwageval.Contracts/
COPY Suwageval.Server/Suwageval.Server.csproj Suwageval.Server/packages.lock.json Suwageval.Server/
RUN dotnet restore Suwageval.Server --locked-mode

COPY .editorconfig ./
COPY Suwageval.AgentSdk/ Suwageval.AgentSdk/
COPY Suwageval.Contracts/ Suwageval.Contracts/
COPY Suwageval.Server/ Suwageval.Server/
RUN dotnet publish Suwageval.Server --no-restore -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0.13-noble

COPY --from=builder /app /app

ENV ASPNETCORE_HTTP_PORTS=6230

USER app
WORKDIR /app
EXPOSE 6230

ENTRYPOINT ["dotnet", "Suwageval.Server.dll"]
