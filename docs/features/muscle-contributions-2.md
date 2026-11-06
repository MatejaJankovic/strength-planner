# Doprinosi mišića, drugi deo

**Grana:** `fix/muscle-contributions-2`

Iz revizije nauke o treningu (odeljak F), nalaz F16.

## Problem

Runda 13 ([muscle-contributions.md](muscle-contributions.md)) uvela je pravilo: pomoćni udeo
pripada mišiću koji raste od rada, a ne onom koji samo učestvuje. Primenila ga je na potisak
(ramena) i čučanj (zadnja loža). Nalaz F16 je naveo tri vežbe koje su ostale po starom:

- **Deadlift → Back 1.0.** Mrtvo dizanje je opružanje kuka, a pokret nosi gluteus. Runda 13 je
  ovaj udeo namerno ostavila („u zbirnoj grupi koja uključuje opružače leđa, pun kredit je
  odbranjiv"), i ova grana tu odluku menja.
- **Hip Thrust → Hamstrings 0.5.** Uz savijeno koleno zadnja loža je skraćena i malo
  opterećena. Runda 13 je i ovaj udeo zadržala, jer je potisak kukom svrstala u „zglobove kuka
  koji se zaista savijaju". Ovaj udeo je punio budžet zadnje lože koji gradi pregib.
- **Veslanja bez udela u ramenima.** U horizontalnom veslanju zadnji deltoid radi dinamički.
  Ovaj deo nalaza je **odbačen posle pregleda**, vidi ispod.

## Šta je urađeno

| Vežba | Bilo | Sada |
|---|---|---|
| Deadlift | Back 1.0, Glutes 0.5, Hamstrings 0.5, Quads 0.5 | **Glutes 1.0**, Hamstrings 0.5, **Back 0.5**, Quads 0.5 |
| Hip Thrust | Glutes 1.0, Hamstrings 0.5 | Glutes 1.0 |

Obrazloženje za mrtvo dizanje, kako stoji uz katalog:

- **Gluteus** nosi pokret.
- **Zadnja loža** dobija pola, a ne pun udeo kao u rumunskom mrtvom dizanju. Koleno se opruža
  zajedno sa kukom, pa se taj dvozglobni mišić malo menja po dužini.
- **Leđa** zadržavaju pola. Opružači rade izometrijski, ali pod velikom napetošću. Za razliku od
  zadnje lože u čučnju, oni tu napetost i nose.

Pravilo iz runde 13 je dakle preciznije „mišić koji nosi veliku napetost u radu", a ne samo
„mišić koji se skraćuje". Brojevi su gruba podela (1.0 / 0.5), kao u celom katalogu. Ni
priručnik ni literatura ne daju brojeve za udele po vežbi, pa ovo nije citat nego dosledna
primena pravila.

**Upper/Lower x3: Face Pull na „Lower C" je zamenjen sa Straight-Arm Pulldown.** Kad mrtvo
dizanje više ne puni leđa punim udelom, taj šablon nema nijednu izolaciju za leđa. Napredni
vežbač je tada padao na **10.5** serija za leđa naspram MEV-a **12**, u prvoj nedelji linearnog
i prve dve nedelje obrnutog bloka hipertrofije. Uz Straight-Arm Pulldown je **15**.

Ramena u tim i u ravnim nedeljama ostaju na **14** i **19**. Nose ih dva bočna podizanja, Rear
Delt Fly i Face Pull na „Upper C", ne veslanje: napredni u bloku hipertrofije dobija jednu
složenu vežbu po treningu, i to nijedno veslanje. U najtežim nedeljama ramena padaju sa 24
(i 29 u petoj nedelji obrnutog bloka) na **23**, i dalje iznad MAV-a. To je cena uklonjenog Face
Pull-a. Razlog iz runde 13 za vežbu gornjeg tela na tom danu i dalje važi, a komentar uz dan je
ažuriran.

Tabela u [evidence-based-leg-templates.md](evidence-based-leg-templates.md) je usklađena sa
katalogom: tri iskoraka su je i posle runde 13 navodila sa udelom zadnje lože. Beleške iz runde
13 ([muscle-contributions.md](muscle-contributions.md),
[template-placement.md](template-placement.md)) su dobile napomenu „Kasnije".

## Posle pregleda: veslanje bez udela u ramenima

Prva verzija je veslanjima dala Shoulders 0.5, i time vratila tačno onaj kvar koji je runda 13
ispravila za potisak. „Shoulders" je zbirna grupa, a njen budžet se troši na bočni i zadnji
deltoid. Čim veslanje puni taj budžet, balansiranje skida serije sa bočnog podizanja, jedine
vežbe za bočni deltoid, koji veslanje ne radi. Izmereno preko svih ugrađenih nedelja:

| | `main` | sa udelom veslanja | bez njega (ova grana) |
|---|---|---|---|
| Lateral Raise, ukupno serija | 4433 | 4138 | **4604** |
| Rear Delt Fly | 3153 | 2854 | **3193** |

Uz udeo veslanja je Lateral Raise u bloku snage srednjeg nivoa na Push/Pull/Legs padao na dve
serije u većini nedelja. Uz to je udeo pomerio i tabelu u uputstvu (PPL, srednji nivo), a
test granice po treningu je dobio šest novih izuzetaka. Bez udela je tabela tačna kakva jeste,
a spisak izuzetaka je isti kao na `main`.

Pregled je našao i da je moja tvrdnja „ramena se ne pomeraju jer veslanja sada daju udeo" bila
pogrešna o uzroku: napredni u tim nedeljama nema veslanje. Tekst iznad je ispravljen.

## Šta su testovi i merenja pokazali

Proba sa novim doprinosima, pre izmene šablona:

- `TheTemplatesTheGuideRecommends_GiveAnAdvancedLifterMev_WhereTheGuideSays`:
  - **nestale su tri rupe za gluteuse** na Upper/Lower (4 dana), jer mrtvo dizanje sada
    puni gluteuse;
  - **pojavile su se tri nove rupe za leđa** na Upper/Lower x3, koji uputstvo preporučuje
    naprednom kao šablon koji svuda daje MEV. Zato je zamenjena vežba na „Lower C".
  - Spisak sada sadrži samo biceps u bloku snage na Upper/Lower.
- Spisak mišića ispod MEV-a za naprednog u ravnom bloku hipertrofije
  (`AnAdvancedHypertrophyWeek_FallsBelowMev_OnlyWhereRecorded`) se **nije** promenio.

Sve nedelje ispod MEV-a, svi šabloni od tri dana naviše, svi nivoi, ciljevi i modeli (`main` →
ova grana): **498 → 491**.

- Nestalo je:
  - četiri nedelje gluteusa ispod MEV-a za naprednog na Upper/Lower;
  - tri nedelje bicepsa za početnika u bloku snage na Full Body.
- **Novo:** Full Body (3 dana), napredni, blok snage: leđa **11** naspram MEV-a **12** u prvoj
  nedelji linearnog, prve dve obrnutog i petoj nedelji starog linearnog modela („Linearan
  (stari)", koji čarobnjak više ne nudi). Na istim nedeljama su
  ranije bili gluteusi (4.5 naspram 5). Nijedan test to ne pokriva, jer testovi MEV-a gledaju
  hipertrofiju ili dva Upper/Lower šablona. Uputstvo to sada kaže.
- **Dublje:** Upper/Lower, početnik, blok snage: leđa 7 → 6 naspram MEV-a 8 u istim lakšim
  nedeljama. Rupa je postojala i pre.

Hip Thrust bez zadnje lože daje **Leg Curl 5391 → 5778** serija preko svih ugrađenih nedelja.
Udeo zadnje lože u mrtvom dizanju se nije promenio, pa rast dolazi od ove izmene. Pregib je
vežba koja zadnju ložu gradi, i sada mu više mesta ne uzima potisak kukom.

## Provera

- `dotnet test`: **856** (bez novih testova; ažuriran je spisak u testu MEV-a i komentar
  u testu granice po treningu, gde su najveći treninzi sada 11, 9 i 8 serija, a ne 11, 8 i 7).
- Merenje vraćanjem, svaka izmena sama, na konačnoj verziji:
  - Deadlift po starom: obara **1** (na verziji sa udelom veslanja 2, jer je drugi pad dolazio
    od tog udela);
  - Face Pull nazad na „Lower C": obara **1**;
  - Hip Thrust sa zadnjom ložom: obara **0**. Tu izmenu ne drži nijedan test, a izmereni
    učinak (Leg Curl +387) je zapisan iznad.
- Klijent: 201 test, bez izmena.
- Razvojna baza: `ReconcileSystemExercisesAsync` usklađuje sistemske vežbe pri pokretanju,
  pa migracija nije potrebna. Pre restarta API-ja baza je imala `Deadlift → Back 1.00` i
  `Hip Thrust → Hamstrings 0.50`, a posle `Deadlift → Back 0.50, Glutes 1.00` i
  `Hip Thrust → Glutes 1.00`.
- Uživo, na 375 px: nov plan Upper/Lower x3 ima na „Lower C" Straight-Arm Pulldown
  (6 serija, 10–20), bez prelivanja.

## Šta ostaje

- Postojeći blokovi zadržavaju svoje vežbe. Šablon se čita pri generisanju bloka.
- Vertikalno povlačenje (zgib, Lat Pulldown) radi zadnji deltoid manje od veslanja, a
  zbirna grupa ramena ne može da primi nijedno od njih bez istog kvara. Prava ispravka je
  podela ramena na glave, i to je, kao u rundi 13, odluka vlasnika.
