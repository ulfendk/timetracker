# syntax=docker/dockerfile:1
#
# Standard framework-dependent multi-stage .NET build (SDK to build, aspnet runtime to run).
# Deliberately NOT self-contained/trimmed: Track My Time uses reflection-heavy libraries
# (Dapper's POCO mapping, DbUp's embedded-resource script loading, MudBlazor) that are easy to
# break under trimming without extensive testing. The aspnet runtime image is already compact,
# and buildx resolves the right per-arch base image automatically - this is the simplest,
# least-surprising path to a small, fast image on the target ARM64 hardware.

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore and publish in one step (rather than a separate cached restore layer) - a split
# restore/--no-restore publish across separate RUN layers intermittently can't find packages
# that were just restored, so this trades a little layer-cache efficiency for reliability.
# Framework-dependent output is portable IL, not tied to a runtime identifier, so this
# deliberately does NOT pass --arch/-r - the base image below is what fixes the architecture.
COPY app/src/TrackMyTime.Web/ TrackMyTime.Web/
RUN dotnet publish TrackMyTime.Web/TrackMyTime.Web.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

# Ingress reaches the app over plain HTTP on the container network - HA's proxy terminates
# TLS/auth in front of it. 8099 matches ingress_port in config.yaml.
ENV ASPNETCORE_URLS=http://+:8099 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

EXPOSE 8099
ENTRYPOINT ["dotnet", "TrackMyTime.Web.dll"]
