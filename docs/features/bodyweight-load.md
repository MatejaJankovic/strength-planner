# Sopstvena masa je opterećenje

Grana `feature/bodyweight-load`. Četvrta i poslednja grana iz pregleda logike treninga
(odeljak A), nalaz A6.

## Problem

Zgib se logovao kao `0 kg`. Sve što je niže u lancu čitalo je taj broj doslovno:

| Gde | Šta je radilo |
|---|---|
| Procena maksimuma | `0 × (1 + 11/30) = 0` — nula je ulazila u `OneRepMaxRecords` kao maksimum |
| Tonaža | `0 × 10 = 0` — trening od četrdeset zgibova sabirao je nulu |
| Progresija | vrh opsega je davao `0 + korak`, dakle „stavi 2.5 kg" na seriju koja nikada nije bila opterećena |
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

| | Ukupno | Predlog |
|---|---|---|
| Pre | 10 kg | 10 × 1.06 + 2.5 = **13 kg** |
| Sada | 90 kg | (90 × 1.06 + 2.5) − 80 = **17.5 kg** |

Korekcija od 6% je vredela 0.6 kg umesto 5.4 — zgib je „napredovao" petinom koraka.

Zaokruživanje ide u **dodatom** prostoru, jer korak tamo i postoji: tanjiri se stavljaju na
pojas, ne na telo. Deo tela nije umnožak koraka, pa bi zaokruživanje ukupnog dalo broj koji
se ne može staviti.

**Pod.** Kada pravilo traži ukupno manje od tela samog, nema šta da se skine. Predlog je
tada 0 dodatnih kilograma i `ProgressionResult.LoadFloorReached` je `true`, što rezime
prikazuje rečenicom da se dalje napreduje kroz ponavljanja. Kada nema ni tela ni tegova
(plank upisan sa 0), `ComputeNext` vraća 0 bez koraka — to je jedini način da se ukloni
„+2.5 kg" sa vežbe koja se ne opterećuje.

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
je značila kada je upisana. Menja se samo ono što tek predstoji, jer se tu broj čita na novoj
skali: planovi neodrađenih treninga za vežbe sa udelom dobijaju `max(0, cilj − deo tela)`.
Na razvojnoj bazi je to 18 planova zgiba sa 44–69 kg → 0 („sopstvenom masom"), dok je šest
planova planka ostalo netaknuto (udeo 0). Obrisan je i jedan zapis maksimuma sa `ValueKg ≤ 0`
(1106 → 1105): domensko pravilo ga ionako odbija, ali je stajao u istoriji rekorda.

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

## Prikaz

- Stepper na kartici vežbe: **„Dodatno opterećenje"** umesto „Težina serije", sa redom ispod
  koji kaže koliko je kilograma telesna masa (TM) za tog vežbača i da 0 znači sopstvenom
  masom.
- Spisak odrađenih serija: `TM × 10`, `TM + 5 kg × 8`. Gleda **snimak iz serije**, ne
  zastavicu plana — serija upisana pre ove verzije nosi 0 i čita se kao obična težina, jer se
  tako i računala.
- Rezime: oznaka `e1RM (ukupno)`, `Sledeće: TM + 17.5 kg ↑ +7.5`, i rečenica o podu kada je
  predlog stao na sopstvenoj masi.
- Analitika: ispod rekorda vežbe sa telesnom masom piše da su oba broja ukupno opterećenje,
  a ispod tonaže da te vežbe ulaze sa ukupnim opterećenjem.

Jedno odstupanje od plana, iz istog razloga: „najveća težina" u rekordima je **ukupno**, a ne
dodato. Zgib bez pojasa bi inače prijavio „najveća težina 0 kg" pored „najbolji e1RM 109 kg"
— dva broja u istom redu, u različitim jedinicama. `PersonalRecordDto.IsBodyweight` postoji
da bi se ta jedinica mogla i napisati.

## Testovi

`BodyweightLoadTests` (25 tvrdnji): udeli i zaokruživanje na skalu kolone, `AddedTarget` koji
nikada ne ide ispod nule, progresija sa delom tela i bez njega (identična kada je deo 0),
pod, plank bez ijednog opterećenja, `WorkingLoad` po ukupnom, `BestEstimate` iz tela,
deload i `UndoDeload` na ukupnoj skali, i tri tvrdnje o katalogu — udeo je u [0, 1], udeo
nosi samo oprema „Bodyweight", i nijedan ugrađen šablon više ne propisuje plank.

`BodyweightColumnTests` vezuje kolone za domen na nivou EF modela: `numeric(3,2)` prima
najveći dozvoljen udeo (1.00), a `numeric(6,2)` prima tačno ono što `PortionKg` proizvodi.
Kolona uža od izračunate vrednosti ne bi obarala upis nego bi tiho zaokruživala, pa bi
progresija računala sa jednim brojem a istorija čuvala drugi.

`ProgressionPropertyTests` je proširen mrežom sa delom tela (3 koraka × 3 udela × 4 dodate
težine × 5 opsega × 4 ciljna RIR-a × sve serije): predlog nikad nije negativan, pod uvek daje
nulu, vrh opsega nikad ne spušta, i — najvažnije — isti scenario sa telom pretvorenim u teg
daje isto **ukupno** opterećenje, do na jedan korak zaokruživanja.

Ukupno: 459 → 489 testova na serveru, 128 → 133 na klijentu.

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
