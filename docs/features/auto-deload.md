# Auto-deload

**Grana:** `feature/auto-deload`

## Problem iz rada

Zaključak navodi: *"...a deload је везан за четврту недељу уместо за стварно акумулирани
умор"*, i među pravcima daljeg razvoja: *"auto-deload, активиран праћењем показатеља
умора уместо календара, учинио би периодизацију заиста реактивном."*

Deload u četvrtoj nedelji je pretpostavka o tome *kada* će se umor nakupiti, a ne
merenje umora. Vežbač koji zakuca u drugoj nedelji nosi taj umor još dve nedelje; onaj
koji u četvrtoj još uvek napreduje biva rasterećen bez razloga.

## Rešenje

Svaka završena nedelja koja nije deload dobija **ocenu umora** od 0 do 1. Ako pređe
prag, **sledeća** nedelja se pretvara u deload. Planirani deload u četvrtoj nedelji
ostaje kao donja granica — ovo ga samo može povući ranije.

### Četiri signala

| Signal | Puna težina na | Udeo |
|---|---|---|
| Prosečno odstupanje efektivnog RIR-a ispod cilja | −2 poena | 0.35 |
| Udeo serija do otkaza | 50% | 0.25 |
| Pad najbolje procene 1RM u odnosu na prethodnu nedelju | −5% | 0.25 |
| Najveći odnos volumena i MRV-a među mišićnim grupama | 100% MRV (od 80% naviše) | 0.15 |

Svaki signal se normalizuje na 0–1, pa se sabira sa svojim udelom. Prag je **0.60**.

Pošto najteži signal nosi 0.35, **nijedan sam ne može da izazove deload** — moraju se
složiti bar dva. To je namerno: svaki od njih je pojedinačno bučan (loš san, jedan
neuspeo dan, jedna vežba blizu MRV-a), a nepotreban deload košta nedelju treninga.

Signali koji pokazuju u suprotnom smeru se odsecaju na nuli: nedelja lakša od plana, sa
rastom snage, daje ocenu 0, a ne negativnu vrednost koja bi "kompenzovala" nešto drugo.
Prva nedelja nema sa čim da uporedi 1RM, pa taj signal doprinosi nulom — nedostatak
podatka se ne tumači kao pad.

### Šta konkretno radi deload

Kada se nedelja pretvori u deload:

- broj serija po vežbi se prepolovljava (zaokruženo naviše, najmanje jedna),
- ciljno opterećenje se postavlja na **90% težine koja je stvarno korišćena** u
  prethodnoj nedelji, zaokruženo na [korak te vežbe](per-exercise-weight-step.md).

Opterećenja se preračunavaju zato što ih je progresija već popunila dok se prethodna
nedelja završavala — bez toga bi "deload" nedelja nosila normalne radne težine.

## Šta je urađeno

- `FatigueEvaluator` i `WeeklyFatigue` — čist domenski deo sa pragovima i udelima,
  bez EF-a i DTO-ova.
- `TrainingWeek.FatigueScore` (ocena izračunata **iz** te nedelje) i
  `TrainingWeek.IsAutoDeload` (nedelja je pretvorena u deload, nije bila planirana).
- `DeloadService` — skuplja signale, upisuje ocenu i po potrebi pretvara sledeću
  nedelju. Upis ocene je uslovni `UPDATE`, pa se nedelja ne može oceniti dvaput ni kada
  dva zahteva istovremeno završe njene sesije.
- `SessionService.CompleteAsync` poziva procenu **posle** progresije, jer deload
  prepisuje opterećenja koja je progresija upravo popunila.
- Rezultat treninga nosi `autoDeload`, pa ekran može odmah da objasni šta se desilo.
- Ekran treninga: objašnjenje u rezimeu treninga, tamo gde ga korisnik sigurno vidi
  pre nego što otvori sledeći trening.
- Dashboard: nedelja sa ocenom nosi diskretnu oznaku *Umor 0.60*, automatski deload
  *Deload zbog umora*, a planirani i dalje samo *Deload*.

## Provera

- `dotnet build`, `dotnet test` (86 testova, bilo 74), `npm run build` — sve prolazi.
- `FatigueEvaluatorTests` pokriva: nulu za nedelju po planu, jedinicu kada su svi
  signali na maksimumu, odsecanje signala u suprotnom smeru, to da nijedan pojedinačni
  signal ne prelazi prag, da se više umerenih signala zajedno prelazi, da naporna ali
  produktivna nedelja **ne** izaziva deload, neutralnost nedostajućeg 1RM poređenja,
  prag od 80% MRV-a i to da ocena nikada ne izlazi iz opsega 0–1.
- End-to-end, teška prva nedelja (sve serije do otkaza tri ponavljanja ispod opsega):
  ocena **0.60**, nedelja 2 pretvorena u deload (`isAutoDeload: true`), serije 3 → 2,
  opterećenja sa 80 kg na 70/72/72.5 kg zavisno od koraka vežbe (mašina/bučice/šipka).
- End-to-end, uredna prva nedelja (vrh opsega uz ciljni RIR): ocena **0.00**, nedelja 2
  netaknuta, planirani deload u nedelji 4 i dalje na mestu i označen kao planiran.
- U pretraživaču: posle završetka treninga u rezimeu stoji *"Nedelja 2 je pretvorena u
  deload… (ocena 0.6 od 1)"* sa `role="status"`, a na dashboardu nedelja 1 nosi
  *Umor 0.6*, nedelja 2 *Deload zbog umora*, nedelja 4 *Deload*.
