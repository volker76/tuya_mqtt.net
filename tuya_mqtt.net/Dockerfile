FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
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


FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/sdk:7.0 AS build
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
ENV DOTNET_CLI_HOME=/tmp

WORKDIR /src

COPY ["tuya_mqtt.net.csproj", "."]

RUN dotnet --info

COPY . .
WORKDIR "/src/."

RUN if [ "$TARGETPLATFORM" = "linux/amd64" ]; then \
		RID=linux-x64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm64" ]; then \
		RID=linux-arm64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm/v7" ]; then \
		RID=linux-arm ; \
    elif [ "$TARGETPLATFORM" = "linux/arm/v6" ]; then \
		RID=linux-arm ; \
    fi \
	&& echo "Building on $BUILDPLATFORM, targeting $TARGETPLATFORM" \
	&& echo "Building on ${BUILDOS} and ${BUILDARCH} with optional variant ${BUILDVARIANT}" \
	&& echo "Targeting ${TARGETOS} and ${TARGETARCH} with optional variant ${TARGETVARIANT}" \
	&& echo "RID is ${RID}" \
	&& dotnet restore -r $RID  "tuya_mqtt.net.csproj" \
    && date > "/src/Resources/BuildDate.txt" \
    && echo $GIT_COMMIT >> "/src/Resources/BuildDate.txt" \
    && dotnet build "tuya_mqtt.net.csproj" --no-restore -r $RID -c Release -o /app/build

FROM --platform=$TARGETPLATFORM build AS publish
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

RUN if [ "$TARGETPLATFORM" = "linux/amd64" ]; then \
		RID=linux-x64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm64" ]; then \
		RID=linux-arm64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm/v7" ]; then \
		RID=linux-arm ; \
    elif [ "$TARGETPLATFORM" = "linux/arm/v6" ]; then \
		RID=linux-arm ; \
    fi \
	&& echo "Publishing on $BUILDPLATFORM, targeting $TARGETPLATFORM" \
	&& echo "Publishing on ${BUILDOS} and ${BUILDARCH} with optional variant ${BUILDVARIANT}" \
	&& echo "Targeting ${TARGETOS} and ${TARGETARCH} with optional variant ${TARGETVARIANT}" \
	&& echo "RID is ${RID}" \
    && dotnet publish "tuya_mqtt.net.csproj" -r $RID -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM --platform=$TARGETPLATFORM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "tuya_mqtt.net.dll"]
