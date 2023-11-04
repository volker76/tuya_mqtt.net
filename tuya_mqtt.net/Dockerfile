FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
ENV DOTNET_CLI_HOME=/tmp
WORKDIR /app
EXPOSE 80
ARG DEBUGHELPER
ENV TZ=EUROPE/Berlin
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone
HEALTHCHECK CMD curl --fail http://localhost:80/health || exit
RUN apt-get update && \
    apt install -y curl

RUN if [ "$DEBUGHELPER" = "yes" ] ; then \
    apt install -y tzdata && \
    apt install -y arping inetutils-ping libnet-ifconfig-wrapper-perl mc \
    ; fi

FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["tuya_mqtt.net.csproj", "."]
ENV DOTNET_CLI_HOME=/tmp
RUN dotnet --info
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
    elif [ "$TARGETPLATFORM" = "linux/arm/v6" ]; then \
		RID=linux-arm ; \
    fi \
    && echo "target $TARGETPLATFORM build $BUILDPLATFORM RID $RID" \
    && date > "/src/Resources/BuildDate.txt" \
    && echo $GIT_COMMIT >> "/src/Resources/BuildDate.txt" \
    && dotnet build "tuya_mqtt.net.csproj" --self-contained false -r $RID -c Release -o /app/build

FROM --platform=$TARGETPLATFORM build AS publish
ARG BUILDPLATFORM
ARG TARGETPLATFORM
RUN if [ "$TARGETPLATFORM" = "linux/amd64" ]; then \
		RID=linux-x64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm64" ]; then \
		RID=linux-arm64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm/v7" ]; then \
		RID=linux-arm ; \
    elif [ "$TARGETPLATFORM" = "linux/arm/v6" ]; then \
		RID=linux-arm ; \
    fi \
    && echo "target $TARGETPLATFORM build $BUILDPLATFORM RID $RID" \
    && dotnet publish "tuya_mqtt.net.csproj" --self-contained false -r $RID -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "tuya_mqtt.net.dll"]
