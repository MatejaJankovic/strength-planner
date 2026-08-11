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

## Ispravke posle revizije koda

### Kritično: kaskada je znala da pojede ceo mezociklus

Nedelje koje čekaju ocenu se učitaju **jednom**, na početku, a pretvaranje nedelje u
deload je samo promena u change trackeru — pa je upit za "sledeću nedelju koja nije
deload" u narednom krugu i dalje video staro stanje u bazi.

Redosled treniranja nije nametnut (i `SessionService` to izričito podržava), pa je
dovoljno da korisnik prvo odradi celu nedelju 2, a zatim se vrati na nedelju 1. Tada se
ocenjuju obe: nedelja 1 pretvori nedelju 2, a nedelja 2 zatim pretvori nedelju 3.
Provereno na stvarnom API-ju: **tri deload nedelje od četiri, jedna trenažna**.

Sada se posle prve konverzije staje. To je i suštinski ispravno: ono što dolazi posle
umetnutog deload-a više nije ista situacija, pa se ne sme suditi po podacima od pre.
Posle ispravke isti scenario daje **jedan deload i tri trenažne nedelje**.

### Kritično: deload je prepisivao već odrađene treninge

Ni upit za sledeću nedelju ni upit za planove nisu gledali status sesija — iako
progresija to pravilo poštuje (`plan.WorkoutSession.Status != Completed`, sa komentarom
da "complete van redosleda ne sme da prepiše ciljeve već odrađenih treninga").

Posledica: nedelja koju je korisnik već odradio mogla je naknadno da dobije prepolovljene
serije i spuštena opterećenja, pa zapis plana više ne bi opisivao ono što je stvarno
urađeno; nedelja u toku bi se menjala korisniku pod rukama. Sada se deload stavlja samo
na nedelju čije su **sve sesije još u statusu Planned**.

### Umor se merio nejednako za različite ciljeve

Odstupanje RIR-a se normalizovalo fiksnom skalom od dva poena. Ali RIR ne ide ispod
nule, pa serija bez otkaza pri cilju RIR 1 (hipertrofija, podrazumevani cilj) najviše
može da prijavi −1 → pola udela. Maksimum dostižan bez otkaza je bio **0.575**, ispod
praga od 0.60: hipertrofija praktično nije mogla da izazove deload, dok je ista slika
pri cilju RIR 2 davala 0.75. Sada se meri u odnosu na ono što je za dati cilj dostižno,
pa oba cilja daju isti signal za isto ponašanje.

### Dva najteža signala merila su isti događaj

Serija do otkaza ispod opsega ulazila je i u prosek RIR-a (kroz efektivni RIR) i u udeo
otkaza. Time je pravilo "moraju se složiti bar dva signala" gubilo smisao — jedan
događaj je popunjavao oba. RIR se sada računa **samo nad dovršenim serijama**; otkazi su
zaseban signal. Nedelja u kojoj nijedna serija nije dovršena nema prosek, ali se to
odsustvo tretira kao najgore moguće očitavanje, a ne kao neutralno.

### Mezociklus je ostajao sa dva deload-a

Pretvaranje nedelje 2 uz planirani deload u nedelji 4 davalo je raspored
`trening – deload – trening – deload`: dve izolovane trenažne nedelje. Pretvaranje
nedelje 3 davalo je dva deload-a jedan za drugim. Sada mezociklus nosi **jedan** deload:
kada ga umor povuče ranije, planirani na kraju se vraća u trenažnu nedelju (broj serija
se preuzima sa odgovarajuće trenažne nedelje, jer je deload nedelja pri generisanju
kreirana već prepolovljena). Odgovor to i prijavljuje, pa ekran može da objasni zašto je
plan izgubio poslednji deload.

### Rezime treninga je protivrečio sam sebi

Rezime je popunjavan tokom progresije, a deload posle toga prepisuje ista planska
zaduženja. Korisnik je u istom ekranu video poruku "nedelja je pretvorena u deload" i,
odmah ispod, *"Sledeće 82.5 kg ↑"*. Rezime se sada usklađuje sa stvarnim planom:
provereno da za svih šest vežbi prijavljuje tačno ono što stoji u deload nedelji
(72.5 / 70 kg), bez strelice naviše.

### Sitnije

- Poređenje e1RM je išlo sa prethodnom nedeljom bez obzira na to da li je ona bila
  deload. Pošto su deload serije namerno submaksimalne, nedelja posle deload-a je uvek
  izgledala kao skok i taj signal (udeo 0.25) je tiho otpadao. Sada se poredi sa
  poslednjom nedeljom koja nije bila deload.
- Nedelja bez ijedne upisane serije nije dobijala ocenu, pa je zauvek ostajala u listi
  "za ocenjivanje" i svaki naredni završetak treninga ju je iznova učitavao. Sada dobija
  ocenu 0.
- Dva upita nisu bila ograničena po korisniku (bez stvarnog curenja, jer je mezociklus
  već proveren, ali suprotno pravilu iz `CLAUDE.md`).
- `FatigueScore` je bio neograničeni `numeric`; dobio je `HasPrecision(4, 3)` kao i
  ostale decimalne kolone u šemi.
- `WorkoutSessionDto.IsAutoDeload` je bio popunjen ali nigde prikazan — zaglavlje
  treninga sada razlikuje "Deload" od "Deload zbog umora".
