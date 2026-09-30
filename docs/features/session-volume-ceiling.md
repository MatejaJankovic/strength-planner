# Granica serija po treningu

**Grana:** `feature/session-volume-ceiling`

Treća grana iz revizije nauke o treningu (odeljak F), nalaz F7.

## Problem

Balansiranje serija gleda nedelju: cilj volumena, MRV i najviše šest serija po vežbi. Koliko
od toga jedan mišić dobije u **jednom** treningu nije gledao niko, pa je to bilo ono što šablon
slučajno sabere.

Izmereno probom nad svakim ugrađenim šablonom, na svakom nivou, cilju i modelu, u svakoj
trenažnoj nedelji — 15.132 para (trening, mišić), posle balansiranja:

| Koliko serija jednog mišića u jednom treningu | Parova |
|---|---|
| preko 8 | 1.493 |
| preko 11 | 473 |
| najviše | **18** — grudi na Push danu Push/Pull/Legs, leđa na Day B šablona Full Body (4 dana) |

Uživo, srednji nivo, hipertrofija, Push/Pull/Legs:

| Blok | Push dan, serije za grudi |
|---|---|
| ravan, nedelja 1 | 16 (Bench Press 6, Cable Fly 5, Dumbbell Fly 5) |
| linearan, nedelja 1 | 18 (sve tri po 6) |

Balansiranje nije samo propuštalo problem, nego je ponekad i dodavalo baš pretrpanom danu:
dvodnevni Full Body (početnik, snaga, linearan) propisuje prvog dana 9 serija grudi, a
balansiranje ga je podiglo na **11**, dok je drugi dan ostao na 5.

## Šta kažu izvori

- **Priručnik** (str. 10–11): MAV je „8–20 serija nedeljno po mišićnoj partiji, odnosno 4–8
  serija po treningu", i „previše vežbi i serija u jednom treningu može dovesti do većih
  mišićnih oštećenja i produžiti oporavak".
- **Remmert, Pelland, Robinson, Hinson i Zourdos (2025)**, meta-regresije volumena po treningu
  (SportRxiv, preprint): odnos je pozitivan, ali sa opadajućim prinosom, i posle oko **11
  frakcionih serija** po treningu korist od daljih serija za hipertrofiju više nije merljiva.
- **Pelland i sar. (2025)**: kad je nedeljni volumen isti, frekvencija za hipertrofiju gotovo
  ne menja ishod. Zato granica ne tvrdi da je 16 serija u jednom treningu štetno, nego da
  serije preko ~11 ne donose ništa merljivo, a oporavak troše.

## Odluka: 11, a ne 8

Plan (odluka D6) je predlagao priručnikovih 8 kao meku kaznu. Izmereno na istih 15.132 para
pre nego što je išta ušlo u kod:

| Varijanta | Preko 8 | Preko 11 | Najviše | Nedeljni volumen skinut (zbir) | Nedelja-mišić ispod MEV |
|---|---|---|---|---|---|
| bez granice (bilo) | 1.493 | 473 | 18 | — | 800 |
| meka kazna iznad 8 | 1.366 | 442 | 18 | 136,5 | 808 |
| meka kazna iznad 8 + prenos serije između treninga | 1.246 | 338 | 18 | 605 | 833 |
| tvrda granica 8 | 81 | 21 | 12 | 3.884,5 | 937 |
| **tvrda granica 11** | 1.487 | **21** | **12** | 985,5 | 837 |

(Ispod MEV se broji od 6.630 parova nedelja-mišić; 800 ih je ispod MEV-a i bez ikakve granice.)

- **Meka kazna ne radi ono što obećava.** Pomogla bi u slučaju iznad (dvodnevni Full Body:
  10 i 6 umesto 11 i 5), ali pretraga pomera jednu seriju odjednom. Kad nedelja
  stoji na cilju, serija oduzeta pretrpanom treningu košta cilj, a dodata drugom ga probija,
  pa nijedan pojedinačan korak nije isplativ. Ni uz dodat potez „prenesi seriju" najveći
  trening nije pao ispod 18, a kazna je kroz sprezanje sa kaznom za odstupanje od propisa
  skinula 605 serija nedeljnog volumena kao nuspojavu.
- **Tvrda granica 8** skida serije koje po literaturi još mere rast: 3.884,5 serija, a
  Push/Pull/Legs-u, koji grudi trenira samo u Push danu, u ravnom bloku ostavlja 8 nedeljno — ispod
  MEV-a srednjeg nivoa (10).
- **Tvrda granica 11** radi i sa potezima od jedne serije: serija preko granice košta četiri,
  a nedostajuća serija nedelje jednu, pa se višak skida, i ako postoji drugi trening za taj
  mišić, tamo se dodaje.

Priručnikovih 4–8 ostaje ono što i jeste: MAV podeljen na treninge u nedelji, dakle gde trening
obično pada, a ne tačka posle koje serija prestaje da deluje.

## Rešenje

- `ExerciseSetSlot` zna kom treningu pripada (`SessionId`); `WeeklySetPlanner` prosleđuje
  `WorkoutSessionId`. Parametar je obavezan, ne podrazumevan: kompajler je tako našao svako
  mesto koje pravi slot.
- `WeeklySetAllocation.CostDelta`: serija jednog mišića preko
  `TrainingConstants.MaxSetsPerMusclePerSession` (11) u jednom treningu košta koliko serija
  preko MRV-a (`CeilingPenalty`). Broji se kao nedeljni volumen — sekundarni mišić pola serije —
  i samo planirano: trening u koji je već nešto upisano nije slot.
- Prozor propisa ostaje granica balansiranja (±2 serije po vežbi). Linearna nedelja 1 propisuje
  18 serija grudi u Push danu, i predlog staje na **12** — sve tri vežbe su dve serije ispod
  propisa. Granica je razlog da se spusti koliko sme, a ne ovlašćenje da pregazi propis.
- **Push/Pull/Legs dobija upozorenje**, kao dvodnevni šablon: svaki mišić jednom nedeljno, pa
  grudi i leđa mogu da ostanu ispod ciljnog volumena.
- Napomena na kartici vežbe govorila je da je predlog pomeren „da bi nedelja ostala u ciljnoj
  zoni volumena". Na Push danu je Cable Fly spušten sa 4 na 3 dok je nedelja bila **ispod**
  cilja, pa je sada tekst: nedelja cilja svoju zonu, a nijedan trening ne sme da pretrpa jedan
  mišić. Koji je od dva razloga pomerio baš tu vežbu predlog ne pamti, pa ekran to i ne tvrdi.
- „Prati moj šablon" (`SetAllocation.FollowTemplate`) se ne dira: balansiranje se tu ne pokreće, pa
  ni granica. Lični šablon sa 20 serija za jedan mišić u danu i tim izborom ih zadržava.
- Postojeći blokovi: balansiranje se ponovo pokreće posle svakog završenog treninga, nad
  treninzima u koje ništa nije upisano. Tekući blok će granicu dobiti posle sledećeg
  završenog treninga; ono što je već odrađeno se ne menja.

## Šta se promenilo, izmereno

Referentni nivo (srednji, hipertrofija, ravan blok, nedelja 1), svaki par koji je bio preko 11:

| Šablon, trening | Mišić | U treningu | Nedelja | Cilj nedelje |
|---|---|---|---|---|
| Push/Pull/Legs, Push | grudi | 16 → 11 | 16 → 11 | 16 |
| Push/Pull/Legs, Pull | leđa | 15 → 11 | 18 → 14 | 18 |
| Push/Pull/Legs, Legs | kvadriceps | 12 → 11 | 12 → 11 | 14 |
| Push/Pull/Legs, Pull | biceps | 12 → 10,5 | 12 → 10,5 | 14 |
| Push/Pull/Legs, Push | triceps | 12 → 11 | 12 → 11 | 12 |
| Full Body (4 dana), Day B | leđa | 13 → 11 | 18 → 17 | 18 |
| Upper/Lower + PPL, Pull | leđa | 12 → 11 | **18 → 18** | 18 |
| Legs Specialization, Upper A | leđa | 12 → 11 | 18 → 17 | 18 |

Kod Upper/Lower + PPL višak je preseljen u drugi trening, pa nedelja nije izgubila ništa. Kod
Full Body (4 dana) od dve skinute serije preseljena je jedna, a kod Legs Specialization nijedna:
zgib u drugom treningu dostiže šest serija, najviše po vežbi. Višak se seli koliko drugi trening
može da primi, ne u celini.

Nedelje koje su tek sada ispod MEV-a: **37** od 6.630 —

| Šta | Nedelja | Primer |
|---|---|---|
| Push/Pull/Legs, napredni, grudi | 26 | 15 → 11, MEV 12 |
| Push/Pull/Legs, početnik, biceps | 4 | 7,5 → 4,5, MEV 6 — biceps tu dolazi samo kao pola serije veslanja, a veslanje je spušteno zbog leđa |
| Upper/Lower + PPL, napredni, leđa | 7 | 12 → 11, MEV 12 |

To je cena frekvencije jednom nedeljno, i zato je upozorenje uz šablon.

## Provera

- `dotnet test`: **704** (bilo 693); `npm run build` i `npm test` (177) prolaze.
- Novi testovi:
  - `WeeklySetAllocationTests` — trening se drži ispod granice i kad nedelji fali serija;
    višak se seli u drugi trening istog mišića; sekundarni mišić broji pola serije, a višak se
    skida tamo gde je najjeftinije (ekstenzija, ne potisak); kad je prozor propisa potrošen,
    višak ostaje.
  - `SessionVolumeCeilingTests` — preko svakog ugrađenog šablona, nivoa, cilja, modela i
    nedelje: trening sme da ostane preko granice samo ako je svaka vežba kojoj je taj mišić
    glavni već na najnižem što propis dozvoljava; nijedan trening nije više od jedne serije
    preko; Push/Pull/Legs ostaje ispod MAV-a i nosi upozorenje; Full Body, Upper/Lower i
    Upper/Lower x3 na referentnom nivou dobijaju isti predlog kao pre, vežba po vežba; granica
    menja samo nedelje u kojima bi je neki trening prešao.
  - `TemplateWeekSimulation` sastavlja nedelju ugrađenog šablona istim domenskim pozivima kao
    generator i balansiranje. Katalog-testovi su do sada sabirali propis; ono što korisnik
    dobija posle balansiranja nisu videli.
- Merenje vraćanjem (commit, pa vraćeno staro pravilo, rebuild): bez granice pada **7 od 704**
  — četiri nova testa alokatora i tri kataloška. Četiri preostala kataloška testa tvrde da
  granica **ne** deluje tamo gde ne treba, pa bez nje prolaze. Sa granicom spuštenom na 8
  padaju **4** testa, među njima dva od ta četiri (Full Body na referentnom nivou i „granica
  menja samo nedelje koje bi je prešle") — čuvari nisu prazni. Upper/Lower i Upper/Lower x3 na
  referentnom nivou ne prelaze ni 8, pa njihovi redovi prolaze i tada.
- End-to-end, uživo, u oba smera (staro pravilo, rebuild, API; pa novo, rebuild, API):

  | Blok | Push dan pre | Push dan posle |
  |---|---|---|
  | ravan, nedelja 1 | grudi 16: Bench 6, Cable Fly 5, Dumbbell Fly 5 | grudi 11: Bench 4, Cable Fly 3, Dumbbell Fly 4 |
  | linearan, nedelja 1 | grudi 18: sve tri po 6 | grudi 12: sve tri po 4 (propis 6) |

- Na ekranu (375 px, bez horizontalnog skrola): upozorenje stoji ispod izbora Push/Pull/Legs u
  čarobnjaku plana, a kartica Cable Fly kaže „Predlog serija je spušten sa 4 na 3. Nedelja
  cilja svoju zonu volumena, a nijedan trening ne sme da pretrpa jedan mišić."

## Posle revizije

Pregled (dva recenzenta, svaki nalaz srednje težine pred dva osporavača) je potvrdio dva nalaza i
oba su ispravljena u ovoj grani:

- **Objašnjenje posle treninga je lagalo o rezovima granice.** Postojeći blok dobija granicu
  pri sledećem završenom treningu, i to kroz spisak „Predlog serija je prilagođen". Taj spisak
  je uz svaku promenu pisao mišić koji je čitao samo nedeljni cilj: rez koji je tražila granica,
  dok je nedelja ispod cilja, dobijao je **nijedan** mišić, ili mišić koji stoji ispod cilja
  pored strelice nadole. Pravac se uz to računao od propisa, a ne od onoga što je korisnik
  video, pa je veslanje spušteno sa 6 na 5 uz propis 4 čitano kao podizanje. Objašnjenje je
  sada `SetChangeExplanation` u domenu, sa testovima: pravac od prethodnog predloga, i razlog
  — `WeeklyTarget` ili `SessionCeiling` — koji ekran piše kao „pun trening". Test preko svih
  ugrađenih nedelja traži da svaki rez granice dobije baš taj razlog.
- **Zaglavlje tog spiska** je i dalje tvrdilo da je svaka promena tu „da bi nedelja završila u
  ciljnoj zoni" — isti tekst koji je već ispravljen na kartici vežbe. Sada kaže oba razloga.

Provereno uživo baš na slučaju iz nalaza: Push/Pull/Legs blok generisan starim pravilom
(rebuild sa vraćenim pravilom, API), pa novo pravilo (rebuild, API), Push odrađen kako je
propisan i završen u pregledaču na 375 px. Spisak posle treninga: Barbell Row 6 → 5, Pull-up
6 → 4 i Face Pull 6 → 4 uz „Back · pun trening", Leg Extension 6 → 5 uz „Quads · pun trening".
Pre ispravke su Pull-up i Face Pull stajali bez mišića, a veslanje uz „Back" dok su leđa bila
14 naspram cilja 18. Merenje vraćanjem, svako posebno: bez razloga „granica" padaju 2 od 711
testova, a sa pravcem čitanim od propisa umesto od prethodnog predloga takođe 2. Test preko
svih ugrađenih nedelja pada u oba.

Pet nalaza niske težine bile su tvrdnje jače od merenja, i sve su ispravljene: višak se seli u
drugi trening samo koliko taj može da primi (Full Body (4 dana) preseli jednu od dve serije,
Legs Specialization nijednu); upozorenje i uputstvo su pominjali samo grudi i leđa, a padaju i
ruke; odbačena granica 8 ostavlja Push/Pull/Legs-u 8 serija grudi samo u ravnom bloku (u
nedelji volumena 12); „ostaje na 12" važi za ugrađene šablone, lični može i više; i opis jednog
testa je obećavao više šablona nego što proverava.

Jedan nalaz je oboren kao nešto što ova grana nije uvela, ali je stvaran i ide dalje:
**balansiranje seče složenu vežbu pre izolacije** kada ona rasterećuje dva mišića odjednom — u
bloku snage naprednog nivoa bench pada na 2 serije dok razvlačenja zadržavaju 5. Osporavači su
izmerili da je to svojstvo cene po mišiću starije od granice: bez nje ima 630 takvih slučajeva
(370 u blokovima snage), sa njom 633. Rešava se u grani za sastav bloka snage (nalaz F5), gde
složena vežba i inače dobija ulogu koju sada nema.

## Ograničenja

- Granica deluje samo u prozoru propisa. Nedelja volumena linearnog i obrnutog modela na
  Push/Pull/Legs i dalje nosi 12.
- „Leđa" i „Ramena" su zbirne grupe (runda 13), pa je i granica po zbiru: 11 serija „leđa"
  mogu biti latovi i kičmeni mišići zajedno.
- Za snagu isti rad nalazi da posle oko **dve direktne serije** po treningu dalji rast 1RM-a
  nije merljiv. To ovde nije primenjeno: blok snage i dalje cilja nedeljni volumen, a granica
  od 11 je hipertrofijska. Da li blok snage treba da ima manje serija po vežbi je pitanje za
  sastav bloka snage (nalaz F5), ne za ovu granicu.
- Remmert 2025 je preprint, a 11 je tačka posle koje korist nije *merljiva*, ne tačka posle
  koje je serija štetna.
