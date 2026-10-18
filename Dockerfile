FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ComputerLending.Core/ComputerLending.Core.csproj ComputerLending.Core/
COPY ComputerLending.Data/ComputerLending.Data.csproj ComputerLending.Data/
COPY ComputerLending.Service/ComputerLending.Service.csproj ComputerLending.Service/
COPY ComputerLending.API/ComputerLending.API.csproj ComputerLending.API/
RUN dotnet restore ComputerLending.API/ComputerLending.API.csproj

COPY ComputerLending.Core/ ComputerLending.Core/
COPY ComputerLending.Data/ ComputerLending.Data/
COPY ComputerLending.Service/ ComputerLending.Service/
COPY ComputerLending.API/ ComputerLending.API/
RUN dotnet publish ComputerLending.API/ComputerLending.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN mkdir -p /app/logs && chown app:app /app/logs
COPY --from=build --chown=app:app /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "ComputerLending.API.dll"]
