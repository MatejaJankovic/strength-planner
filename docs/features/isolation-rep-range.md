# Izolacija ne ide na opseg snage

Nalaz B11 iz pregleda logike treninga.

## Problem

Cilj bloka je do sada davao opseg ponavljanja **svakoj** vežbi u ugrađenom šablonu. U bloku
snage to znači 3–6 ponavljanja za bočno podizanje, letenje, biceps pregib i triceps
ekstenziju, a u petoj nedelji linearnog modela **3–4**.

To nije mali detalj na ivici plana. Nivo iskustva određuje sastav treninga, i napredan
vežbač ima **jednu složenu i pet izolacija** po treningu — dakle pet od šest vežbi je
nosilo opseg koji im ne pripada.

Trojka na bočnom podizanju nije provera sile. Cilj bloka opisuje kako vežbač namerava da
ojača, a snaga se izražava u pokretima koji mogu da nose opterećenje; izolacija ne može, i
opterećenje kojim bi se mišić doveo do otkaza u tri ponavljanja opterećuje zglob, a ne
mišić koji je i tu ograničavajući faktor.

## Pravilo

`GoalPrescriptions.ForExercise(goal, type)`: **opseg prati vežbu, ciljni RIR prati blok.**

| Blok | Vežba | Opseg | Ciljni RIR |
|---|---|---|---|
| Snaga | složena | 3–6 | 2 |
| Snaga | izolacija | **8–12** | 2 |
| Hipertrofija | složena | 8–12 | 1 |
| Hipertrofija | izolacija | 8–12 | 1 |

Rezerva je koliko nedelja treba da bude teška, a to je svojstvo nedelje, a ne pokreta.
Zadržavanje jednog ciljnog RIR-a po bloku je i ono što dozvoljava da deload i vraćanje
oslobođene nedelje rade sa jednim brojem, umesto da se po vežbi pamti još jedna osnova.

Periodizacija se primenjuje na oba opsega kao i na svaki drugi, pa razlika traje kroz ceo
blok:

| Nedelja (linearan, snaga) | Složena | Izolacija |
|---|---|---|
| 1 (volumen) | 6–9 | 8–12 |
| 3 (osnova) | 3–6 | 8–12 |
| 5 (intenzitet) | **3–4** | **6–10** |
| 6 (deload) | 3–6 | 8–12 |

Hipertrofijski blok se ne menja ni za jednu vrstu vežbe: tamo je izolacija ionako bila na
svom opsegu.

## Zašto nema nove kolone

`ExercisePlan` od šestog kruga pamti `BaseRepRangeMin/Max` po planu, i deload vraća **taj**
opseg, a ne opseg cilja. Izolacija se zato posle deload-a vraća na 8–12 sama po sebi. Da je
razlika bila u ciljnom RIR-u, trebala bi i `BaseTargetRir` kolona i migracija — to je drugi
razlog zbog koga RIR ostaje po bloku.

Lični šablon se ne dira: on nosi opseg koji je korisnik uneo za svaku vežbu, i to je prvi
operand u `planned.RepRangeMin ?? ...`.

## Propis se od sada računa po vežbi

Prva verzija ove izmene je prošla sve testove i **nije promenila aplikaciju**. Generator je
propis nedelje računao **jednom** (iz cilja i nivoa iskustva) i tu istu vrednost davao svakoj
vežbi ugrađenog šablona; grana `ForWeek` po vežbi je postojala samo za lični šablon, koji
nosi svoje serije:

```csharp
var exercisePrescription = planned.Sets is null
    ? prescription                       // <- nedeljni propis, iz opsega cilja
    : Periodization.ForWeek(..., baseRepRangeMin, baseRepRangeMax, ...);
```

Moj opseg je zato stizao samo u kolone `BaseRepRangeMin/Max`, a ne u nedelju: Cable Fly je u
planu stajao na `6–9` dok mu je sidro bilo `8–12`. Popravka upola — deload bi vratio 8–12,
a trenažne nedelje bi ostale na opsegu snage.

Propis se sada računa **po vežbi**, iz njenog osnovnog opsega i njenog broja serija
(`planned.Sets ?? startingSets`). Za složene vežbe iz ugrađenog šablona daje identične
brojeve kao deljeni propis, pa se hipertrofijski blok i dalje ne menja.

Ovo je isti oblik greške kao `SetLogDto` iz devetog kruga: pravilo je bilo tačno, testovi
zeleni, a aplikacija nepromenjena — jer su testovi proveravali pravilo, a ne to da li ga
iko poziva. Našao ga je jedino E2E.

## Posledica za broj serija

Kada izolacija nosi opseg koji već stoji na Epley granici, pomeraj ponavljanja u nedelji
volumena nema kuda — pa ga, po pravilu iz [prethodne grane](rep-window-and-fixed-reps.md),
dobija kao seriju. Zato u bloku snage izolacija u nedeljama volumena nosi **jednu seriju
više** od složene vežbe:

| Nedelja (linearan, snaga, napredan nivo) | Bench Press | Izolacije |
|---|---|---|
| 1 (volumen) | `4 × 6–9` | `5 × 8–12` |
| 3 (osnova) | `3 × 3–6` | `3 × 8–12` |
| 5 (intenzitet) | `2 × 3–4` | `2 × 6–10` |
| 6 (deload) | `2 × 3–6 @RIR 4` | `2 × 8–12 @RIR 4` |

To je dosledno: faza volumena za vežbu kojoj je prozor zatvoren izražava se serijama.

## Šta je izmereno

- Vraćanje starog pravila (svaka vežba uzima opseg cilja) obara **3 od 536** testova:
  `AStrengthBlock_KeepsIsolationInTheHypertrophyRange`, `TheDifferenceSurvivesPeriodization`
  i `InAStrengthBlock_NoIsolationExerciseOfABuiltInTemplateSitsOnTheStrengthRange`.
- U živoj aplikaciji (napredan nivo, šablon Upper/Lower, linearan model) brojevi iz tabele
  iznad; kartica u nedelji 1 pokazuje `Opseg 6–9` za Bench Press i `Opseg 8–12` za Cable Fly.
- Hipertrofijski blok kroz isti šablon: sve vežbe 8–12 u svakoj nedelji, dakle nepromenjen.

## Testovi

`GoalPrescriptionTests` (14 tvrdnji): pravilo po cilju i tipu, da ciljni RIR ostaje po
bloku, da se hipertrofija ne menja, da razlika preživi periodizaciju i deload, i da katalog
zaista razvrstava vežbe (Lateral Raise, Cable Fly, Barbell Curl, Triceps Pushdown i Leg
Curl kao izolacije; Bench Press, Back Squat, Deadlift i Barbell Row kao složene).

Poslednji test prolazi kroz **sve** ugrađene šablone i traži da nijedna izolacija u bloku
snage ne stoji na opsegu snage (preko 50 mesta). On drži ishod, ne prolaz kroz kod:
vezivanje pravila za generator dokazuje E2E, jer servisi nemaju test harness.

Ukupno: 522 → 536 testova na serveru, 139 na klijentu (bez izmena).
