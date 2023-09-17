FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base

WORKDIR /app
EXPOSE 80
ENV TZ=EUROPE/Berlin
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone
RUN apt-get update && apt-get -y upgrade
RUN apt install -y tzdata
RUN apt install -y arping inetutils-ping libnet-ifconfig-wrapper-perl mc

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["tuya_mqtt.net.csproj", "."]
RUN dotnet --info
RUN dotnet restore -r linux-arm "./tuya_mqtt.net.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "tuya_mqtt.net.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "tuya_mqtt.net.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "tuya_mqtt.net.dll"]
