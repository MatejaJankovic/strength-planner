# Sopstvena masa je opterećenje

Grana `feature/bodyweight-load`. Četvrta i poslednja grana iz pregleda logike treninga
(odeljak A), nalaz A6.

## Problem

Zgib se logovao kao `0 kg`. Sve što je niže u lancu čitalo je taj broj doslovno:

| Gde | Šta je radilo |
|---|---|
| Procena maksimuma | `0 × (1 + 11/30) = 0` — nula je ulazila u `OneRepMaxRecords` kao maksimum |
| Tonaža | `0 × 10 = 0` — trening od četrdeset zgibova sabirao je nulu |
| Progresija | vrh opsega je davao `0 + korak`, dakle „stavi 1 kg" (korak opreme „Bodyweight") na seriju koja nikada nije bila opterećena |
| Rekordi | „najveća težina 0 kg" |

Profil već nosi `BodyweightKg` — od registracije, i koristi se za granice volumena. Do
trenažnih pravila prosto nikada nije stigao.

Druga polovina istog problema je **plank**: on nije pogrešno modelovan nego nemodelabilan.
Izdržaj nema ponavljanje čije bi se opterećenje procenjivalo, a stajao je u petnaest mesta
u ugrađenim šablonima, sa propisom „3 × 8–12 ponavljanja" i predlogom težine.

## Rešenje

### Model: jedan broj po vežbi

`Exercise.BodyweightShare` (numeric(3,2), podrazumevano 0) — koliki deo tela pokret diže.
Zgib 1.00, sklek 0.64, iskorak (Split Squat) 0.85, plank 0.

Udeo je procena i ne mora da bude tačan: na 80 kg telesne mase greška od 10% u udelu pomera
opterećenje za oko 7 kg, a korekcija od 3% na tome je 0.2 kg — unutar jednog tanjira. Ono
što ne sme da bude je nula, a to je bio.

`SetLog.BodyweightLoadKg` (numeric(6,2), podrazumevano 0) je **snimak** `masa × udeo` u
trenutku upisa serije. Promena mase u profilu zato ne menja istoriju: e1RM, tonaža i umor iz
već odrađenih serija ostaju onakvi kakvi su bili tog dana. Izmena serije (olovka) snimak ne
dira — ispravlja se broj ponavljanja, ne masa tog jutra.

`WeightKg` i `TargetWeightKg` ostaju **dodati** kilogrami. To je jedina podela koja radi u
oba smera: vežbač stavlja tegove na pojas, a pravila računaju ceo posao.

### Pravila rade nad ukupnim, vraćaju dodato

`BodyweightLoad` (`Domain/Algorithms`) nosi tri operacije: `PortionKg` (koliko je tela),
`AddedTarget` (iz željenog ukupnog u ono što ide na pojas) i `IsAtBodyweightFloor`.

`ProgressionEngine.ComputeNext` je dobio parametar `bodyweightLoadKg` (podrazumevano 0, pa
je svaki postojeći poziv nepromenjen) i radi na `usedWeightKg + bodyweightLoadKg`. Razlika
je merljiva: zgib sa +10 kg na vežbaču od 80 kg, tri serije po 12 sa RIR 3 na cilju 1.

Korak zgiba je **1 kg**: `EquipmentWeightStep` toliko daje opremi „Bodyweight", a
`DbSeeder` to upisuje vežbi. (Prva verzija ovog odeljka je računala sa 2.5 kg i dobijala
13 → 17.5 — brojeve koje aplikacija za tu vežbu ne daje ni po starom ni po novom pravilu.
Naći ih je bilo lako tek kad je test prestao da prosleđuje korak rukom.)

| | Ukupno | Predlog na pojasu |
|---|---|---|
| Pre | 10 kg | zaokruženo(10 × 1.06 + 1) = **12 kg** |
| Sada | 90 kg | zaokruženo((90 × 1.06 + 1) − 80) = **16 kg** |

Korekcija od 6% je vredela 0.6 kg umesto 5.4, pa je od koraka i korekcije zajedno ostajalo
2 kg umesto 6. `ComputeNext_OnTheOldRule_WouldHaveProposedFourKilogramsLess` drži oba broja.

Zaokruživanje ide u **dodatom** prostoru, jer korak tamo i postoji: tanjiri se stavljaju na
pojas, ne na telo. Deo tela nije umnožak koraka, pa bi zaokruživanje ukupnog dalo broj koji
se ne može staviti.

**Pod.** Kada pravilo traži ukupno manje od tela samog, nema šta da se skine. Predlog je
tada 0 dodatnih kilograma i `ProgressionResult.LoadFloorReached` je `true`, što rezime
prikazuje rečenicom da se dalje napreduje kroz ponavljanja. Kada nema ni tela ni tegova
(plank upisan sa 0), `ComputeNext` vraća 0 bez koraka — to je jedini način da se ukloni
korak sa vežbe koja se ne opterećuje.

Isto se provuklo i kroz ostatak lanca: `WorkingLoad` poredi serije po ukupnom opterećenju,
`E1RmCalculator.BestEstimate` procenjuje iz ukupnog, `NextWeekLoad` (svih pet pravila i
`UndoDeload`) prima deo tela, `DeloadService` rasterećuje 90% ukupnog, a `AnalyticsService`
računa tonažu kao `(dodato + telo) × ponavljanja`.

### Ručni 1RM se ne unosi

`POST /api/onerepmax` odbija vežbu sa udelom telesne mase (400). Njen maksimum je ukupno
opterećenje, pa uneto „100" ne znači ništa određeno — ni 100 kg na pojasu, ni 100 ukupno.
Ekran „Poznati maksimumi" te vežbe ne nudi za dodavanje, a postojeće zapise prikazuje kao
vrednost bez steppera, sa objašnjenjem „Procenjuje se iz odrađenih serija".

### Plank

Ostaje u katalogu — strani ključevi, stari planovi, lični šabloni — sa udelom 0. Iz
ugrađenih šablona je izbačen: svih petnaest mesta zamenjeno je novom vežbom **Machine
Crunch** (Isolation, Machine, Abs 1.0), koja se opterećuje i zato može da napreduje. Zamena
je na istom indeksu u listi, pa su testovi šablona ostali zeleni bez izmene.

## Izmereno na razvojnoj bazi

Migracija `AddBodyweightLoad` nije uradila ono što je plan predviđao, i razlog je merenje.

Plan je bio: upisati snimak u postojeće serije (`masa × udeo`) i ponovo izgraditi procene
maksimuma na ukupnoj skali. Pre pisanja migracije prebrojano je šta u bazi zaista stoji:

```
  Name   | WeightKg | count
---------+----------+-------
 Pull-up |    38.00 |     4
 Pull-up |    40.00 |    34
 Pull-up |    50.00 |    10
 Pull-up |    70.00 |     5
 Pull-up |    77.00 |     4
 ...
```

**Nijedna serija nije upisana sa 0 kg.** Svih 119 nosi 38–77 kg, jer je aplikacija tražila
broj, a vežbač je ukucao koliko mu je ceo pokret „težio". Dodavanje telesne mase na to bi
isto telo brojalo dvaput i podiglo procenu zgiba sa ~90 na ~200 kg. Pretpostavka iza
backfill-a — „stara istorija čita nulu" — u ovoj bazi ne važi.

Zato migracija **ne dira** odrađene serije ni procene: istorija i dalje znači tačno ono što
je značila kada je upisana. Menja se samo ono što nije ni počelo, jer se tu broj čita na
novoj skali: planovi **nezapočetih** treninga za vežbe sa udelom dobijaju
`max(0, cilj − deo tela)`. Na razvojnoj bazi je to 18 planova zgiba sa 44–69 kg → 0
(„sopstvenom masom"), dok je šest planova planka ostalo netaknuto (udeo 0). Obrisan je i
jedan zapis maksimuma sa `ValueKg ≤ 0` (1106 → 1105): domensko pravilo ga ionako odbija, ali
je stajao u istoriji rekorda.

Trening **u toku** se ostavlja zajedno sa odrađenim, i to je ispravka iz revizije: prvo je
uslov bio „sve što nije završeno". Takva sesija nosi obe polovine — serije upisane u staroj
skali i cilj u staroj skali — pa bi prevođenje samo cilja ostavilo dve skale unutar jednog
treninga, dok vežbač sledeću seriju upisuje u jedinicama u kojima je upisao prve tri. Sesija
zato zadržava skalu u kojoj je započeta. Na razvojnoj bazi razlika između dve varijante nije
dotakla ni jedan red (nijedna sesija u toku nije imala vežbu sa udelom), pa već primenjena
migracija nije morala da se popravlja.

Cena je jedan šav u podacima: zgib odrađen pre migracije piše „40 kg", a posle nje „TM".
Alternativa je bila izmišljanje brojeva, pa je šav izabran svesno.

## Izmereno u živoj aplikaciji: isti red, dva broja

Serija upisana kroz aplikaciju prikazivala se kao `0 kg × 12` u trenutku upisa, a kao
`TM × 12` posle osvežavanja stranice. Ista serija, isti ekran.

`SetLog` se u `SetLogDto` preslikavao na **tri** mesta, svaki put ručno: u `SetLogService`
(odgovor na upis serije), u `SessionService` i u `MesocycleService` (čitanje). Nova kolona
dodata je u dva od tri. Inicijalizator objekta u C#-u ne mora da bude potpun, pa se to
prevelo bez ijedne poruke, a tip je i dalje bio tačan.

Ispravka nije bila „dodaj i na treće mesto" nego uklanjanje trećeg mesta: `SetLogMapper.ToDto`
sada je jedini put od entiteta do DTO-a. Sledeća kolona ne može da se izgubi na isti način.
Našla ga je jedino provera u živoj aplikaciji — nijedan test nije ni mogao, jer su sva tri
preslikavanja bila tačna po tipu.

## Šta je revizija našla

Tri nalaza su prošla adversarijalnu proveru i ispravljena su u istoj grani.

**Deload je izmišljao opterećenje na podu.** `NextWeekLoad.UndoDeload` vraća težinu od koje
je deload izveden, deljenjem sa 0.90. Uz deo telesne mase je to počelo da radi nad ukupnim
opterećenjem — što je tačno, osim kada je deload cilj **nula**. Nula je mesto gde je
`AddedTarget` odsekao, pa informacija o polaznoj težini više ne postoji; deljenje je odatle
„vraćalo" `80 / 0.9 − 80 = 8.888…`, zaokruženo naniže na korak. Zgib bez pojasa je posle
deload nedelje dobijao **TM + 8 kg**,
skok od 10% ukupnog opterećenja vežbaču koji nikada nije dodao ni kilogram. Na `main`-u je
ista funkcija vraćala 0. Sada se nula vraća kao nula: ne može se obrnuti, a potcenjivanje
se ispravlja samo, u jednoj sesiji, kroz RIR korekciju.

**Napomena o podu je imala jedan tekst za dva uzroka.** `LoadFloorReached` je `true` i kada
pravilo traži manje od tela, i kada opterećenja nije bilo uopšte (plank upisan sa 0 kg, gde
telo ne ulazi u račun). Rezime je u oba slučaja pisao „Ispod sopstvene mase nema šta da se
skine" — za plank neistinu, jer on ne nosi ni gram telesne mase u ovom modelu. Uzrok se
razlikuje po delu telesne mase, koji rezime već nosi, pa je tekst razdvojen u
`load-floor-note.ts`, sa testom za oba slučaja. XML dokumentacija `LoadFloorReached` sada
imenuje oba uzroka umesto jednog.

**Test je bio zelen i za pogrešnu implementaciju.** `AddedTarget` postoji zbog jednog
svojstva: mreža koraka je na pojasu, ne na telu. Sve provere su ga nosile sa delom tela od
80 kg i korakom od 2.5 — a 80 = 32 × 2.5, pa su za takav deo dve različite implementacije
*identične*. Izmereno: sa zaokruživanjem nad ukupnim opterećenjem ceo paket od 489 testova
ostaje zelen. Sklek (udeo 0.64, korak 1 kg) bi tada dobijao predlog od 4.8 kg — težinu koja
se ne može staviti ni na jedan pojas i koja se onda nosi dalje kao osnova. Dodate su provere
sa delom tela koji **nije** umnožak koraka (51.2 uz korak 1, 68 uz korak 2.5) i invarijanta
u mreži svojstava da predlog mora biti umnožak koraka. Sa obrnutom implementacijom pada 5
testova; sa ispravnom prolazi 500.

Dva nalaza su odbijena proverom i nisu menjana: da oznaka „Sačuvano" na redu sa procenom
obmanjuje (ona znači „vrednost postoji", a procena jeste vrednost — ali se red više ne
broji u „Sačuvano 3 / 6", jer taj brojač prati unos), i da su 120 i 109 dva čitanja istog
merenja (dva različita primera: 12 ponavljanja sa RIR 3 i 10 sa RIR 1, na telu od 80 kg).

## Prikaz

- Stepper na kartici vežbe: **„Dodatno opterećenje"** umesto „Težina serije", sa redom ispod
  koji kaže koliko je kilograma telesna masa (TM) za tog vežbača i da 0 znači sopstvenom
  masom.
- Spisak odrađenih serija: `TM × 10`, `TM + 5 kg × 8`. Gleda **snimak iz serije**, ne
  zastavicu plana — serija upisana pre ove verzije nosi 0 i čita se kao obična težina, jer se
  tako i računala.
- Rezime: oznaka `e1RM (ukupno)`, `Sledeće: TM + 6 kg ↑ +6`, i rečenica o podu kada
  predlog ne može niže — jedna za vežbu koja je stala na sopstvenoj masi, druga za vežbu
  koja nije bila opterećena.
- Analitika: ispod rekorda vežbe sa telesnom masom piše da su oba broja ukupno opterećenje,
  a ispod tonaže da te vežbe ulaze sa ukupnim opterećenjem.

Ekran „Poznati maksimumi" te vežbe i dalje nudi u spisku za dodavanje, ali bez polja za
unos: red piše „Ne unosi se" i vrednost procene, ili „Još nema procene". Prvo su bile
izbačene iz spiska — a onda korisnik koji traži zgib ne nalazi ni vežbu ni objašnjenje zašto
je nema.

Jedno odstupanje od plana, iz istog razloga: „najveća težina" u rekordima je **ukupno**, a ne
dodato. Zgib bez pojasa bi inače prijavio „najveća težina 0 kg" pored „najbolji e1RM 120 kg"
— dva broja u istom redu, u različitim jedinicama. `PersonalRecordDto.IsBodyweight` postoji
da bi se ta jedinica mogla i napisati.

## Testovi

`BodyweightLoadTests` (35 tvrdnji): udeli i zaokruživanje na skalu kolone, `AddedTarget`
koji nikada ne ide ispod nule **i uvek pada u mrežu koraka** (uključujući deo tela koji nije
umnožak koraka), progresija sa delom tela i bez njega, oba broja iz merenja u ovom dokumentu
(12 naspram 16), pod, plank bez ijednog opterećenja, vežba sa spoljnim opterećenjem upisana
sa 0 kg, `WorkingLoad` po ukupnom, `BestEstimate` iz tela, `NextWeekLoad` na sve tri putanje
koje polaze od maksimuma, `UndoDeload` na ukupnoj skali i na podu, i tri tvrdnje o katalogu
— udeo je u [0, 1], udeo nosi samo oprema „Bodyweight", i nijedan ugrađen šablon više ne
propisuje plank.

`BodyweightColumnTests` vezuje kolone za domen na nivou EF modela: kapacitet se čita iz
modela i poredi sa najvećim udelom koji domen dozvoljava i sa najvećim delom tela koji
aplikacija može da proizvede (profil prima do 400 kg). Prva verzija je tvrdila
`IsValidShare(MaxShare)` — to je `MaxShare <= MaxShare`, tačno za svaku vrednost i bez
ikakve veze sa kolonom o kojoj test govori. Izmereno: sužavanje kolone na `numeric(2,2)`
sada obara test koji se tako i zove, a ne samo literal `3` u susednom.

`ProgressionPropertyTests` je proširen mrežom sa delom tela (3 koraka × 3 udela × 4 dodate
težine × 5 opsega × 4 ciljna RIR-a × sve serije): predlog nikad nije negativan, pod uvek daje
nulu, vrh opsega nikad ne spušta, predlog je uvek umnožak koraka, i isti scenario sa telom
pretvorenim u teg daje isto **ukupno** opterećenje, do na jedan korak zaokruživanja.

Ukupno: 459 → 500 testova na serveru, 128 → 136 na klijentu.

## Šta ostaje otvoreno

- Udeli su jedna procena za sve. Sklek na 0.64 je prosek iz literature; kod nekoga je 0.60,
  kod nekoga 0.70. Personalizacija bi tražila merenje koje aplikacija nema čime da uradi.
- Asistirani zgib (guma, mašina) se ne modeluje: oduzimanje bi tražilo negativno dodato
  opterećenje, a `AddedTarget` ga namerno ne dozvoljava.
- Vežba koju korisnik sam napravi nema udeo, čak i ako izabere opremu „Bodyweight". Udeli su
  procene napravljene jednom, za sve, i niko nije procenio njegovu.
- Plank i dalje prima ručni maksimum, jer mu je udeo 0 — za sistem je to obična vežba sa
  spoljnim opterećenjem (može se raditi sa tanjirom na leđima). Ponašanje je isto kao pre
  ove grane.
