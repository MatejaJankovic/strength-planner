# Doprinosi mišića, drugi deo

**Grana:** `fix/muscle-contributions-2`

Iz revizije nauke o treningu (odeljak F), nalaz F16.

## Problem

Runda 13 ([muscle-contributions.md](muscle-contributions.md)) uvela je pravilo: pomoćni udeo
pripada mišiću koji raste od rada, a ne onom koji samo učestvuje. Primenila ga je na potisak
(ramena) i čučanj (zadnja loža). Tri vežbe su ostale po starom:

- **Deadlift → Back 1.0.** Mrtvo dizanje je opružanje kuka: gluteusi i zadnja loža se
  skraćuju pod opterećenjem, a leđa (opružači, latovi, trapez) drže kičmu i šipku
  izometrijski. Runda 13 je ovaj udeo namerno ostavila („u zbirnoj grupi koja uključuje
  opružače leđa, pun kredit je odbranjiv"). Ali i opružači u mrtvom dizanju rade izometrijski,
  pa po pravilu iste runde pun udeo ne sledi.
- **Veslanja bez udela u ramenima.** U horizontalnom veslanju zadnji deltoid radi dinamički, a
  „Shoulders" je zbirna grupa čiji se budžet troši upravo na bočni i zadnji deltoid. Face Pull i
  Rear Delt Fly ga zato i nose.
- **Hip Thrust → Hamstrings 0.5.** Uz savijeno koleno zadnja loža je skraćena i malo
  opterećena. Gluteus nosi pokret, a ovaj udeo je punio budžet zadnje lože koji gradi pregib.

## Šta je urađeno

| Vežba | Bilo | Sada |
|---|---|---|
| Deadlift | Back 1.0, Glutes 0.5, Hamstrings 0.5, Quads 0.5 | **Glutes 1.0**, Hamstrings 0.5, **Back 0.5**, Quads 0.5 |
| Barbell Row | Back 1.0, Biceps 0.5 | Back 1.0, Biceps 0.5, **Shoulders 0.5** |
| Seated Cable Row | Back 1.0, Biceps 0.5 | Back 1.0, Biceps 0.5, **Shoulders 0.5** |
| Hip Thrust | Glutes 1.0, Hamstrings 0.5 | Glutes 1.0 |

Zgib i Lat Pulldown ne dobijaju udeo u ramenima: vertikalno povlačenje ne radi zadnji deltoid
kao veslanje.

**Upper/Lower x3: Face Pull na „Lower C" je zamenjen sa Straight-Arm Pulldown.** Kad mrtvo
dizanje više ne puni leđa punim udelom, taj šablon nema nijednu izolaciju za leđa. Napredni
vežbač je tada padao na **10.5** serija za leđa naspram MEV-a **12**, u prvoj nedelji linearnog
i prve dve nedelje obrnutog bloka hipertrofije. Uz Straight-Arm Pulldown je **15**. Ramena se ne
pomeraju (14 i 19 u tim nedeljama, isto kao pre), jer veslanja sada daju udeo zadnjem
deltoidu. Razlog iz runde 13 za vežbu gornjeg tela na tom danu i dalje važi, a komentar uz dan
je ažuriran.

Tabela u [evidence-based-leg-templates.md](evidence-based-leg-templates.md) je usklađena sa
katalogom: tri iskoraka su je i posle runde 13 navodila sa udelom zadnje lože.

## Šta su testovi pokazali

Proba pre izmene šablona (samo novi doprinosi) je oborila 2 testa:

- `TheTemplatesTheGuideRecommends_GiveAnAdvancedLifterMev_WhereTheGuideSays`:
  - **nestale su tri rupe za gluteuse** na Upper/Lower (4 dana), jer mrtvo dizanje sada
    puni gluteuse;
  - **pojavile su se tri nove rupe za leđa** na Upper/Lower x3, koji uputstvo preporučuje
    naprednom kao šablon koji svuda daje MEV. Taj šablon je MEV za leđa dostizao samo kroz
    mrtvo dizanje. Zato je zamenjena vežba na „Lower C".
  - Spisak sada sadrži samo biceps u bloku snage na Upper/Lower.
- `TheCeiling_ChangesOnlyWeeksThatWouldBreachIt`: šest nedelja sada dobija drugačiji predlog
  sa granicom po treningu nego bez nje, iako ni jedan predlog ne prelazi granicu. Svih šest je
  isti slučaj kao dva već imenovana izuzetka: **propis** stavlja više od 11 serija jednog
  mišića u trening. U Upper C srednjeg nivoa to su ramena (12.5, sa veslanjem). U Pull danu
  Push/Pull/Legs x2 početnika to su leđa i biceps (12, i ranije), a tek novi udeo je pomerio
  raspodelu. Dodati su imenom, a petlja u testu proverava da svaki zaista ima taj uzrok. Dva
  stara izuzetka (Full Body, 4 dana, početnik, snaga) su skinuta, jer više nisu izuzeci, kako
  test i traži.

Spisak mišića ispod MEV-a za naprednog (`AnAdvancedHypertrophyWeek_FallsBelowMev_OnlyWhereRecorded`)
se **nije** promenio.

## Provera

- `dotnet test`: **856** (bez novih testova; dva postojeća su ažurirana).
- Merenje vraćanjem, svaka izmena sama:
  - Deadlift po starom: obara **2**;
  - veslanja bez ramena: obara **1**;
  - Face Pull nazad na „Lower C": obara **1**;
  - Hip Thrust sa zadnjom ložom: obara **0**. Tu izmenu ne drži nijedan test. Ni katalog ni
    šabloni ne zavise od nje toliko da bi pomerili MEV ili MRV, a test koji bi samo ponovio
    tabelu doprinosa ne bi proveravao ništa.
- Klijent: 201 test, bez izmena.
- Razvojna baza: `ReconcileSystemExercisesAsync` usklađuje sistemske vežbe pri pokretanju,
  pa migracija nije potrebna. Pre restarta API-ja baza je imala `Deadlift → Back 1.00`, a
  `Hip Thrust → Hamstrings 0.50`. Posle restarta ima `Back 0.50, Glutes 1.00`, a veslanja
  `Shoulders 0.50`.
- Uživo, na 375 px: nov plan Upper/Lower x3 ima na „Lower C" Straight-Arm Pulldown
  (6 serija, 10–20), bez prelivanja.

## Šta ostaje

- Postojeći blokovi zadržavaju svoje vežbe. Šablon se čita pri generisanju bloka.
- Brojevi udela su i dalje gruba podela (1.0 / 0.5), kao i u celom katalogu. Ovde je samo
  pravilo iz runde 13 primenjeno dosledno.
