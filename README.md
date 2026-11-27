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

## Telefon u lokalnoj mreži (HTTPS)

Instalacija na početni ekran i „ekran se ne gasi" dok trening traje rade samo u bezbednom kontekstu. Računar `localhost` smatra bezbednim, a telefon adresu računara u mreži ne, pa je
potreban HTTPS sa sertifikatom kome telefon veruje. Ništa se ne preuzima: koristi se `openssl` koji dolazi
uz Git za Windows.

```bash
# u Git Bash-u (ne u PowerShell-u: tamo `bash` može da bude WSL, koji vidi drugu mrežnu adresu)
# jednom, i kad se promeni adresa računara: CA se pravi u ~/.strength-planner-ca, sertifikat u .certs/
scripts/local-https.sh

# dva razvojna servera, svaki u svom terminalu
dotnet run --project src/StrengthPlanner.API --launch-profile http
npm --prefix strength-planner-web run start:lan
```

Zatim jednom instaliraj `.certs/strength-planner-ca.crt` na telefon kao pouzdan sertifikat
(koraci za Android i iPhone ispisuje skripta) i otvori `https://<adresa računara>:4200`.

Posle ponovnog pokretanja skripte treba ponovo pokrenuti `start:lan`: server čita sertifikat samo pri
pokretanju. Dok `start:lan` radi, aplikaciji (i API-ju kroz proksi, bez prijave) može da pristupi svaki
uređaj u istoj mreži: koristi se samo u poverljivoj mreži.

Telefon traži samo razvojni server aplikacije: on prosleđuje `/api` na API na računaru
(`strength-planner-web/proxy.conf.json`), pa API ne mora da bude dostupan sa telefona i nema CORS-a ni
mešovitog sadržaja. Obična komanda `npm start` ostaje kakva je bila. Detalji i ograničenja:
`docs/features/local-https.md`.

## Pokretanje za odbranu

Iz korena projekta pokreni:

```bash
docker compose up --build
```

Zatim otvori `http://localhost:8080`. Port je vezan za petlju, pa se aplikacija preko čistog
HTTP-a otvara samo sa iste mašine; za server vidi `docs/deployment-security.md`.

Pri prvom pokretanju PostgreSQL kontejner inicijalizuje bazu, API sačeka da baza
postane dostupna, automatski primeni EF migracije i izvrši seed podataka. Primer
podešavanja je u `.env.example`; `docker-compose.yml` već ima lokalne default
vrednosti, pa je komanda iznad dovoljna za lokalnu odbranu.

## Status

Sistem sadrži backend, PostgreSQL perzistenciju, seed podatke, Angular frontend i
Docker Compose konfiguraciju za lokalno pokretanje baze, API-ja i web aplikacije.
