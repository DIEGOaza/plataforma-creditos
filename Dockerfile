# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restaurar primero para aprovechar la caché de capas de Docker.
COPY ["PlataformaCreditos/PlataformaCreditos.csproj", "PlataformaCreditos/"]
RUN dotnet restore "PlataformaCreditos/PlataformaCreditos.csproj"

COPY . .
WORKDIR /src/PlataformaCreditos
RUN dotnet publish "PlataformaCreditos.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "PlataformaCreditos.dll"]
