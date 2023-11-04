FROM --platform=$BUILDPLATFORM mcr.microsoft.com/nightly/aspnet:8.0 AS base
ENV DOTNET_CLI_HOME=/tmp
WORKDIR /app
EXPOSE 80


ENV TZ=EUROPE/Berlin
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone
HEALTHCHECK CMD curl --fail http://localhost:80/health || exit
RUN apt-get update && \
    apt install -y curl

RUN if [ "$DEBUGHELPER" = "yes" ] ; then \
    apt install -y tzdata && \
    apt install -y arping inetutils-ping libnet-ifconfig-wrapper-perl mc \
    ; fi


FROM --platform=$BUILDPLATFORM mcr.microsoft.com/nightly/sdk:8.0 AS build
WORKDIR /src

COPY ["tuya_mqtt.net.csproj", "."]
ENV DOTNET_CLI_HOME=/tmp
RUN dotnet --info
RUN dotnet restore -a $TARGETARCH "tuya_mqtt.net.csproj" 
COPY . .
WORKDIR "/src/."

ARG DEBUGHELPER
ARG TARGETPLATFORM
ARG TARGETOS
ARG TARGETARCH
ARG TARGETVARIANT
ARG BUILDPLATFORM
ARG BUILDOS
ARG BUILDARCH
ARG BUILDVARIANT
ARG GIT_COMMIT

RUN echo "Building on $BUILDPLATFORM, targeting $TARGETPLATFORM"
RUN echo "Building on ${BUILDOS} and ${BUILDARCH} with optional variant ${BUILDVARIANT}"
RUN echo "Targeting ${TARGETOS} and ${TARGETARCH} with optional variant ${TARGETVARIANT}"

RUN if [ "$TARGETPLATFORM" = "linux/amd64" ]; then \
		RID=linux-x64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm64" ]; then \
		RID=linux-arm64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm/v7" ]; then \
		RID=linux-arm ; \
    elif [ "$TARGETPLATFORM" = "linux/arm/v6" ]; then \
		RID=linux-arm ; \
    fi \
    && date > "/src/Resources/BuildDate.txt" \
    && echo $GIT_COMMIT >> "/src/Resources/BuildDate.txt" \
    && dotnet build "tuya_mqtt.net.csproj" -a $TARGETARCH  --self-contained false -r $RID -c Release -o /app/build

FROM --platform=$BUILDPLATFORM build AS publish
ARG TARGETPLATFORM
ARG TARGETOS
ARG TARGETARCH
ARG TARGETVARIANT
ARG BUILDPLATFORM
ARG BUILDOS
ARG BUILDARCH
ARG BUILDVARIANT
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
    && dotnet publish "tuya_mqtt.net.csproj" --self-contained false -r $RID -c Release -a $TARGETARCH --no-restore -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "tuya_mqtt.net.dll"]
