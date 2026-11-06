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
| `BlockExperienceLevelTests` | blok se sudi po nivou sa kojim je generisan, a ne po profilu promenjenom usred bloka (#74) |

`MacrocycleService` dobija lažni generator: on beleži šta mu je poslato i upisuje prazan
mezociklus, jer blok na njega pokazuje stranim ključem.

## Šta je merenje pokazalo

Prva verzija testa za #101 je stavila ugrađeni blok na prvo mesto, i **vraćanje pravila pri
pravljenju bloka je ostalo zeleno**. Prvi blok se generiše odmah, a upis nazad pri generisanju
ga ispravi i bez tog pravila. Pravilo pri pravljenju štiti samo blokove koji čekaju red. Sada
ugrađeni blok stoji drugi, na čekanju.

## Provera

- `dotnet test`: **865** (bilo 860). Klijent 204, bez izmena.
- Merenje vraćanjem, svaka izmena sama, uz rebuild posle svakog vraćanja:

| Vraćeno | Obara |
|---|---|
| oznaka u `GetCurrentAsync` (#99) | 1 od 865 |
| pravilo pri pravljenju bloka (#101) | 1 od 865 (na prvoj verziji testa 0) |
| upis nazad pri generisanju (#101) | 1 od 865 |
| nivo iz profila umesto sa bloka (#74) | 1 od 865 |

## Šta ostaje

- `BlockExperienceLevel` je pokriven, ali ne i to da ga `VolumeLandmarkService` i
  `DeloadService` zaista zovu. Za to je potrebna cela odrađena nedelja sa serijama. Sada postoji
  harness u kome se ti testovi mogu napisati.
- Filteri vlasništva (EF `HasQueryFilter`) na SQLite-u rade isto kao na PostgreSQL-u, ali
  ponašanje specifično za PostgreSQL (tipovi kolona, poređenje datuma sa vremenskom zonom) ovaj
  harness ne vidi. Za to ostaje provera uživo.
