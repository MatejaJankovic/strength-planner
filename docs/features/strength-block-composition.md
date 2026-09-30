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
istom treningu još može da da seriju (iznad dna svog prozora) — napredni Push dan sa bench-om
na 2 serije i razvlačenjem sa bučicama na 5. (Uže čitanje, izolacija na svom propisu ili iznad
njega, daje 284.)

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

`ExerciseSetSlot.IsMainLift`: u bloku snage složena vežba čiji je osnovni opseg opseg snage
(`GoalPrescriptions.IsMainLift`). Čita se iz opsega samog plana, ne iz kataloga: lični šablon
bira svoj, pa je leg press koji je vežbač uneo na 12–15 u njegovom bloku snage pomoćni rad, a
iskorak na 3–5 dizanje koje je izabrao da optereti. Kod ugrađenog šablona oba čitanja se slažu,
i test ih drži zajedno.

Balansiranje sada radi u dva prolaza koja se smenjuju dok nijedan ništa ne pomera:

1. Pomoćni rad slaže nedelju — cilj, MRV i granicu po treningu — dok glavna dizanja stoje tamo
   gde su (na početku na propisu).
2. Glavno dizanje sme niže samo koliko granica oporavka i dalje traži — MRV nedelje ili
   granica po treningu — pošto pomoćni rad više nema šta da da, i vraća se ka propisu čim mu
   mesto dozvoli. Nedeljni cilj se u tom prolazu ne pita, pa glavno dizanje nikad ne pomera.
   Kad su dva glavna dizanja podjednako dobar rez, seče se kasnije u treningu: šablon glavno
   dizanje dana navodi prvo. Uživo je početnikov Push dan (snaga, linearan, nedelja 1) pre toga
   završavao sa bench-om na 2 serije i incline-om na 3; sada bench 3/4, incline 2/4.

**Jedna kazna ovo nije mogla.** Serija mrtvog dizanja pomera 2,5 serije nedeljnog cilja (leđa,
gluteus, zadnja loža, kvadriceps). Kazna koja bi blokirala pomeranje zbog cilja morala bi da
bude veća od 2,5, a da bi granica oporavka (4 po seriji) i dalje mogla da je spusti, manja od
1,5. Prvi pokušaj — potpuno zaključano glavno dizanje — dao je trening sa **18** serija jednog
mišića, jer granica više nije imala šta da seče. (Izmereno sa odbačenim budžetom „jedna više za
svaki nivo"; sa budžetom koji je ušao, zaključano glavno dizanje daje 14 — i dalje preko
granice, pa zaključavanje ostaje odbačeno.)

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

- `dotnet test`: **751** (bilo 715; 741 pre revizije); `npm test`: **188** (bilo 184);
  `npm run build` prolazi.
- Novi testovi: `StrengthBlockCompositionTests` (nijedno glavno dizanje ispod propisa dok pomoćni
  rad za isti mišić u istom treningu može još da da — bilo 373; nijedno iznad propisa; blok
  hipertrofije nema glavnih dizanja; nijedna vežba koja ne podnosi nizak opseg nije glavno
  dizanje; svaki trening bloka snage počinje glavnim dizanjima), testovi alokatora za dva
  prolaza (cilj ne pomera glavno dizanje ni u jednom smeru; granica po treningu i MRV ga spuštaju
  tek pošto je pomoćni rad na dnu), budžet po cilju, opseg po zastavici, `MainLiftsFirst`, i test
  MRV-a šablona i za blok snage na sva tri nivoa.
- Merenje vraćanjem (commit, vraćeno pravilo, rebuild), svako posebno: budžet — 3 od 738;
  opseg za vežbe koje ga ne podnose — 9 od 738; redosled — 2 od 741. Glavna dizanja u
  balansiranju zavise od toga šta se vraća: prvi prolaz bez zakucavanja, a drugi zadržan — 7
  od 738; ceo alokator od pre ove grane (jedan prolaz, bez glavnih dizanja) — 6 od 738 (izmerila
  revizija), odnosno 7 od 750 na konačnoj verziji, sa testom vraćanja mesta pomoćnom radu.
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

## Posle revizije

Revizija (dva recenzenta, svaki nalaz srednje težine pred dva osporavača) potvrdila je dva
nalaza, i oba su ispravljena:

- **Mesto koje oslobodi rez glavnog dizanja ostajalo je prazno.** Drugi prolaz seče celim
  serijama, pa često stane ispod granice po treningu sa mestom viška, a prolaz pomoćnog rada se
  posle toga nije ponavljao. Na Pull danu Push/Pull/Legs (početnik, snaga) face pull je ostajao
  na 3, iako četvrta serija ništa ne probija, a leđima i ramenima fali — rezultat nije bio ni
  lokalni optimum sopstvene cene, u 12 od 351 ugrađene nedelje bloka snage. Prolazi se sada
  smenjuju dok nijedan ništa ne pomera. Vraćanje ove ispravke: pada 1 od 750 (baš taj slučaj
  kao test). Uživo, isti Pull dan: veslanje, zgib i veslanje na sajli 3/4, face pull **4**/5.
- **Uputstvo je pogrešno ograničavalo pravilo na ugrađene šablone.** Novi pasus je stajao
  iznad rečenice „Ovo važi samo za ugrađene šablone", pa je izgledalo da se na nju odnosi. Sada
  stoji posle nje i kaže da važi i za lični šablon, a red „Ciljni volumen" u tabeli ličnog
  šablona pominje izuzetak.

Od nalaza niske težine ispravljeni su: glavno dizanje se čita iz opsega plana (gore);
objašnjenje posle treninga više ne okrivljuje nedeljni cilj za glavno dizanje — vraćeno ka
propisu nema oznaku, a spušteno se objašnjava samo MRV-om ili granicom po treningu (ranije je
incline bench spušten zbog MRV-a grudi dobijao oznaku „Triceps"; vraćanje ove ispravke: 2 od
750); komentari koji su govorili „jedna složena vežba više" za blok snage, što važi samo za
naprednog; brojevi 373, 18 i merenje vraćanjem, sada sa tačnim značenjem; test MRV-a sa ciljem
iznad MRV-a, što proizvodnja ne može da da.

## Ograničenja

- Upper/Lower ni sada ne daje naprednom vežbaču potisak iznad glave: oba dana za gornji deo ga
  navode tek kao treću složenu vežbu. Premestiti ga bi promenilo i sastav hipertrofije.
- Korisnička vežba podrazumevano podnosi nizak opseg; ekran za pravljenje vežbe to polje ne
  nudi. Korisnik koji napravi svoj iskorak i stavi ga u ugrađeni šablon ne može — ugrađeni
  šabloni koriste samo sistemske vežbe — a u ličnom šablonu opseg ionako bira sam.
- Već generisani blokovi ne menjaju sastav ni opsege. Balansiranje se ponovo pokreće posle
  završenog treninga, pa tekući blok snage dobija zaštitu glavnih dizanja od tada.
