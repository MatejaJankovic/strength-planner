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
| Prosečno odstupanje RIR-a ispod cilja, **samo nad dovršenim serijama** | ciljni RIR (koliko je uopšte dostižno) | 0.35 |
| Udeo serija do otkaza | 50% | 0.25 |
| Pad najbolje procene 1RM u odnosu na poslednju nedelju koja nije bila deload | −5% | 0.25 |
| Najveći odnos volumena i MRV-a među mišićnim grupama | 100% MRV (od 80% naviše) | 0.15 |

Svaki signal se normalizuje na 0–1, pa se sabira sa svojim udelom. Prag je **0.60**.

Pošto najteži signal nosi 0.35, **nijedan sam ne može da izazove deload** — moraju se
složiti bar dva. To je namerno: svaki od njih je pojedinačno bučan (loš san, jedan
neuspeo dan, jedna vežba blizu MRV-a), a nepotreban deload košta nedelju treninga.

Da bi to pravilo išta značilo, signali moraju da budu **nezavisni**. Zato se RIR meri
samo nad serijama koje su dovršene: serija do otkaza bi inače pomerila i prosek RIR-a i
udeo otkaza, pa bi jedan događaj sam ispunio uslov "moraju se složiti bar dva".

Iz istog razloga se odstupanje RIR-a meri u odnosu na ono što je za dati cilj **dostižno**,
a ne fiksnom skalom: RIR ne ide ispod nule, pa serija bez otkaza pri cilju RIR 1
(hipertrofija) najviše može da prijavi −1, dok pri cilju RIR 2 (snaga) može −2. Fiksna
skala bi istu sliku ocenila različito samo zbog cilja.

> **Kasnija izmena ([`e1rm-reliability.md`](e1rm-reliability.md)).** Signal pada procenjenog
> 1RM-a sada se gradi samo od serija koje uopšte smeju da daju procenu (do 12 ponavljanja i
> RIR najviše 3). Nedelja u kojoj takvih serija nema doprinosi tim signalom nula, umesto da
> ga gradi na proceni koju sistem nigde drugde ne priznaje.

> **Kasnija izmena ([`progression-reference-and-summary.md`](progression-reference-and-summary.md)).**
> Osnova deload-a je najteža podignuta težina, ne prosek svih serija u nedelji; kad vežba
> nema ni jednu upisanu seriju, uzima se propis završene nedelje, a ne već progresovani cilj
> same deload nedelje. Uz to, nedelja posle deload-a (kad auto-deload oslobodi planirani) ne
> nastavlja od deload težine nego od one zarađene pre njega.

> **Kasnija izmena ([`load-progression-top-of-range.md`](load-progression-top-of-range.md)).**
> Granica „najviše −1" važi za seriju unutar opsega. Od te grane se serija koja je stala
> **ispod** donje granice opsega meri kapacitetom (ponavljanja + RIR prema dnu opsega), pa
> može da prijavi i manje od −1; ocena je u tom slučaju ograničena na punu težinu signala.

Signali koji pokazuju u suprotnom smeru se odsecaju na nuli: nedelja lakša od plana, sa
rastom snage, daje ocenu 0, a ne negativnu vrednost koja bi "kompenzovala" nešto drugo.
Prva nedelja nema sa čim da uporedi 1RM, pa taj signal doprinosi nulom — nedostatak
podatka se ne tumači kao pad.

### Šta konkretno radi deload

Kada se nedelja pretvori u deload:

- broj serija po vežbi se prepolovljava (zaokruženo naviše, najmanje jedna),
- ciljno opterećenje se postavlja na **90% težine koja je stvarno korišćena** u
  prethodnoj nedelji, zaokruženo na [korak te vežbe](per-exercise-weight-step.md),
- planirani deload na kraju mezociklusa se oslobađa: blok nosi **jedno** rasterećenje.

Deload se stavlja samo na nedelju čije sesije još nisu započete — prepisivanje ciljeva
već odrađenog treninga bi falsifikovalo istoriju.

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

- `dotnet build`, `dotnet test` (89 testova, bilo 74), `npm run build` — sve prolazi.
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
- End-to-end, van redosleda (prvo cela nedelja 2, pa nedelja 1): jedan deload i tri
  trenažne nedelje; nedelja 2, koju je korisnik već odradio, ostaje netaknuta.
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

### Kasnija izmena (odeljak C): jedan uzrok je punio dva signala

Pravilo „nijedan signal sam ne može da pokrene deload" je imalo izuzetak koji ga je rušio u
krajnjem slučaju: nedelja bez ijedne dovršene serije je RIR signal čitala kao 1.0, a udeo
otkaza je iz **istog** razloga već bio 1.0 — 0.35 + 0.25 je tačno prag. Izmereno u živoj
aplikaciji: ista nedelja je davala **0.703** i deload odmah, a sada daje **0.353** i deload
ne; ponovljena još jednom daje **0.65** i deload, jer tada govori i član o padu snage.
Postojao je i test koji je tvrdio to isto pravilo i prolazio zbog ulaza koji servis ne
proizvodi. Vidi [`independent-fatigue-signals.md`](independent-fatigue-signals.md).

### Kasnija izmena (pregled logike treninga, odeljak B)

Kad se deload pokrene, uz prepolovljene serije i 90% opterećenja ide i **ciljni RIR + 2**
(hipertrofija 3, snaga 4). Ovde je pisalo samo prve dve stvari, jer je treća i u kodu
izostajala: deload propisan na ciljnom RIR-u je po naporu bio radna serija — na 90%
opterećenja se RIR 1 dostiže tek na vrhu opsega. Signal umora se ne menja: deload nedelje
se ionako ne ocenjuju. Vidi [`deload-intensity.md`](deload-intensity.md).

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
