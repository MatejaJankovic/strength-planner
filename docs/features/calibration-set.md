# Kalibraciona serija

**Grana:** `feature/calibration-set`

Korak 8 plana posle revizije (6. 11. 2026), stavka A1. Odluka vlasnika: serija do otkaza za
glavna dizanja, srednji i napredni nivo.

## Problem

Svaka procena maksimuma u aplikaciji počiva na vežbačevoj proceni rezerve (RIR). Ta procena u
proseku greši za oko jedno ponavljanje, uz veliku razliku među vežbačima (Halperin i sar. 2022).
Serija do otkaza nema rezervu koju treba proceniti, pa je njena procena najčistija. Plan je
nikada nije tražio.

## Šta je urađeno

- **Pravilo (`CalibrationSet`, domen).** Prva serija je kalibraciona kad važi sve ovo:
  - nivo bloka je srednji ili napredni;
  - nedelja je poslednja trenažna u bloku, dakle poslednja koja nije deload. Ako je
    auto-deload oslobodio planirani, poslednja trenažna nedelja se pomera, a kalibracija s njom;
  - vežba je prva u treningu, složena, i podnosi opseg snage (`SuitsLowReps`);
  - na propisanoj težini serija do otkaza staje oko `RepRangeMax + TargetRir` ponavljanja, i to
    mora biti najviše 12. Iznad toga se procena ne uzima (`EpleyRepCap`), pa ne bi bilo šta da se
    izmeri.
- **Računa se pri čitanju, bez nove kolone.** Sve što pravilo pita već je zapisano (nivo bloka,
  nedelje, tip vežbe, opseg i RIR plana), pa oznaka prati i auto-deload koji nastane posle
  generisanja.
- **Ekran treninga.** `ExercisePlanDto.IsCalibration` dolazi iz `SessionService`. Kartica prve
  vežbe nosi napomenu „Kalibracija: ...", polje ponavljanja piše „do otkaza" umesto cilja,
  kvačica „Serija do otkaza" je unapred uključena, a napomena ispod unosa za tu seriju ćuti: bez
  toga bi 7 ponavljanja do otkaza na opsegu 3-6 dobilo upozorenje o otkazu na vrhu opsega. Posle
  upisane prve serije sve se vraća na plan.
- **Granice volumena.** Udeo otkaza je za njih znak umora (MRV pada od 0.25 naviše).
  Kalibraciona serija ulazi u zbir serija, ali ne i u otkaze. Odstupanje RIR-a se ionako računa
  samo nad dovršenim serijama (runda 11).
- **Procena.** Bez nove putanje: završen trening upisuje najbolju procenu iz svojih serija kao
  `Estimated`, kao i do sada. Kalibracija **ne nadjačava** ostale procene, isto kao test-serija
  u rundi 12. Poslednja trenažna nedelja je najumornija u bloku, pa bi niža procena iz umora
  inače spustila start sledećeg bloka. Viša procena (vežbač je potcenjivao rezervu) ionako
  postaje najbolja u prozoru od 56 dana.

## Gde je ima, a gde nema

Poslednja trenažna nedelja po modelu, za glavnu složenu vežbu:

| Model | Hipertrofija | Snaga |
|---|---|---|
| ravan (nedelja 3) | 8-12 uz RIR 1 → oko 13, **nema** | 3-6 uz RIR 2 → oko 8, ima |
| linearan (nedelja 5) | 6-10 uz RIR 1 → oko 11, ima | ima |
| obrnut (nedelja 5) | 8-12 uz RIR 1 → oko 13, **nema** | ima |

Prvo sam očekivao da obrnut blok hipertrofije završava na RIR 0, pa bi stao u granicu. Test je
pokazao da nije tako: ciljni RIR namerno ne ide ispod 1 (`Periodization.MinRir`), jer bi inače
nestao najveći signal ocene umora. Čarobnjak za svaki blok predlaže linearan model (#90), pa
kalibraciju dobija podrazumevani izbor.

Nedelje umora posle poslednje trenažne nema šta da odlučuje (sledeća je deload, ili je nema),
pa ocena umora nije menjana.

## Šta je usput pronađeno

- **Ekran treninga bi pao za svaki trening.** Prva verzija je nedelje bloka čitala kao
  `Include` od nedelje do bloka i nazad do njegovih nedelja. To je ciklus, i EF ga u upitu bez
  praćenja odbija izuzetkom. Našao ga je servisni test pre provere uživo. Nedelje se sada čitaju
  posebnim upitom.
- **Upit granica volumena nije mogao u test nad bazom.** `SelectMany` preko mišića vežbe SQLite
  prevodi u APPLY, koji ne podržava. Upit je prepisan kao `Join`, istog značenja, i sada ga
  pokriva test.

## Provera

- `dotnet test`: **913** (bilo 893); `npm test`: **213** (bilo 209); `npm run build` prolazi, a
  `workout-session.scss` je ostao na 7.48 kB (bez novih stilova).
- Merenje vraćanjem, jedna izmena po merenju:

| Vraćeno | Obara |
|---|---|
| kalibracija i za početnika | 2 |
| bez granice od 12 ponavljanja | 4 |
| kalibracioni otkaz opet u udelu otkaza | 1 |
| oznaka u DTO-u ekrana uvek netačna | 1 (pre servisnog testa 0) |

- Uživo, na 375 px, nalog za testiranje, novi blok snage (Upper/Lower, ravan, srednji nivo),
  trening Upper A treće nedelje:
  - napomena stoji samo na Bench Press-u, ne i na Barbell Row-u;
  - polje piše „Ponavljanja · do otkaza", a kvačica je uključena;
  - 7 ponavljanja do otkaza ne daje napomenu ispod unosa;
  - posle upisa nema napomene o kalibraciji, polje piše „cilj 6", a kvačica je isključena;
  - nema prelivanja.

## Šta ovo ne rešava

- Kalibracija je preporuka. Ako vežbač prvu seriju ne odradi do otkaza, upisuje se kao obična
  serija sa svojim RIR-om, i ništa se ne kvari.
- Ravan i obrnut blok hipertrofije je nemaju. Kalibracija na težoj, posebno propisanoj težini bi
  to rešila, ali je to novi propis, a ne očitavanje postojećeg.
