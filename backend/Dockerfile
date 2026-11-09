# ---- Build stage -------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so the NuGet layer is cached while source files change.
COPY SchoolAdmission.sln ./
COPY src/SchoolAdmission.Api/SchoolAdmission.Api.csproj src/SchoolAdmission.Api/
RUN dotnet restore src/SchoolAdmission.Api/SchoolAdmission.Api.csproj

COPY src/ src/
RUN dotnet publish src/SchoolAdmission.Api/SchoolAdmission.Api.csproj \
    -c Release -o /app/publish --no-restore

# ---- Runtime stage -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# The brief requires the API on port 8080. ASPNETCORE_URLS overrides appsettings "Urls".
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

# Run as the non-root user that ships with the aspnet image.
USER app

COPY --from=build /app/publish ./

# No HEALTHCHECK here: the slim aspnet image has no curl/wget. /health exists for external probes.
ENTRYPOINT ["dotnet", "SchoolAdmission.Api.dll"]
