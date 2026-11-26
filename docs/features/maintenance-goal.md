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
- nema **seriju do otkaza za procenu maksimuma** (`CalibrationSet.AppliesTo` čita cilj), ni uz lični šablon;
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
| `CalibrationSet.AppliesTo` | čita propis, ne cilj. Ugrađeni propis (10 + 3 = 13, iznad Epley granice 12) bi preskočio sam, ali lični šablon zadržava svoj opseg: 5-8 uz tri u rezervi staje na 11, pa bi poslednja nedelja propisala seriju do otkaza | **čita i cilj** (pregled #129); granice, ocena umora, polazna težina i ekran treninga čitaju isto pravilo |
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
| motor zanemaruje zastavicu | **5** (prvo merenje je dalo 3, pre testova za nastavak posle deload-a i pre pojačanja provere nad mrežom) |
| kalibracija ne čita cilj | 3 |
| ekran treninga pita sa ciljem rasta | 1 |
| kalibracione serije koje čitaju granice i ocena umora se traže sa ciljem rasta | 1 |

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

Brojevi: server 1682 → **1824** (96 postojećih slučajeva koji nabrajaju cilj i 46 novih), klijent 459 → **466**.
`dotnet build`, `npm run build` prolaze.

## Šta je pregled našao (ispravljeno)

Pregled je radio agent koji samo čita.

- **Kalibraciona serija je ostajala u bloku održavanja iz ličnog šablona**, uprkos uputstvu i ovoj belešci koji su
  tvrdili da je nikad nema. Pravilo je čitalo propis, ne cilj; test je proveravao samo ugrađeni opseg, pa nije mogao da
  vidi uneti opseg 5-8. Sada pravilo čita cilj, i tri mesta koja ga zovu su pod testom (domen, serije koje čitaju granice i
  ocena umora, ekran treninga); kontrola uz svaki opseg pokazuje da bi rastući blok istu seriju imao.
- Uputstvo je za deload tvrdilo „cilj + 2 (hipertrofija 3, snaga 4)", što je za održavanje (cilj 3) pogrešno: gornja
  granica rezerve je 4, pa je rasterećenje za jedan. Ispravljeno u uputstvu.
- Natpis u čarobnjaku je tvrdio da se težine ne podižu, a lakša serija od cilja je i dalje podiže (10 × 10 uz RIR 5 daje
  105 kg uz 100 kg). Natpis sada kaže da se težina ne podiže kad stigneš do vrha opsega. Broj nedelja rasporeda se čita iz
  istog spiska kao za ostale ciljeve, ne piše se rukom.
- Komentar je tvrdio da se i plafon MRV-a sudi samo iz nedelje blizu njega; samo se podizanje čita iz takve nedelje, a
  spuštanje iz svake nedelje koja je bar na minimumu, što i ovaj tekst kaže niže.
- Pregled kaže da zastavica „motor je zanemaruje" nije pala 3 testa nego najmanje četiri; izmereno je 5. Prvi broj je bio
  uzet pre dva testa koja su kasnije dodata.
- Dopunjeno: test koji drži da se dan održavanja sastavlja kao dan hipertrofije (čarobnjak pokazuje dane hipertrofije i za
  održavanje, što je tačno samo dok je to tako); mreža težina × korak × ponavljanja × rezerva sada traži da održavanje
  tačno na cilju drži težinu u svakoj tački, i da negde zaista drži tamo gde bi običan blok podigao; izvori (Bickel,
  Spiering) su označeni kao čitani preko sažetaka i u komentarima, ne samo u tekstu.

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
- **RIR 3 je poslednji korak punog kredita** (`StimulativeVolume.FullCreditRir`): serija upisana sa RIR 4 broji pola, pa
  vežbač koji upisuje labavije od plana pokazuje manje stimulativnog volumena od propisa, a balansiranje u narednim
  treninzima nedelje dodaje serije ka MEV-u. **Izmereno** (`ASetLoggedLooserThanThePlan_AddsSetsToTheRestOfTheWeek`, MEV 8,
  dan A od četiri serije, dan B propisan sa četiri): do RIR 3 dan B ostaje na četiri, uz RIR 4 postaje šest, uz RIR 5
  ostaje šest (balansiranje ne diže jedan trening preko polovine propisanog). Znači najviše dve serije više u jednom
  treningu, i to samo uz upis od RIR 4 naviše; nedelja tada izađe oko četvrtine iznad MEV-a. Ostavljeno: RIR 2 bi
  dao veću marginu, ali bi težina bila oko 2.6% veća od hipertrofijske i prekinula bi istu težinu po Epley-u, a
  lifteri pre potcenjuju rezervu nego što je precenjuju (Halperin i sar. 2022), pa upis od 4 pri stvarnom 3 nije
  uobičajen. Ako se u upotrebi pokaže drugačije, to je jedna konstanta u `GoalPrescriptions.ForGoal`.
- **Ocena umora pri malom volumenu.** Izmereno nad pravom ocenom (`InAFlatMaintenanceBlock_TheDeloadThreshold_...`):
  rezerva se meri prema dubini do koje serija sme da side ispod cilja (3), signal volumena je nula, a u ravnom bloku nema
  potvrđene snage ni odgovora. Srednji nivo dostiže prag samo kad je pola serija do otkaza a ostale su sve na RIR 0;
  napredni još traži pun signal rezerve. Deload se u održavanju dakle ne pokreće osim u nedelji u kojoj je sve bilo na
  granici (tri puta manje osetljivo na rezervu nego u hipertrofiji). Ostavljeno: volumen je ionako najmanji, pa deload tu
  daje malo, a nedelja u kojoj je sve na granici je ona kojoj treba.
- **Izolacije nose 10-20 uz RIR 3**, pa je relativno opterećenje mišića koje treniraju samo izolacije (bočni deltoid,
  listovi) nisko, a napredni ima jednu složenu vežbu po treningu (budžet je kao u hipertrofiji). Obrazloženje „intenzitet
  drži" tu važi slabije; to je izbor, ne greška.
- Promena cilja bloka na održavanje i nazad na rastući ostavlja raspored ravnim: raniji izbor se ne pamti.
- Blok koji je već generisan ne menja cilj; postojeći blokovi se ne dodiruju.
- Nije proveren pravi telefon; PostgreSQL trka pri istovremenom završetku treninga nije dirana ovom izmenom.

Usklađivanje sa tezom: `docs/teza-uskladjivanje.md`, odeljak 13 („Ciljevi bloka").
