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

## Ispravke posle revizije koda

Reviziju je odradio zaseban agent nad `main...feature/per-exercise-weight-step`.
Sve što je našao je provereno i ispravljeno:

1. **Stepper je mogao da skoči dvostruko.** `Math.round(value ± step)` je tačno samo
   dok je tekuća težina na mreži koraka — što je pre ove grane bilo garantovano, jer
   je korak uvek bio 2.5. Kada korisnik prebaci Bench Press sa 2.5 na 5 kg, planska
   težina od 82.5 kg više nije na mreži, pa je "+" pomerao **7.5 kg** a "−" samo 2.5 kg.
   Zamenjeno pomeranjem na prvu vrednost mreže u pritisnutom smeru
   (82.5 → 85 → 90 → 85). Uz to, `canDecrease` je sa korakom 10 i težinom 5 kg trajno
   zaključavao dugme "−"; sada spušta na 0.
2. **Izmena drugog reda dok prvi snima je nestajala bez traga.** `savingStepId` je
   jedan globalni signal, a `[disabled]` je gasio samo red koji snima. Sada su svi
   select-i i dugmad zaključani dok traje upis.
3. **Neuspeo PUT ostavljao je select na odbijenoj vrednosti.** Angular ne prepisuje
   `[selected]` kada se model nije promenio, pa je red trajno lagao o stanju servera.
   Select se sada ručno vraća na serversku vrednost i u `error` i u `next` grani.
4. **`toFixed(1)` je 1.25 kg prijavljivao kao "1.3 kg"** u aria-labeli dugmeta.
5. **Prikaz težine sa jednom decimalom** krio je 61.25 kao "61.3"; sada `1.1-2`.
6. **API je primao proizvoljnu preciznost.** `2.333` je vraćano u odgovoru, a baza
   (`numeric(6,2)`) čuvala `2.33`. Dodat `EquipmentWeightStep.Normalize` koji zaokružuje
   pre provere i upisa, pa se odgovor i naknadni `GET` slažu.
7. **`UserExerciseSettings.UserId` nije imao FK ka `AspNetUsers`**, za razliku od svih
   ostalih korisničkih tabela. Dodat kaskadni FK; migracija je regenerisana.
8. `SetWeightStepAsync` je čitao `Exercise` sa praćenjem promena bez potrebe → `AsNoTracking`.
9. Trka dva prva override-a rušila se u 500 preko jedinstvenog indeksa → sada 409.
10. Duplirani upit za override-e izbačen iz `ExerciseService` (koristi `WeightStepResolver`).
11. `WeightStepResolver.ResolveAsync` sada i sam filtrira po korisniku, umesto da se
    oslanja na to da pozivalac prosledi već ograničen skup ID-jeva.
12. Sitno: `HasDefaultValue(2.5m)` → `TrainingConstants.WeightStepKg`, mrtav izračun
    u `ProgressionEngine` pomeren ispod ranog izlaza, uklonjen `|| 2.5` fallback u šablonu.

Bezbednost je pregledana posebno i nije nađen nijedan put ka tuđim podacima: sva četiri
upita nad `UserExerciseSettings` filtriraju po `userId`, `SetWeightStepAsync` odbija tuđu
custom vežbu sa 404 pre validacije (bez orakla za nabrajanje), a `userId` uvek dolazi iz
tokena, nikad iz tela zahteva.

### Posledica koju vredi pomenuti u radu

Grublji korak utišava RIR auto-regulaciju na lakšim opterećenjima. Korekcija je ±3% po
RIR poenu, pa se težina pomeri tek kada je `|w · c| ≥ korak/2`. Sa starim korakom od
2.5 kg jedan RIR poen razlike pomerao je opterećenje od 41.7 kg naviše; sa mašinskim
korakom od 5 kg tek od 83.3 kg. Ispod toga zaokruživanje pojede korekciju i vežba se
ponaša kao čista double progression. To je fizički iskreno (međukorak ionako ne postoji
na spravi), ali jeste promena u odnosu na model auto-regulacije opisan u radu.
