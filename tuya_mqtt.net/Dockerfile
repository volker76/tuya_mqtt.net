FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
ENV DOTNET_CLI_HOME=/tmp
WORKDIR /app
EXPOSE 80

ENV TZ=EUROPE/Berlin
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone
RUN apt-get update
RUN apt install -y tzdata
RUN apt install -y arping inetutils-ping libnet-ifconfig-wrapper-perl mc


FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["tuya_mqtt.net.csproj", "."]
ENV DOTNET_CLI_HOME=/tmp
RUN dotnet restore "tuya_mqtt.net.csproj"
COPY . .
WORKDIR "/src/."
ARG BUILDPLATFORM
ARG TARGETPLATFORM
ARG GIT_COMMIT
RUN if [ "$TARGETPLATFORM" = "linux/amd64" ]; then \
    RID=linux-x64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm64" ]; then \
    RID=linux-arm64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm/v7" ]; then \
    RID=linux-arm ; \
    fi \
    && echo "target $TARGETPLATFORM build $BUILDPLATFORM RID $RID" \
    && date > "/src/Resources/BuildDate.txt" \
    && echo $GIT_COMMIT >> "/src/Resources/BuildDate.txt" \
    && dotnet build "tuya_mqtt.net.csproj" -r $RID --self-contained -c Release -o /app/build

FROM build AS publish
ARG BUILDPLATFORM
ARG TARGETPLATFORM
RUN if [ "$TARGETPLATFORM" = "linux/amd64" ]; then \
    RID=linux-x64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm64" ]; then \
    RID=linux-arm64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm/v7" ]; then \
    RID=linux-arm ; \
    fi \
    && echo "target $TARGETPLATFORM build $BUILDPLATFORM RID $RID" \
    && dotnet publish "tuya_mqtt.net.csproj" -r $RID --self-contained false -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "tuya_mqtt.net.dll"]
