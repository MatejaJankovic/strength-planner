# Adaptivne MEV/MRV granice

**Grana:** `feature/adaptive-volume-landmarks`

## Problem iz rada

Zaključak navodi: *"Волуменске границе MEV и MRV су статичке вредности по мишићној
групи, иако се стварна толеранција волумена разликује од корисника до корисника."*

Seed tabela daje jedan par brojeva po mišićnoj grupi za sve — Chest 10/22, Back 10/25 i
tako dalje. To su populacioni proseci: koristan početak, ali za konkretnog vežbača mogu
biti daleko od istine. Sistem pritom već ima sve podatke da to sam primeti — zna koliko
je serija odrađeno, koliko je rezerve ostajalo i koliko je serija otkazalo.

## Rešenje

Uz seed tabelu stoji **lična** tabela granica (`UserVolumeLandmarks`). Posle svake
završene nedelje koja nije deload, svaka mišićna grupa se oceni i granice se pomere
**najviše za jednu seriju**.

Nedelja se po mišićnoj grupi sažima u tri broja (`VolumeResponse`), sve mereno
doprinosom serije (primarna 1.0, sekundarna 0.5), pa sekundarni rad ne broji kao pun:

- `PerformedSets` — nedeljne radne serije
- `AverageRirDeviation` — prosek `(efektivni RIR − ciljni RIR)`; koristi efektivni RIR
  uveden uz [otkaze](failed-reps-logging.md), pa promašena ponavljanja ulaze kao minus
- `FailureShare` — udeo serija izvučenih do otkaza

Pravila (`VolumeAdaptation.Adjust`):

| Uslov | Pomeraj |
|---|---|
| Volumen ≥ 90% MRV **i** ostajala rezerva (odstupanje ≥ 0, bez otkaza) | MRV +1 |
| Volumen ≥ MEV **i** umor (odstupanje ≤ −1 **ili** ≥ 25% otkaza) | MRV −1 |
| Volumen ≤ MEV **i** bilo lako (odstupanje ≥ +1) | MEV +1 |
| Volumen ≤ MEV **i** umor | MEV −1 |

Dva ograničenja koja drže stvar u razumnim okvirima:

- **Korak od jedne serije nedeljno.** Signal je bučan (san, stres, ishrana); granica
  koja bi jurila jednu lošu nedelju ne bi bila pouzdanija od statičke vrednosti koju
  zamenjuje.
- **±50% od seed vrednosti.** Lične granice smeju da odlutaju, ali ne u besmislicu;
  MRV za Chest se kreće između 11 i 33.

Uz to, MEV nikada ne sme da pojede optimalni pojas: između granica se drži razmak od
bar dve serije, i u algoritmu i kao `CHECK` ograničenje u bazi.

Namerno **ne** zaključujem o gornjoj granici iz nedelje odrađene daleko ispod nje —
lakoća na 12 serija ne dokazuje da bi i 22 bile podnošljive. Isto tako, o minimalnoj
dozi se sudi samo kada je nedelja i bila na toj dozi.

## Šta je urađeno

- `VolumeAdaptation`, `VolumeResponse`, `VolumeLandmarkValues` — čist domenski deo,
  bez EF-a i DTO-ova, potpuno pokriven testovima.
- `UserVolumeLandmark` — nova tabela sa jedinstvenim indeksom `(UserId, MuscleGroupId)`,
  kaskadnim FK ka nalogu i `CHECK` ograničenjem `Mev >= 1 AND Mrv > Mev`.
- `VolumeLandmarkService` — čita efektivne granice (lične ili seed) i sažima završenu
  nedelju u `VolumeResponse` po mišićnoj grupi.
- `SessionService.CompleteAsync` pokreće adaptaciju u trenutku kada poslednja sesija
  nedelje pređe u `Completed`. Taj prelaz se dešava tačno jednom (završena sesija se ne
  može ponovo završiti), pa se nedelja ne može dvaput obračunati. Sve je u istoj
  transakciji kao i završetak sesije.
- Deload nedelje se preskaču — namerno su submaksimalne i o toleranciji ne govore ništa.
- `WeeklyVolumeDto` nosi i `defaultMev`/`defaultMrv`/`isPersonal`, pa ekran može da
  pokaže odakle je granica došla.
- `POST /api/analytics/volume/landmarks/reset` briše naučene granice.
- Ekran "Volumen": uz svaku pomerenu granicu stoji oznaka *lično (podrazumevano 10/22)*,
  ispod je objašnjenje koliko je grupa prilagođeno i dugme za povratak na podrazumevano.

## Provera

- `dotnet build`, `dotnet test` (69 testova, bilo 58), `npm run build` — sve prolazi.
- `VolumeAdaptationTests` pokriva svako pravilo iz tabele, korak od najviše jedne serije,
  oba kraja ±50% pojasa, očuvanje optimalnog pojasa i konvergenciju (dvadeset istih
  dobrih nedelja staje na 33, ne beži dalje).
- Baza posle migracije: jedinstveni indeks, oba FK i `CHECK` ograničenje potvrđeni
  kroz `\d "UserVolumeLandmarks"`.
- End-to-end, cela nedelja odrađena lagano (RIR 4 uz cilj 1, volumen ispod MEV):
  Chest MEV **10 → 11**, ostale grupe netaknute jer im volumen nije bio na granici.
- End-to-end, cela nedelja izvučena do otkaza tri ponavljanja ispod opsega: MRV spušten
  za Back, Biceps, Glutes, Hamstrings, Shoulders i Triceps (grupe iznad MEV), a MEV
  spušten za Abs, Calves, Chest i Quads (grupe ispod MEV) — svaka tačno za jednu seriju.
- Reset kroz API i kroz dugme vraća sve na seed vrednosti.
- U pretraživaču: deset grupa označeno kao "lično" sa podrazumevanim vrednostima u
  zagradi, tekst "Granice za 10 mišićnih grupa su prilagođene…", a posle klika na
  "Vrati podrazumevane granice" oznake nestaju i dugme se skloni.
