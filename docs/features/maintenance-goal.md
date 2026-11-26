# Održavanje kao treći cilj

**Grana:** `feature/maintenance-goal`

Runda 20, stavka 20.4. Odluka vlasnika: blok koji čuva postignuto, za periode kad nema vremena ili energije; volumen oko
MEV-a, težine se ne guraju. Vrhunac forme (peaking) nije izabran. Promena modela periodizacije već generisanog bloka
ostaje van opsega.

## Šta je izgrađeno

`Goal` ima treću vrednost, `Maintenance`. Blok održavanja:

- propisuje **6-10 ponavljanja uz tri u rezervi** za složene vežbe (izolacija zadržava 10-20), jer po Epley-u set vredi
  ponavljanja plus rezerva: 6 + 3 i 10 + 3 su isto što i 8 + 1 i 12 + 1, pa je šipka ista kao u hipertrofiji, uz dva
  ponavljanja manje i dva više u rezervi. Intenzitet je ono što drži snagu i veličinu mišića kad se volumen i učestalost
  smanje (Spiering i sar. 2021);
- cilja **MEV** nedeljno po mišiću (`WeeklyVolumeTarget.ForGoal`);
- **ne diže težinu na vrhu opsega** (`ProgressionEngine`, zastavica `stepAtTopOfRange`): tad težina ostaje, a korekcija
  naviše zbog lakše serije od cilja i dalje važi, jer vraća napor na cilj i nije napredovanje;
- **ne uči granice volumena** (`GoalPrescriptions.LearnsVolumeLimits`, čita ga `VolumeLandmarkService`): nedelja se
  obračuna, ali MEV/MAV/MRV ostaju;
- je **uvek ravan** (`Periodization.ModelFor`): pri pravljenju plana, pri predlogu blokova i u generatoru;
- u planu se **ponavlja** (`MacrocyclePlanner.AlternatingGoals`), ne smenjuje sa hipertrofijom;
- u čarobnjaku nema izbor rasporeda, a pod ciljem piše šta održavanje menja.

Migracije nema: cilj je u bazi tekst (`varchar(16)`, „Maintenance" ima 11 znakova) i nema CHECK ograničenja nad njim.

## Pre gradnje: šta svako pravilo koje čita cilj radi sa trećom vrednošću

| Mesto | Sa trećom vrednošću, bez izmene | Odluka |
|---|---|---|
| `GoalPrescriptions.ForGoal` | baca `ArgumentOutOfRangeException`: generator, deload i `SessionOutcomes` bi pali | dodat propis 6-10, RIR 3 |
| `ForExercise`, `CarriesTheGoalRange`, `IsStrengthLift`, `IsMainLift` | sve što nije snaga ide putem hipertrofije: svaka složena nosi raspon bloka, nema „glavnih dizanja" | ostavljeno, i pod testom |
| `WeeklyVolumeTarget.ForGoal` | `goal == Strength ? sredina : MAV`, pa bi održavanje **tiho dobilo MAV** | MEV; nepoznat cilj sada baca |
| `MaxCompoundsPerSession` | nije snaga, pa budžet nivoa (3/2/1) | ostavljeno |
| `MacrocyclePlanner.AlternatingGoals` | iza održavanja bi stajala hipertrofija, pa plan održavanja ne bi bio plan održavanja | ponavlja se |
| model periodizacije | ništa ga ne vezuje za cilj: linearan model bi dodavao serije iznad MEV-a i spuštao rezervu | uvek ravan |
| `ProgressionEngine` | ne čita cilj; vrh opsega diže težinu | zastavica, vidi gore |
| `VolumeLandmarkService.AdaptWeekAsync` | ne čita cilj; uči iz svake nedelje | ćuti, vidi niže |
| `FatigueEvaluator`, `WeeklyFatigue` | ne čitaju cilj | ostavljeno; ocena umora radi i može da povuče deload |
| `CalibrationSet.AppliesTo` | čita propis, ne cilj: 10 + 3 = 13 je iznad Epley granice (12), pa kalibracije nema | ostavljeno, ali pod testom, jer sledi iz brojeva, a ne iz pravila |
| `DeloadService`, `SessionOutcomes` (povratak posle deload-a) | čitaju `ForGoal`, pa su bacali | rade sa novim propisom |
| `WorkoutTemplateResolver`, `TemplateService`, `BlockEditService`, `WeeklySetPlanner` | prosleđuju cilj dalje | bez izmene |
| klijent | tri mesta sa nazivom cilja, čarobnjak, rečenica o sledećem bloku | jedan pomoćni modul (`core/models/goal.ts`) |

## Zašto granice ne uče iz bloka održavanja

Izmereno nad pravim pravilom učenja (`MaintenanceGoalTests.WhatAWeekAtMevWouldHaveTaught_IsWhyTheBlockLearnsNothing`),
srednji nivo, mišić sa 10/16/22:

| Nedelja na MEV-u | Šta bi pravilo uradilo |
|---|---|
| dve rezerve teža od cilja | MRV 22 → 21 (nedelja je „bar na minimumu", pa umor spušta plafon) |
| snaga raste dve nedelje zaredom po 4% | MEV 10 → 9 |
| snaga ravna | ništa |

Oba pomaka su po pravilu ispravna, ali o pogrešnom pitanju. Umor na najmanjem volumenu koji aplikacija zna je skoro
sigurno posao ili san, ne trening, a plafon oporavka ne zavisi od njega; a snaga koja se oporavlja posle teškog bloka
nije dokaz da je minimum niži, i spustila bi sledeći cilj održavanja, jer je on upravo MEV. Petlja bi se zatvorila
na dole. Ćutanje košta samo to što blok održavanja ništa ne uči, što mu nije ni svrha.

Alternativa koja je odbačena: pustiti samo MEV da se pomera naviše kad snaga padne na minimumu. To je jedino što bi
održavanje moglo da nauči o sebi („ni ovo ne održava"), ali bi pad posle bolesti ili putovanja podizao minimum svim
budućim blokovima. Ako se pokaže da je potrebno, to je jedan red u `LearnsVolumeLimits`.

## Merenja

**1. Šta pukne kad se samo doda vrednost.** Sa trećom vrednošću i bez ijednog pravila, od 1682 testa pala su 3, i
sva tri su zapisani spiskovi nad trenažnim nedeljama ugrađenih šablona (ispod MEV-a, mišići bez serije, preporuke iz
vodiča). Oni opisuju blokove rasta, pa se petlja `TemplateWeekSimulation.EveryTrainingWeek` i dve petlje u
`WorkoutTemplateCatalogTests` ograničavaju na `GrowthGoals`, a održavanje ima svoju (`EveryMaintenanceWeek`). Ostalih 96
slučajeva koji nabrajaju cilj (mreže propisa, periodizacije, ciljnog volumena, strukture kataloga) prošlo je sa
održavanjem bez izmene, što je prva provera da nijedna opšta osobina nije pretpostavljala dva cilja.

**2. Gde stiže nedelja održavanja.** Prva trenažna nedelja svakog ugrađenog šablona na svakom nivou, posle
balansiranja, mišići sa MEV-om većim od nule (zapisano tačno u testu): od 274 mišića koje šablon trenira **182 (66%) stoji
tačno na MEV-u**, 28 ispod (šablon nema dovoljno posla za mišić, isto kao u rastućem bloku), 64 iznad, od toga 21 za više
od dve serije; još 23 para šablon-mišić nema nijednu vežbu. Prosek je **7.1 serija po mišiću naspram 10.8** u hipertrofiji
(66%). Razlog iznad je najmanji broj serija po vežbi (dve): vežba se ne ukida, pa mišić sa mnogo vežbi ili velikim
sporednim udelom ne može da side na MEV. Kroz pravi generator (srednji nivo, Upper/Lower): grudi 10 serija nedeljno u
održavanju, 16 u hipertrofiji.

**3. Merenje vraćanjem, jedna izmena po merenju** (na commit-ovanom stablu, sa ponovnom gradnjom između):

| Vraćeno | Obara |
|---|---|
| učenje granica ostaje uključeno | 1 |
| korak na vrhu opsega se uzima (glavni poziv) | 1 |
| korak na vrhu opsega se uzima pri nastavku posle deload-a | **0**, pa dodat test (sada 1) |
| ciljni volumen održavanja je MAV | 5 |
| plan održavanja se smenjuje | 4 |
| opseg je hipertrofijski | 6 |
| rezerva je dva umesto tri | 7 |
| `ModelFor` vraća traženo | 5 |
| napravljeni blok zadržava traženi model | 1 |
| generator zadržava traženi model | **0**, pa dodat test (sada 1) |
| predlog blokova zadržava predloženi model | 1 |
| motor zanemaruje zastavicu | 3 |

Klijent: izbor modela ostaje posle promene cilja na održavanje 2, blok iza održavanja se smenjuje 1, izbor rasporeda se nudi
i za održavanje 1, nema opisa cilja 1, rečenica o sledećem bloku 1. Jedno vraćanje nije palo ništa (model pri dodavanju
bloka), jer je za cilj koji sledi iza održavanja model već ravan; taj red je izbačen umesto da se brani testom.

Dva vraćanja koja su prošla sa celim paketom zelenim bila su isto što i u rundama 12 i 16: pravilo se primenjuje na
drugom mestu poziva i nijedan test ne dolazi do tog mesta. Oba su dobila test koji ih hvata.

**4. Uživo, lokalni API i 375 px.** Plan napravljen kroz čarobnjak: cilj „Održavanje", izbor rasporeda nestaje i piše
„Ravan · 4 ned. Održavanje je uvek ravno…", opis cilja je ispod izbora. Server je upisao cilj 2, model 0, četiri nedelje,
osam treninga; složene vežbe 6-10 i izolacije 10-20, sve uz RIR 3. Trening odrađen na vrhu opsega uz ciljni RIR: svih šest
vežbi je dobilo istu težinu za sledeću nedelju (107.5, 77.5, 15, 17.5, 18, 17.5), bez strelice naviše. Drugi trening
sa svim serijama do otkaza: nedelja obračunata, ocena umora 0.197, **nula redova pomaka i nula ličnih granica**. Ekran
„Trening" pokazuje „Cilj: Održavanje", plan „Održavanje · Ravan, 4 ned."; nema vodoravnog prelivanja.

Brojevi: server 1682 → **1818** (96 postojećih slučajeva koji nabrajaju cilj i 40 novih), klijent 459 → **466**.
`dotnet build`, `npm run build` prolaze.

## Šta nije urađeno, i zašto

- **Doza ispod MEV-a.** Literatura kaže da bi trećina ili devetina doze držala (Bickel i sar. 2011), a jedan trening
  nedeljno sa jednom serijom po vežbi do 32 nedelje (Spiering i sar. 2021); oba rada su čitana preko sažetaka, ne celog
  teksta. MEV je oprezno čitanje „najmanje što drži", a pod njim aplikacija ništa ne opisuje; niži udeo je jedna
  konstanta u `WeeklyVolumeTarget.ForGoal`.
- **Broj vežbi po treningu.** Ostaje isti, svaka vežba šablona ima najmanje dve serije. Kraći trening je lični šablon sa
  manje vežbi i cilj održavanje.
- **Planirani deload ostaje** (ravan blok, srednji i napredni nivo; početnik ga trenira). Kod rezerve 3 njegovo „plus dva"
  seče gornja granica rezerve (`Periodization.MaxRir` = 4), pa je rasterećenje za jednu rezervu i 90% težine; pod testom
  (`ATiredWeek_StillPullsADeload_InAMaintenanceBlock`). Ocena umora radi i može da ga povuče ranije.
- **Početna težina novog bloka** se izvodi iz maksimuma kao i svuda: održavanje ne gura težinu, ali ne zamrzava ni procenu
  maksimuma, koja se i dalje upisuje iz serija (uz RIR do 3).
- **Upozorenje šablona** iz čarobnjaka („dva treninga nedeljno… rast je mali") ostaje i uz održavanje, gde je beznačajno.
- Blok koji je već generisan ne menja cilj; postojeći blokovi se ne dodiruju.
- Nije proveren pravi telefon; PostgreSQL trka pri istovremenom završetku treninga nije dirana ovom izmenom.

Usklađivanje sa tezom: `docs/teza-uskladjivanje.md`, odeljak 13 („Ciljevi bloka").
