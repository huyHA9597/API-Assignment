FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/EventGoNoGoAdvisor.Api/EventGoNoGoAdvisor.Api.csproj", "src/EventGoNoGoAdvisor.Api/"]
COPY ["src/EventGoNoGoAdvisor.ServiceDefaults/EventGoNoGoAdvisor.ServiceDefaults.csproj", "src/EventGoNoGoAdvisor.ServiceDefaults/"]
RUN dotnet restore "src/EventGoNoGoAdvisor.Api/EventGoNoGoAdvisor.Api.csproj"

COPY src/ src/
RUN dotnet publish "src/EventGoNoGoAdvisor.Api/EventGoNoGoAdvisor.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

COPY --from=build /app/publish .
USER app

EXPOSE 8080
EXPOSE 8081

ENTRYPOINT ["dotnet", "EventGoNoGoAdvisor.Api.dll"]
