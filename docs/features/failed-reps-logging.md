# Bележење неуспелих понављања (serije do otkaza)

**Grana:** `feature/failed-reps-logging`

## Problem iz rada

Zaključak navodi: *"пошто RIR скала стаје на нули, корекција наниже је у пракси знатно
ужа од корекције навише (при циљном RIR 1 највише 3% по тренингу), па систем спорије
реагује на претешке тренинге него на прелаке."*

Uzrok je asimetrija skale. Korekcija je `(prosečan RIR − ciljni RIR) × 3%`, ograničena
na ±10%:

- **Naviše** je prostor širok: pri cilju RIR 1, serija sa RIR 5 daje +4 poena → +12%,
  odsečeno na +10%. Trening je bio prelak i sistem to odmah ispravi.
- **Naniže** skala staje na nuli: najgori mogući signal je RIR 0, to jest −1 poen → **−3%**.

Posledica je da je vežbač koji je zakucao u zid morao tri do četiri treninga da čeka
da opterećenje spadne na održiv nivo, dok se prelak trening ispravljao iz prve.

## Rešenje

Serija dobija zastavicu "do otkaza". Otkaz nije isto što i RIR 0: RIR 0 znači *"jedva
sam izvukao poslednje ponavljanje, ali jesam"*, a otkaz znači *"nisam mogao više"*.
Kada je serija otkazala **ispod** donje granice rep-opsega, svako promašeno ponavljanje
se broji kao jedan RIR poen naniže:

```
efektivni RIR = otkaz ? −max(0, repRangeMin − odrađena ponavljanja) : uneti RIR
```

Time skala postaje simetrična. Pri cilju RIR 1 i opsegu 8–12:

| Situacija | Efektivni RIR | Odstupanje | Korekcija |
|---|---|---|---|
| RIR 5, prelako | +5 | +4 | +10% (odsečeno) |
| RIR 0, ali završeno | 0 | −1 | −3% |
| Otkaz na 8 (dno opsega) | 0 | −1 | −3% |
| Otkaz na 6 | −2 | −3 | −9% |
| Otkaz na 4 | −4 | −5 | −10% (odsečeno) |

Sada i korekcija naniže dostiže isti prag od 10% koji je korekcija naviše oduvek imala.

## Šta je urađeno

### Domen

- `WorkingSet` — nov record `(Reps, Rir, IsFailure)` sa metodom `EffectiveRir(repRangeMin)`.
  Zamenio je anonimnu torku `(int reps, int rir)` u potpisu progresije; time je otkaz
  postao deo modela, a ne još jedan paralelan parametar.
- `ProgressionEngine.ComputeNext` računa prosek **efektivnog** RIR-a i uz to više ne
  dodaje korak kada je vrh opsega dostignut isključivo otkazom: dvanaest ponavljanja
  izvučenih do otkaza nije isti signal kao dvanaest sa RIR 1.
- `SetLog.IsFailure` — nova kolona.

### Servisi

- `SetLogService` odbija kombinaciju "otkaz + RIR > 0" sa 400 i normalizuje RIR na 0
  pri upisu, pa u bazi ne može da postoji serija koja tvrdi i otkaz i rezervu.
- `SessionService` gradi `WorkingSet` iz logova, pa progresija vidi otkaze.
  e1RM ostaje nepromenjen: Epley ionako pretpostavlja seriju do otkaza, a RIR je 0.

### Frontend

- Prekidač "Serija do otkaza" na ekranu treninga. Dok je uključen, RIR dugmad su
  onemogućena (otkaz po definiciji nema rezervu).
- Pomoćni tekst broji promašena ponavljanja: *"Promašeno 3 ponavljanja do donje granice
  opsega — sledeći put predlažemo osetno manje opterećenje."*
- Odrađene serije sa otkazom nose oznaku **otkaz** umesto "RIR 0".
- Posle upisa prekidač se gasi. Težina, ponavljanja i RIR se pamte za sledeću seriju
  (postojeće ponašanje), ali otkaz ne — zaboravljena kvačica bi nemo spustila sledeći
  trening za do 10%, a korisnik ne bi imao razlog da je traži.

## Provera

- `dotnet build`, `dotnet test` (56 testova, bilo 46), `npm run build` — sve prolazi.
- Novi testovi: `FailedSetProgressionTests` pokriva mapiranje promašenih ponavljanja u
  negativan RIR, dostizanje praga od −10%, simetriju sa korekcijom naviše, i to da
  običan RIR 0 **ostaje** na −3% (nije promenjeno ponašanje za završene serije).
- End-to-end kroz API, Bench Press sa početnih 100 kg (opseg 8–12, cilj RIR 1):
  - `{"isFailure": true, "rir": 3}` → **400**, "A set taken to failure cannot have reps in reserve."
  - tri otkaza na 5 ponavljanja → sledeća težina **90 kg (−10%)**
  - kontrolni nalog, tri serije po 8 ponavljanja sa RIR 0 bez otkaza → **97.5 kg (−2.5%)**
- U pretraživaču: prekidač gasi RIR dugmad, tekst tačno broji promašena ponavljanja
  (5 od 8 → "Promašeno 3"), serija se prikazuje sa oznakom "otkaz", a prekidač se
  posle upisa vraća na isključeno i RIR dugmad se ponovo omogućavaju.
