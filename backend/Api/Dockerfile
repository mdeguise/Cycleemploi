FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["TremblantLifecycle.Api.csproj", "./"]
RUN dotnet restore "TremblantLifecycle.Api.csproj"
COPY . .
RUN dotnet publish "TremblantLifecycle.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# Attachment storage bind mount target — see docker-compose.yml and appsettings.json's
# AttachmentStorage:BasePath (Phase 2, but the mount point is prepared now).
RUN mkdir -p /app/attachments
EXPOSE 8080
ENTRYPOINT ["dotnet", "TremblantLifecycle.Api.dll"]
