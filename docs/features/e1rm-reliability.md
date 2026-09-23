# Procena maksimuma koja ne veruje svakoj seriji

Grana `fix/e1rm-reliability`. Treća od četiri grane iz pregleda logike treninga (odeljak A),
nalaz A7.

## Problem

e1RM se računao Epley formulom preko efektivnih ponavljanja (`ponavljanja + RIR`), a jedina
provera bila je da stvarnih ponavljanja nije više od 12. Serija daleko od otkaza je time
davala procenu koju niko ne bi potpisao:

| Serija | Procena |
|---|---|
| 100 kg × 12, do otkaza | 140 kg |
| 100 kg × 12 sa RIR 3 | 150 kg |
| 100 kg × 12 sa RIR 5 | **156.7 kg** |

Sam rad to i kaže u odeljku 4.2: procena rezerve je nepouzdana kada je rezerva velika, i
radne serije se planiraju na RIR 1–2. Kod je ipak primao svaku.

Druga polovina problema je što je takva vrednost **trajala**. Start novog bloka i preračun
opterećenja u periodizovanom bloku uzimaju **najbolju** procenu iz poslednjih 56 dana, pa je
jedna omašena serija osam nedelja bila polazna težina. Ručni unos nižeg maksimuma nije
pomagao: „najbolja u prozoru" ga je prosto ignorisala.

## Rešenje

### Koja serija sme da da procenu

`E1RmCalculator.CanEstimateFrom(loadKg, reps, rir)` — jedan predikat, tri uslova: mora da
postoji opterećenje, ponavljanja do Epley granice (12), i **RIR najviše 3**. Granica je
definisana kao `StimulativeVolume.FullCreditRir`, jer serija koja ne ulazi cela u
stimulativni volumen nije ni dokaz o snazi. Test to i zaključava, pa se dve granice ne mogu
razići.

Predikat čitaju oba mesta koja su do sada imala svoju verziju filtera: rezime treninga
(`SessionService`) i e1RM signal u oceni umora (`DeloadService`).

Kad nijedna serija ne prolazi, procene nema — nema zapisa, nema rekorda, nema oznake u
rezimeu. Ta putanja je i ranije postojala za serije iznad 12 ponavljanja.

### Od koje vrednosti plan polazi

`OneRepMaxBaseline.Select(samples, now, lookbackDays, allowStaleFallback)`:

1. prozor od 56 dana;
2. **najnoviji ručni unos** poništava sve starije uzorke — to je izjava o danas i jedini
   način da vežbač ispravi naduvanu procenu naniže;
3. najbolja vrednost, osim ako stoji više od **5%** iznad druge po redu — tada druga. Prag
   je namerno ispod najveće inflacije koju filter još pušta (RIR 3 daje ~7%), pa hvata
   omašenu procenu, a pravi napredak, koji se kreće u manjim koracima, ne dira;
4. prazan prozor: najnoviji zapis ikada (generisanje bloka, da se vežbač posle pauze ne
   vrati na prazna opterećenja) ili `null` (preračun unutar bloka, gde rekord od pre pola
   godine nije dokaz).

Domen ne čita sat: `now` je parametar.

`SelectSample` vraća i sam zapis, pa ekran „Poznati maksimumi" prikazuje **vrednost od koje
plan polazi**, a ne prosto najnoviji zapis. Ta dva broja se razlikuju tačno kada je najnoviji
zapis samotna naduvana procena.

## Testovi

- `E1RmCalculatorTests`: predikat na granicama (RIR 3 prolazi, RIR 4 i 5 ne, 13 ponavljanja
  ne, 0 kg ne), veza sa `StimulativeVolume.FullCreditRir`, i `BestEstimate` koji ignoriše
  seriju sa prevelikom rezervom (136.67 umesto 156.7) ili vraća `null`.
- `OneRepMaxBaselineTests`: ekstrem naspram napretka (141.7 umesto 150; 146 ostaje 146),
  jedan uzorak, ručni unos poništava starije, procene posle ručnog unosa se računaju, uzorak
  van prozora, oba ponašanja praznog prozora, `SelectSample`, i test koji drži prag ispod
  inflacije koju filter propušta.

`dotnet test`: 456 (bilo 434).

## Provereno u živoj aplikaciji

Nov nalog, ručni 1RM za Bench Press 130 kg, ravan blok Upper/Lower.

| Korak | Rezultat |
|---|---|
| Nedelja 1: Barbell Row 3 × 10 **sa RIR 5** | bez e1RM oznake, bez PR-a, bez novog zapisa (ranije bi upisalo 90 kg) |
| Nedelja 1: Bench 3 × 12 sa RIR 3 | e1RM 150 kg, sledeće 107.5 kg |
| Nedelja 2: Bench 3 × 10 sa RIR 1 | e1RM 136.67 kg |
| Ekran „Poznati maksimumi" | **136.67 kg** — osnova, a ne naduvanih 150 |
| Nov blok | nedelja 1 nosi **105 kg** (136.67 / 1.3). Po starom pravilu bi bilo 115 kg, izvedeno iz 150 |
| Ručni unos 120 kg, pa nov blok | ekran pokazuje 120 (Manual), nedelja 1 nosi **92.5 kg** (120 / 1.3) |

## Poznata ograničenja

- **Stari naduvani zapisi ostaju u istoriji.** Grafik e1RM trenda i „Lični rekordi" prikazuju
  sve što je ikada upisano, pa jedna stara procena može da stoji kao rekord i da spreči
  prikaz novog PR-a dok se ne pređe. Plan ih ne koristi: prozor od 56 dana i pravilo o
  ekstremu ih zaobilaze, a ručni unos ih poništava. Brisanje istorije nije uvedeno, jer se
  ne zna koji je zapis bio omašena procena, a koji dobar dan.
- **Pravilo o ekstremu hvata jednu vrednost, ne dve.** Dve naduvane procene u istom prozoru
  (150 i 156.7 iznad poštenih 140) prolaze, jer se porede međusobno.
- **Vežbač koji radne serije stalno prijavljuje sa RIR 4+** neće imati nijednu procenu:
  trend i rekordi ostaju prazni, a preračun opterećenja pada na pravilo koje izvodi
  implicitni maksimum iz same težine. To je namerno, ali vidljivo.
