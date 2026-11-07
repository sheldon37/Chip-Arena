FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Relay/ChipArena.Relay.csproj Relay/
RUN dotnet restore Relay/ChipArena.Relay.csproj
COPY Relay/Program.cs Relay/
COPY Assets/ChipArena/Core/NativeRoomCodes.cs Assets/ChipArena/Core/
RUN dotnet publish Relay/ChipArena.Relay.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "ChipArena.Relay.dll"]
