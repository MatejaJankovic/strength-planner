# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json ./
COPY StrengthPlanner.sln ./
COPY src/StrengthPlanner.Domain/StrengthPlanner.Domain.csproj src/StrengthPlanner.Domain/
COPY src/StrengthPlanner.Application/StrengthPlanner.Application.csproj src/StrengthPlanner.Application/
COPY src/StrengthPlanner.Infrastructure/StrengthPlanner.Infrastructure.csproj src/StrengthPlanner.Infrastructure/
COPY src/StrengthPlanner.API/StrengthPlanner.API.csproj src/StrengthPlanner.API/

RUN dotnet restore src/StrengthPlanner.API/StrengthPlanner.API.csproj

COPY src/ src/

RUN dotnet publish src/StrengthPlanner.API/StrengthPlanner.API.csproj \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

# Bez ovoga kontejner radi kao root: proboj u aplikaciji odmah dobija root u kontejneru.
# Korisnik `app` postoji u zvaničnom aspnet imidžu.
USER app

ENTRYPOINT ["dotnet", "StrengthPlanner.API.dll"]
