# Test-harness za servise

**Grana:** `test/service-harness`

Drugi korak plana posle revizije (6. 11. 2026), stavka R4.

## Problem

Runde 12 i 16 su tri puta naišle na istu rupu: pravilo koje živi u upitu ili na mestu poziva u
servisu moglo je da se vrati uz ceo paket zelen, jer nijedan test nije pravio servis.

- #74, nivo iskustva zaključan na bloku: vraćanje je oborilo **0 od 613**.
- #99, oznaka starog maksimuma u `OneRepMaxService.GetCurrentAsync`: proverena samo uživo.
- #101, „Prati moj šablon" samo uz lični šablon, na dva mesta u `MacrocycleService`: provereno
  samo uživo.

## Šta je urađeno

`tests/StrengthPlanner.Tests/ServiceHarness/SqliteDatabase.cs` pravi pravi `AppDbContext` nad
SQLite bazom u memoriji, sa jednim nalogom koga čitaju filteri vlasništva. SQLite, a ne EF-ov
InMemory provajder, jer servisi koriste `ExecuteUpdate`, `ExecuteDelete` i transakcije, koje
InMemory ne podržava. Paket `Microsoft.EntityFrameworkCore.Sqlite` 8.0.10 je dodat samo test
projektu, u istoj verziji kao ostatak EF Core-a.

Prvi testovi:

| Test | Šta drži |
|---|---|
| `OneRepMaxServiceTests` | maksimum stariji od 56 dana nosi `IsOutsideLookback`, a svež ne; test-serija ga skida (#99) |
| `MacrocycleAllocationTests` | ugrađeni blok se upisuje balansiran, a lični zadržava izbor; blok upisan pre pravila se generiše balansiran, i ispravka se upisuje nazad (#101) |
| `BlockExperienceLevelTests` | pomoćna klasa i pozivalac `VolumeLandmarkService`: blok se sudi po nivou sa kojim je generisan, a ne po profilu promenjenom usred bloka (#74) |

`MacrocycleService` dobija lažni generator: on beleži šta mu je poslato i upisuje prazan
mezociklus, jer blok na njega pokazuje stranim ključem. Resolver šablona je pravi, nad ličnim
šablonom upisanim za nalog, pa test prolazi i proveru ključa i vlasništva.

## Šta je merenje pokazalo

Prva verzija testa za #101 je stavila ugrađeni blok na prvo mesto, i **vraćanje pravila pri
pravljenju bloka je ostalo zeleno**. Prvi blok se generiše odmah, a upis nazad pri generisanju
ga ispravi i bez tog pravila. Pravilo pri pravljenju štiti samo blokove koji čekaju red. Sada
ugrađeni blok stoji drugi, na čekanju.

## Posle pregleda

- **Paket koji bi oborio CI.** EF Sqlite vuče `SQLitePCLRaw.lib.e_sqlite3` 2.1.6, sa objavljenom
  ranjivošću (GHSA-2m69-gcr7-jv3q), a provera zavisnosti u CI-ju pada na svaki nalaz, i u test
  projektu. Verzija je podignuta na 2.1.14, uz komentar kao kod ostalih takvih referenci.
- **#74 je bio prikazan kao zatvoren, a nije.** Prvi test je pokrivao samo telo pomoćne klase.
  Pregled je izmerio da vraćanje prave ispravke iz #74 - pozivaoci opet čitaju profil - ostaje
  zeleno, kao i izbacivanje upisa nivoa u generatoru. Dodat je test kroz
  `VolumeLandmarkService.GetEffectiveAsync`; `DeloadService` i generator su navedeni pod
  „Šta ostaje".
- Lažni resolver koji prihvata svaki ključ je zamenjen pravim.

## Provera

- `dotnet test`: **866** (bilo 860). Klijent 204, bez izmena.
- Merenje vraćanjem, svaka izmena sama, uz rebuild posle svakog vraćanja:

| Vraćeno | Obara |
|---|---|
| oznaka u `GetCurrentAsync` (#99) | 1 |
| pravilo pri pravljenju bloka (#101) | 1 (na prvoj verziji testa 0) |
| upis nazad pri generisanju (#101) | 1 |
| telo `BlockExperienceLevel` (nivo iz profila) (#74) | 2 |
| `VolumeLandmarkService` čita profil umesto bloka (#74) | 1 |
| `DeloadService` čita profil umesto bloka (#74) | 0 |
| generator ne upisuje nivo na blok (#74) | 0 |

## Šta ostaje

- Za #74 nisu pokriveni pozivalac `DeloadService` i upis nivoa u `MesocycleGenerator`: vraćanje
  svakog obara 0 testova. Prvi traži odrađenu nedelju sa serijama, a drugi pravi generator.
- Razlike SQLite-a u odnosu na PostgreSQL, koje harness ne vidi:
  - `HasPrecision(6,2)` se ne primenjuje: 66.6666 se čita kao 66.6666, a PostgreSQL bi vratio
    66.67. Test koji proverava upisanu procenu može da se razlikuje od produkcije.
  - `DateTime` se čita sa `Kind=Unspecified` (Npgsql vraća Utc), a SQLite prihvata i vreme koje
    nije UTC, koje Npgsql 8 odbija za `timestamptz`.
  - `Max` i `OrderBy` nad decimalom bacaju `NotSupportedException`: budući test nad težinama će
    pući glasno, ne lažno proći.
  - `EnsureCreated` ne izvršava `Sql(...)` iz migracija ni seeder, pa su katalog i granice
    prazni dok ih test ne upiše.
  - Strani ključevi se proveravaju (`PRAGMA foreign_keys=1`), kao i na PostgreSQL-u.
- Nativna SQLite biblioteka se ne učitava ako je putanja do repozitorijuma predugačka
  (`DllNotFoundException ... 0x800700CE`). Do sada je ceo paket bio čist upravljani kod.
