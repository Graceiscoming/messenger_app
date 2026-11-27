# syntax=docker/dockerfile:1
# Build context = repository root (see docker-compose.yml: context: ., dockerfile: src/Dockerfile)

# ---------- restore (cached layer: only project files) ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src
COPY global.json ./
COPY src/Messenger.Api/Messenger.Api.csproj src/Messenger.Api/
COPY src/Messenger.Domain/Messenger.Domain.csproj src/Messenger.Domain/
COPY src/Messenger.Data/Messenger.Data.csproj src/Messenger.Data/
RUN dotnet restore src/Messenger.Api/Messenger.Api.csproj

# ---------- publish ----------
FROM restore AS publish
COPY src/ src/
RUN dotnet publish src/Messenger.Api/Messenger.Api.csproj \
    --no-restore -c Release -o /app/publish /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Storage__UploadPath=/data/uploads
RUN mkdir -p /data/uploads && chown -R app:app /data
COPY --from=publish /app/publish .
USER app
EXPOSE 8080
VOLUME ["/data/uploads"]
ENTRYPOINT ["dotnet", "Messenger.Api.dll"]
