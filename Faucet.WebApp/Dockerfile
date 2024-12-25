FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
VOLUME /app/data

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Faucet.WebApp/Faucet.WebApp.csproj", "Faucet.WebApp/"]
RUN dotnet restore "Faucet.WebApp/Faucet.WebApp.csproj"
COPY . .
WORKDIR "/src/Faucet.WebApp"
RUN dotnet build "Faucet.WebApp.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Faucet.WebApp.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final

USER 0
RUN apt-get update && apt-get install avahi-utils -y

WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Faucet.WebApp.dll"]
