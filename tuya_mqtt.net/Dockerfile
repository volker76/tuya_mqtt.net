FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
ENV DOTNET_CLI_HOME=/tmp
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["tuya_mqtt.net.csproj", "."]
#RUN dotnet --info
ENV DOTNET_CLI_HOME=/tmp
RUN dotnet restore "./tuya_mqtt.net.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "tuya_mqtt.net.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "tuya_mqtt.net.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "tuya_mqtt.net.dll"]
