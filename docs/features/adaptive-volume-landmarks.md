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
| Volumen ≥ 90% MRV **i** nedelja bila lakša od plana (odstupanje ≥ +1, do 12.5% otkaza) | MRV +1 |
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
- `SessionService.CompleteAsync` posle svake završene sesije obračuna svaku nedelju
  mezociklusa koja je u celosti odrađena a još nema `VolumeAdaptedAt`. Sam upis tog
  datuma je uslovni `UPDATE`, pa je obračun nedelje atomično preuzimanje: drugi zahtev
  dobija nula redova i preskače. Sve je u istoj transakciji kao i završetak sesije.
- Deload nedelje se preskaču — namerno su submaksimalne i o toleranciji ne govore ništa.
- `WeeklyVolumeDto` nosi i `defaultMev`/`defaultMrv`/`isPersonal`, pa ekran može da
  pokaže odakle je granica došla.
- `POST /api/analytics/volume/landmarks/reset` briše naučene granice.
- Ekran "Volumen": uz svaku pomerenu granicu stoji oznaka *lično (podrazumevano 10/22)*,
  ispod je objašnjenje koliko je grupa prilagođeno i dugme za povratak na podrazumevano.

## Provera

- `dotnet build`, `dotnet test` (74 testa, bilo 58), `npm run build` — sve prolazi.
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

## Ispravke posle revizije koda

### MEV je mogao trajno da zaglavi

Kada bi se MRV spustio do svoje donje ivice, pojas između granica bi se stisnuo, a
zaštita pojasa je tada obarala **MEV** — iako o donjoj granici ta nedelja nije rekla
ništa (volumen je bio znatno iznad nje). Pošto se MEV posle diže samo kada je nedelja
odrađena *na* njemu, oboren MEV bi ostao zaglavljen i pošto se pojas ponovo otvori.
Trag: petnaest teških nedelja na 20 serija obori Chest MEV sa 10 na 9, i tu ostane
zauvek. Sada se pojas prvo širi podizanjem MRV-a, a MEV se dira samo kada je MRV
stvarno na svojoj granici. Dodat regresioni test sa nizom teških pa lakih nedelja.

### Prag je važio samo u jednom smeru

Za spuštanje granice tražio se ceo RIR poen odstupanja, a za dizanje je bilo dovoljno
`>= 0`. Zbog toga je MRV mogao da poraste i posle nedelje koja je u proseku bila
**teža** od plana (npr. odstupanja +0.2 / −0.5, prosek −0.15). Sada isti prag važi u
oba smera.

### Jedan otkaz je zauvek zamrzavao MRV

Uslov za dizanje je bio "nijedan otkaz". Pošto je poslednja serija do otkaza sasvim
uobičajena praksa — a otkazi su tek uvedeni u prethodnoj grani — takvom vežbaču gornja
granica ne bi mogla nikada da poraste, jer ni uslov za spuštanje (≥ 25% otkaza) nije
ispunjen. Uveden je prag tolerancije od 12.5%.

### Algoritam je mogao da vrati vrednost koju baza odbija

`Math.Max(1, mev)` se primenjivao **posle** sužavanja pojasa, pa je mogao da vrati
`Mev == Mrv` i time oborio `CHECK` ograničenje usred završavanja treninga — što bi
srušilo celu transakciju, uključujući i upis samog treninga. Sa isporučenim seed
vrednostima nije bilo dostižno (najmanji MRV je 16), ali jeste čim bi neki seed imao
`Mrv <= 2`. Dodat test koji vrti pet seed vrednosti i pet ekstremnih odgovora kroz
četrdeset nedelja i traži da pojas nikada ne padne.

### Nedelja je mogla da se obračuna dvaput ili nijednom

Oslanjanje na prelaz "poslednja sesija postaje Completed" nije bilo dovoljno:

- **Dvaput:** dva istovremena zahteva za završetak iste sesije oba prođu provera
  statusa (čitanje bez zaključavanja pod READ COMMITTED), pa oba vide nedelju kao
  gotovu i pomere granice za po jednu seriju — ukupno dve.
- **Nijednom:** ako dva zahteva istovremeno završe **poslednje dve različite** sesije
  nedelje, nijedan ne vidi onu drugu kao završenu, pa nedelja ostane neobračunata
  zauvek.

Rešeno na dva mesta. `TrainingWeek.VolumeAdaptedAt` se upisuje uslovnim `UPDATE`-om,
što je istovremeno i zaključavanje i provera — drugi zahtev dobija nula redova i
preskoči. I umesto samo nedelje kojoj pripada završena sesija, obrađuje se **svaka**
nedelja mezociklusa koja je gotova a još nije obračunata, pa propuštena nedelja bude
pokupljena pri sledećem završetku.

### Usput nađena starija greška: dvostruko završavanje treninga

Ista trka postoji i u samom `CompleteAsync`, nezavisno od ove funkcionalnosti: dva
istovremena zahteva su oba vraćala 200 i **oba** upisivala e1RM zapis (istorija dobije
duplikat, koji onda kvari PR listu i trend). Provereno: `e1RM zapisa: 1 -> 3`. Pošto
ova funkcionalnost počiva na tome da se sesija završava tačno jednom, sesija se sada
preuzima istim uslovnim `UPDATE`-om; drugi zahtev dobija 409. Posle ispravke:
`statusi: [200, 409]`, `e1RM zapisa: 1 -> 2`.

### Frontend

- Reset je destruktivan a bio je jedan klik bez potvrde i bez ikakve poruke — jedini
  trag je bio nestanak oznaka. Sada je potvrda u dva koraka, po uzoru na brisanje plana
  na dashboardu, uz poruku o uspehu.
- Osvežavanje posle reseta je rušilo ceo ekran u "Učitavam volumen…" jer je `startWith`
  važio i za ponovno učitavanje. Sada spinner ide samo na promenu mezociklusa ili
  nedelje; provereno da se lista ne isprazni tokom reseta i da se spinner i dalje
  prikazuje pri promeni nedelje.
- `personalCount` je bio deklarisan iznad `rows` koji koristi; radilo je samo zato što
  je `computed` lenj. Premešten ispod.
