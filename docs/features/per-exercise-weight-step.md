# Korak opterećenja po vežbi

**Grana:** `feature/per-exercise-weight-step`

## Problem iz rada

U zaključku je navedeno: *"Корак оптерећења фиксиран је на 2,5 kg за све вежбе, што је
грубо за вежбе са бучицама и машинама."*

Konstanta `TrainingConstants.WeightStepKg = 2.5m` koristila se na tri mesta — pri
zaokruživanju početne radne težine, pri skoku u double progression, i pri zaokruživanju
deload težine. Bučice se u praksi kreću u parovima od 2 kg, a stackovi na mašinama u
skokovima od 5 kg, pa je jedinstven korak od 2.5 kg davao težine koje se ne mogu postaviti.

## Šta je urađeno

### Domen

- `EquipmentWeightStep` — novo, mapira spravu na podrazumevani korak:
  Barbell 2.5, Dumbbell 2, Machine 5, Cable 2.5, Bodyweight 1.
  Nepoznata sprava pada na globalnih 2.5 kg. Dozvoljen opseg je 0.5–10 kg.
- `Exercise.WeightStepKg` — podrazumevani korak same vežbe.
- `UserExerciseSetting` — nova entitet: korisnički override koraka za vežbu.
  Red postoji samo ako korisnik stvarno odstupa od podrazumevane vrednosti.
- `E1RmCalculator.WorkingWeightFor(...)` i `ProgressionEngine.ComputeNext(...)`
  dobili su opcioni `weightStepKg`. Kada se ne prosledi, ponašanje je identično
  kao pre (globalnih 2.5 kg), pa stari testovi prolaze nepromenjeni.

### Infrastruktura

- `WeightStepResolver` — jedno mesto koje rešava *efektivni* korak
  (override ako postoji, inače korak vežbe). Koriste ga i generator mezociklusa
  i `SessionService`, da obe strane računaju sa istim brojem.
- `MesocycleGenerator` zaokružuje početnu radnu težinu na korak vežbe.
- `SessionService.CompleteAsync` prosleđuje korak progresiji **i** zaokruživanju
  deload težine.
- `DbSeeder` postavlja korak iz sprave za nove vežbe i poravnava postojeće sistemske.
- Migracija `AddPerExerciseWeightStep` dodaje kolonu, tabelu override-a, i
  jednokratnim `UPDATE`-om popunjava korak za sve postojeće redove (uključujući
  korisničke custom vežbe koje bi inače ostale na 2.5).

### API

- `PUT /api/exercises/{id}/weight-step` — postavlja korak; `null` briše override.
- `ExerciseDto` nosi `weightStepKg`, `defaultWeightStepKg`, `isWeightStepOverridden`.
- `ExercisePlanDto` nosi `weightStepKg` da klijent zna kojim korakom da pomera težinu.

### Frontend

- Profil: nova sekcija "Korak opterećenja" sa pretragom vežbi, izborom koraka,
  oznakom "izmenjeno" i dugmetom za povratak na podrazumevanu vrednost.
- `WeightStepper` više ne piše "2.5 kg" u aria-labelama nego stvarni korak.
- Ekran treninga prosleđuje `plan.weightStepKg` svakom stepperu.

## Provera

- `dotnet build`, `dotnet test` (42 testa), `npm run build` — sve prolazi.
- Nova jedinična pokrivenost: `EquipmentWeightStepTests`, plus testovi u
  `ProgressionEngineTests` i `E1RmCalculatorTests` za korak po vežbi.
- Baza posle migracije: Barbell 2.50, Dumbbell 2.00, Machine 5.00, Cable 2.50,
  Bodyweight 1.00 — uključujući zatečenu custom vežbu "Hack Squat" (Machine → 5.00).
- End-to-end kroz API: 1RM 185 kg na Leg Pressu daje početnih **140 kg**
  (sa starim korakom bilo bi 142.5), a posle serija do vrha opsega sledeća nedelja
  stoji na **145 kg** — tačno jedan korak od 5 kg.
- U pretraživaču: profil prikazuje po vežbi tačan korak, izmena i reset rade,
  a na ekranu treninga dugmad pomeraju Leg Press za 5 kg a Plank za 1 kg.

## Nađeno i ispravljeno usput

- `[value]` na `<select>`-u u Angularu ne selektuje odgovarajući `<option>`, pa su
  svi redovi prikazivali prvu opciju (0.5 kg) bez obzira na stvarni korak.
  Rešeno vezivanjem `[selected]` po opciji.
- Zatečene custom vežbe ostajale su na 2.5 kg jer seeder poravnava samo sistemske;
  rešeno `UPDATE`-om u samoj migraciji.
