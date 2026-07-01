# StrengthPlanner

Veb aplikacija za planiranje i praćenje treninga snage (diplomski rad).
Single-user "basic trener": korisnik loguje svoje treninge, a sistem predlaže
kako da napreduje sledeći trening (progresivno opterećenje + auto-regulacija).

## Tehnološki stek

- **Backend:** ASP.NET Core Web API (.NET 8, C#)
- **Baza:** PostgreSQL
- **ORM:** Entity Framework Core (Npgsql)
- **Auth:** ASP.NET Core Identity + JWT
- **Frontend:** Angular standalone komponente (`@if` / `@for`)

## Arhitektura (clean / onion)

```
src/
  StrengthPlanner.Domain          # entiteti + algoritmi (jezgro, bez zavisnosti)
  StrengthPlanner.Application      # use-case servisi, DTO-ovi, interfejsi  -> Domain
  StrengthPlanner.Infrastructure   # EF Core, repozitorijumi, Identity, JWT -> Application, Domain
  StrengthPlanner.API              # kontroleri, DI, Program.cs             -> Application, Infrastructure
```

Zavisnosti idu ka unutra: `API → Infrastructure → Application → Domain`.
`Domain` nema nijednu referencu.

## Preduslovi

- .NET SDK 8.x (repo sadrži `global.json` koji pinuje SDK na `8.0.x`)
- PostgreSQL za lokalni razvoj bez Docker-a
- Docker Desktop za pokretanje celog sistema jednom komandom

## Pokretanje

```bash
# iz korena repozitorijuma
dotnet build

# pokretanje API-ja
dotnet run --project src/StrengthPlanner.API
```

Swagger UI je dostupan u Development okruženju na `/swagger`
(npr. `https://localhost:<port>/swagger`; tačan port je u
`src/StrengthPlanner.API/Properties/launchSettings.json`).

## Pokretanje za odbranu

Iz korena projekta pokreni:

```bash
docker compose up --build
```

Zatim otvori `http://localhost:8080`.

Pri prvom pokretanju PostgreSQL kontejner inicijalizuje bazu, API sačeka da baza
postane dostupna, automatski primeni EF migracije i izvrši seed podataka. Primer
podešavanja je u `.env.example`; `docker-compose.yml` već ima lokalne default
vrednosti, pa je komanda iznad dovoljna za lokalnu odbranu.

## Status

Sistem sadrži backend, PostgreSQL perzistenciju, seed podatke, Angular frontend i
Docker Compose konfiguraciju za lokalno pokretanje baze, API-ja i web aplikacije.
