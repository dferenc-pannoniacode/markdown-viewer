FROM --platform=$BUILDPLATFORM node:24-alpine AS assets
WORKDIR /assets
COPY src/MdViewer/package.json src/MdViewer/package-lock.json ./
RUN npm ci --ignore-scripts --no-audit --no-fund

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:11.0 AS build
ARG TARGETARCH
WORKDIR /source
COPY src/Directory.Build.props src/
COPY src/MdViewer/MdViewer.csproj src/MdViewer/
COPY src/MdViewer.Client/MdViewer.Client.csproj src/MdViewer.Client/
RUN dotnet restore src/MdViewer/MdViewer.csproj -a "$TARGETARCH"
COPY src/ src/
COPY docs/ docs/
COPY --from=assets /assets/node_modules src/MdViewer/node_modules/
RUN dotnet publish src/MdViewer/MdViewer.csproj -c Release -a "$TARGETARCH" --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:11.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "MdViewer.dll"]
