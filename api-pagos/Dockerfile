# syntax=docker/dockerfile:1

# ---- Etapa de build (Windows Containers) ----
FROM mcr.microsoft.com/dotnet/sdk:8.0-nanoserver-ltsc2022 AS build
WORKDIR /src
COPY ApiPagos.csproj .
RUN dotnet restore ApiPagos.csproj
COPY . .
RUN dotnet publish ApiPagos.csproj -c Release -o /app/publish --no-restore

# ---- Etapa final ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0-nanoserver-ltsc2022 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ApiPagos.dll"]
