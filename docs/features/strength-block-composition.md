# Sastav bloka snage

**Grana:** `fix/strength-block-composition`

Četvrta grana iz revizije nauke o treningu (odeljak F), nalaz F5, plus nalaz koji je revizija
granice po treningu (#87) oborila kao nešto što ta grana nije uvela, ali ga je prenela ovde.

## Problem

Blok snage je o tri stvari odlučivao pravilima pisanim za hipertrofiju. Izmereno probom nad
svakim ugrađenim šablonom, pre izmene:

**1. Napredni vežbač je i u bloku snage dobijao jednu složenu vežbu po treningu.** Budžet
složenih vežbi zavisio je samo od nivoa. U nedelji: Push/Pull/Legs — bench, veslanje, čučanj
po jednom; Upper/Lower — bench, čučanj, zgib, mrtvo, **nijednom veslanje**; Full Body (2 dana)
— čučanj i potisak iznad glave, **bez bench-a**.

**2. Svaka složena vežba je nosila opseg snage (3–6)**, i ona koja ga ne podnosi. Na Legs
Specialization napredni vežbač je tri od pet složenih mesta dobijao kao bugarski čučanj,
rumunsko mrtvo na jednoj nozi i step-up na 3–6 — i nijedan obostrani čučanj ili pregib kao
dizanje snage. Priručnik (str. 4 i 11) kaže da unilateralne vežbe „nisu idealne za razvoj
apsolutne snage" i da nizak opseg „može narušiti tehniku, naročito kod vežbi sa nestabilnim
uslovima".

**3. Balansiranje je seklo glavno dizanje pre pomoćnog rada.** Jedna serija bench-a
rasterećuje i grudi i triceps, pa je cena po mišiću uvek birala nju. Preko ugrađenih nedelja
bloka snage: **373** slučaja da je složena vežba ispod propisa dok izolacija za isti mišić u
istom treningu drži svoje serije — napredni Push dan sa bench-om na 2 serije i razvlačenjima
na 5.

## Rešenje

### Najmanje dve složene vežbe u treningu bloka snage

`ExperienceProgramming.MaxCompoundsPerSession(nivo, cilj)`: u bloku snage najmanje
`MinCompoundsInAStrengthSession` (2). To podiže samo napredni nivo, sa 1 na 2 — srednji ih već
ima dve, početnik tri. Oslonac: frekvencija sama po sebi podiže snagu (Pelland i sar. 2025), i
to kod višezglobnih dizanja, ne jednozglobnih (Grgic i sar. 2018). Priručnikovo „do 3 složene
vežbe nedeljno" za naprednog opisuje nedelju hipertrofije.

**„Jedna više za svaki nivo" je izmereno i odbačeno.** Treća složena vežba za srednji nivo je
propis Upper/Lower bloka snage digla na **22 serije kvadricepsa naspram MRV-a 20** — a vežbač
koji izabere „Prati moj šablon" dobija baš propis. (Usput je davala i trening sa 14 serija
grudi, dve više od bilo čega drugog.)

### Opseg snage samo za vežbe koje ga podnose

`Exercise.SuitsLowReps` i isto polje u katalogu. Sedam sistemskih vežbi ga nema: Bulgarian
Split Squat, Split Squat, Walking Lunge, Goblet Squat, Step-Up, Single-Leg Romanian Deadlift i
Push-up — na jednoj nozi, nestabilne, ili bez načina da prime teret (goblet čučanj je ograničen
najtežom bučicom koju vežbač drži, sklek nema gde da doda teret). U bloku snage ostaju pomoćni
rad na 8–12, uz ciljni RIR bloka. `GoalPrescriptions.CarriesTheGoalRange` je jedno pravilo za
propis, a `IsStrengthLift` za sve ostalo što „glavno dizanje" znači.

Migracija `AddExerciseSuitsLowReps` postojećim redovima upisuje **true** — svaka složena vežba
je do sada i bila propisivana na 3–6, pa bi generisani podrazumevani `false` tiho prebacio sve
korisničke složene vežbe u pomoćni rad. Sedam sistemskih vežbi poravnava `DbSeeder` iz kataloga
pri startu. Model namerno nema podrazumevanu vrednost: za `bool` bi EF izostavio kolonu kad god
je vrednost `false`, pa bi je baza pregazila svojim `true`; migracija posle popune briše i
podrazumevanu vrednost same kolone.

Izmereno na razvojnoj bazi: pre — 40 sistemskih vežbi (22 složene) i 10 korisničkih; posle
migracije sve 50 na `true`, bez podrazumevane vrednosti kolone; posle starta API-ja tačno tih
sedam sistemskih na `false`, korisničke netaknute.

### Glavna dizanja stoje na propisu

`ExerciseSetSlot.IsMainLift`: u bloku snage svako dizanje sa opsegom snage. Balansiranje sada
radi u dva prolaza:

1. Nedelja se slaže sa glavnim dizanjima na propisu — cilj, MRV i granicu po treningu nosi
   pomoćni rad.
2. Glavno dizanje sme niže samo koliko granica oporavka i dalje traži — MRV nedelje ili
   granica po treningu — pošto pomoćni rad više nema šta da da. Nedeljni cilj se u tom
   prolazu ne pita, pa glavno dizanje nikad ne pomera ni naviše ni naniže.

**Jedna kazna ovo nije mogla.** Serija mrtvog dizanja pomera 2,5 serije nedeljnog cilja (leđa,
gluteus, zadnja loža, kvadriceps). Kazna koja bi blokirala pomeranje zbog cilja morala bi da
bude veća od 2,5, a da bi granica oporavka (4 po seriji) i dalje mogla da je spusti, manja od
1,5. Prvi pokušaj — potpuno zaključano glavno dizanje — dao je trening sa **18** serija jednog
mišića, jer granica više nije imala šta da seče.

### Glavna dizanja na početku treninga

Šabloni stavljaju najdublju složenu vežbu prvu, ali u bloku snage nije svaka složena vežba
dizanje snage. Uživo, prva nedelja Legs Specialization je imala leg press na 3–6 **drugi**, posle
bugarskog čučnja koji je već umorio iste mišiće. `SessionComposition.MainLiftsFirst` stavlja
glavna dizanja napred, a sve ostalo ostavlja u redosledu šablona — u razrešavanju šablona, u
pregledu u čarobnjaku i u simulaciji za testove.

### Pregled u čarobnjaku

`WorkoutTemplateDto.StrengthDays`, pored `Days`: pregled šablona i nedovršenog bloka pokazuje
sastav za cilj bloka. Ranije je blok snage prikazivao sastav hipertrofije.

## Posle izmene, napredni nivo, blok snage

| Šablon | Glavna dizanja u nedelji |
|---|---|
| Full Body (2 dana) | čučanj, **bench**, potisak iznad glave, RDL |
| Push/Pull/Legs | bench, potisak iznad glave, veslanje, zgib, čučanj, RDL |
| Upper/Lower | bench, **veslanje**, čučanj, RDL, zgib, incline bench, mrtvo, front squat |
| Legs Specialization | **leg press, front squat, hip thrust**, bench, veslanje, zgib, incline bench (bugarski čučanj, RDL na jednoj nozi i step-up kao pomoćni rad) |

Od 2.938 mesta glavnih dizanja u nedeljama bloka snage, granica oporavka spušta 108; nedeljni
cilj nijedno.

## Provera

- `dotnet test`: **741** (bilo 715); `npm test`: **188** (bilo 184); `npm run build` prolazi.
- Novi testovi: `StrengthBlockCompositionTests` (nijedno glavno dizanje ispod propisa dok pomoćni
  rad za isti mišić u istom treningu može još da da — bilo 373; nijedno iznad propisa; blok
  hipertrofije nema glavnih dizanja; nijedna vežba koja ne podnosi nizak opseg nije glavno
  dizanje; svaki trening bloka snage počinje glavnim dizanjima), testovi alokatora za dva
  prolaza (cilj ne pomera glavno dizanje ni u jednom smeru; granica po treningu i MRV ga spuštaju
  tek pošto je pomoćni rad na dnu), budžet po cilju, opseg po zastavici, `MainLiftsFirst`, i test
  MRV-a šablona i za blok snage na sva tri nivoa.
- Merenje vraćanjem (commit, vraćeno pravilo, rebuild), svako posebno: budžet — 3 od 738;
  opseg za vežbe koje ga ne podnose — 9 od 738; glavna dizanja u balansiranju — 7 od 738;
  redosled — 2 od 741.
- End-to-end, uživo, napredni nivo, blok snage, prva nedelja:

  | Šablon, trening | Predlog / propis × opseg |
  |---|---|
  | Legs Specialization, Legs A | **Leg Press 3/3×3–6**, Bulgarian Split Squat 3/3×8–12, … |
  | Legs Specialization, Legs C | **Hip Thrust 3/3×3–6**, Step-Up 2/3×8–12, … |
  | Upper/Lower, Upper A | **Bench 3/3×3–6, Barbell Row 3/3×3–6**, Cable Fly 3/3, … |
  | Push/Pull/Legs, Push | **Bench 3/3, OHP 3/3** (3–6), Cable Fly 4/3, Dumbbell Fly 4/3 |

  Glavna dizanja su svuda na propisu; volumen prima pomoćni rad (razvlačenja 3 → 4).
- U čarobnjaku plana (375 px, bez prelivanja): isti šablon za blok hipertrofije i za blok snage
  pokazuje dva različita sastava — blok snage počinje Legs A leg press-om.

## Ograničenja

- Upper/Lower ni sada ne daje naprednom vežbaču potisak iznad glave: oba dana za gornji deo ga
  navode tek kao treću složenu vežbu. Premestiti ga bi promenilo i sastav hipertrofije.
- Korisnička vežba podrazumevano podnosi nizak opseg; ekran za pravljenje vežbe to polje ne
  nudi. Korisnik koji napravi svoj iskorak dobiće ga u bloku snage na 3–6.
- Već generisani blokovi ne menjaju sastav ni opsege. Balansiranje se ponovo pokreće posle
  završenog treninga, pa tekući blok snage dobija zaštitu glavnih dizanja od tada.
