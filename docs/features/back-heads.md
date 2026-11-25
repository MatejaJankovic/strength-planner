# Leđa po glavama

**Grana:** `feature/back-heads`

Runda 20, stavka 20.3. Odluka vlasnika: podeliti leđa po uzoru na rame (#109), posle merenja, uz mere za mišiće koje
promena istiskuje (netrenirani mišić se broji kao nula serija).

## Problem

„Back" je bila jedna zbirna grupa sa jednim ciljem (10/18/25) za vertikalno i horizontalno povlačenje. Rame je istu
grešku imalo sa bočnim deltoidom (#109); ovde je pitanje da li je leđa kriju isto.

Izmereno nad svim ugrađenim trenažnim nedeljama (972: šablon × nivo × cilj × model × nedelja), posle balansiranja, uz
vertikalno povlačenje (zgib, Lat Pulldown, Straight-Arm Pulldown) i horizontalno (veslanja i Face Pull, upola) iz kataloga:

- u **827** od 972 nedelja zbirna grupa dostiže MEV, a u **333** od njih (40%) jedna polovina ima manje od pola MEV-a;
  vertikalno 107, horizontalno 235;
- na Push/Pull/Legs vertikalno povlačenje je ispod pola MEV-a u svih 82 nedelja u kojima zbirna grupa „ima dovoljno";
- 67 nedelja dostiže MEV samo zahvaljujući mrtvom dizanju (pola serije).

## Izvor, i šta ga nema

RP ne daje granice posebno za latove i gornja leđa. Daje dve stvari (rpstrength.com, članak o leđima):
**pravilo** da se volumen deli otprilike podjednako između vertikalnog i horizontalnog povlačenja, i **MRV** za ceo
deo: pri četiri treninga leđa tipično oko 30 serija, otprilike 15 vertikalnog i 15 veslanja.

| Glava | MEV / MAV / MRV (srednji nivo) | Izvor |
|---|---|---|
| latovi | 5 / 9 / **15** | MRV: RP (pola od oko 30); MEV i MAV: polovina zbirne grupe (10/18), **bez izvora** |
| gornja leđa | 5 / 9 / **15** | isto |

MEV i MAV su odluka o modelu, ne podatak, i zapisani su kao takvi u katalogu, u uputstvu i na ekranu volumena.

Prva verzija je uzela i MRV kao polovinu (13 i 12): to je bio izmišljen broj, a imao je posledicu koja ga je odala. Gornja
leđa na Upper/Lower x3 su po propisu imala 14 serija uz taj MRV, pa je test „šablon ne prelazi MRV" pao; popravka
šablonom (veslanje → pulldown) je naprednom oduzela MEV gornjih leđa, što uputstvo naprednom obećava za taj šablon. Sa MRV
15 isti šablon ne prelazi ništa, i izmena šablona je vraćena.

**Opružači nisu glava.** Plan je tražio tri glave (latovi, gornja leđa, opružači). Za opružače nema ni pravila ni
granice, a mrtvo dizanje i rumunsko ih već broje (pola serije) u gornjim leđima. Treća glava bi imala izmišljene
granice, pa je izostavljena i to piše na ekranu.

## Doprinosi

| Vežba | Ranije | Sada |
|---|---|---|
| Pull-up, Lat Pulldown, Straight-Arm Pulldown | Back 1.0 | Lats 1.0 |
| Barbell Row, Seated Cable Row | Back 1.0 | Upper Back 1.0 |
| Deadlift, Romanian Deadlift | Back 0.5 | Upper Back 0.5 |
| Face Pull | Back 0.5 | Upper Back 0.5 (zadnji deltoid 1.0 ostaje) |

Veslanje ne dobija udeo u latovima, ni zgib u gornjim leđima, iako rade i ono drugo: kao kod potisaka i glava ramena,
granice po glavi važe za direktan rad. Svaka vežba koja je radila leđa i dalje ih radi u istoj meri (test
`EveryExerciseThatTrainedTheBack_StillCreditsItsFullContribution`).

## Šabloni

Pretraga po uzoru na #109: za svaki šablon jedna zamena ili jedno umetanje vežbe za leđa, merena nad svim nedeljama i
svim mišićima (netrenirani broj kao nula). Prvi prolaz je birao najbolji zbir, a to je često bila izmena koja šteti
drugom mišiću (veslanje umesto potiska ili Front Squat-a); drugi je tražio samo izmene koje nijedan drugi mišić ne
pogoršavaju. I to nije bilo dovoljno: mišić sa MEV-om nula (zadnji deltoid) se nikad ne računa kao „ispod", pa čista
izmena ume da mu skine jedinu direktnu vežbu. Zato je poslednji korak bio ručno, uz probu po izmeni.

Napravljene su dve izmene:

| Šablon | Izmena | Efekat |
|---|---|---|
| Push/Pull/Legs | Pull: Lat Pulldown umesto Seated Cable Row-a | latovi ispod MEV-a 62 → 49 parova |
| Legs Specialization | Upper B: Seated Cable Row umetnut drugi (posle zgiba) | gornja leđa ispod MEV-a 62 → 18 |

Probane i odbačene, svaka sa razlogom iz merenja:

- **Upper/Lower x3, Upper C: veslanje → Lat Pulldown.** Rešavalo je MRV od 12 (vidi gore), pa je kad je MRV postao 15
  izgubilo smisao, a naprednom je oduzimalo gornja leđa.
- **Push/Pull/Legs x2, Pull B: Face Pull umesto Rear Delt Fly-a** (−4 para). Obara pravilo iz runde 14: glavno dizanje
  se seče pre pomoćne vežbe za isti mišić (`NoMainLiftIsCut_...`), jer Face Pull daje gornjim leđima pola serije.
- **Full Body, Full Body (2 dana), Upper/Lower:** svako umetanje veslanja ili pulldown-a uzima mesto koje nosi drugi
  mišić (grudi, triceps, biceps, kvadriceps, bočni deltoid), ili ništa ne menja, jer napredni vežbu ne stigne.
  Najbolje umetanje u Full Body (Front Squat umesto veslanja) popravlja 25 nedelja gornjih leđa na račun 4 + 4 kvadricepsa i
  gluteusa i ostavlja jedan čučanj nedeljno; nije uzeto.

Ukupno, svi mišići, parovi nedelja-mišić ispod MEV-a:

| | ispod MEV-a | latovi + gornja leđa | iznad MRV-a |
|---|---|---|---|
| `main` (zbirna leđa) | 1454 | 145 (zbirna) | 0 |
| glave, stari šabloni, MRV 13 / 12 | 1614 | 96 + 209 | 3 |
| **glave, MRV 15, dve izmene šablona** | **1557** | **83 + 165** | **0** |

Svaki drugi mišić je **tačno isti** kao pre (trbuh 448, biceps 144, listovi 366, grudi 45, gluteusi 110, zadnja loža
40, kvadriceps 8, bočni deltoid 70, triceps 78). Ukupno je to 103 para više nego sa zbirnom grupom, i to je cena da se
vidi ono što je zbirna grupa krila: prelaz sa 145 na 248 nije novi manjak, nego broj koji je ranije bio sakriven.

## Cena, rečena testovima

Podela je promenila osam zabeleženih spiskova i granica. Svaki je prepisan tačno, ne razblažen:

- **`EveryTemplate_TrainsEveryMajorMuscleGroup`:** napredni nema nijednu vežbu za jednu glavu na četiri šablona
  (Full Body (2 dana), Legs Specialization i Upper/Lower + Push/Pull/Legs za gornja leđa, Push/Pull/Legs za latove).
  Spisak (`AdvancedPullingGaps`) je tačan i pada kad izuzetak prestane da važi.
- **`OnlyAbsAndCalves_AreEverLeftWithoutASet`:** ista četiri para (sada se zove po istoj svrsi, a ne po trbuhu i
  listovima).
- **`AnAdvancedHypertrophyWeek_FallsBelowMev_OnlyWhereRecorded`:** `push-pull-legs/Back` i `upper-lower-ppl/Back` postaju
  latovi i gornja leđa; dodata `full-body` i `legs-specialization` (gornja leđa), sve zbog sastava treninga.
- **`AnAdvancedStrengthBlockOnFullBody_...`:** leđa 11.5 uz MEV 12 u tri nedelje postaju gornja leđa 3.5 do 5.5 uz MEV 6
  u jedanaest nedelja. Napredni u bloku snage tu uzima dve složene vežbe po treningu i nijedna nije veslanje.
- **`TheTemplatesTheGuideRecommends_...`:** uputstvo je tvrdilo da Upper/Lower x3 naprednom daje MEV za svaki mišić. Više
  ne daje: gornja leđa u hipertrofiji su za jednu seriju ispod (5 naspram 6) u tri nedelje, kao i na Upper/Lower
  (4 dana). Uputstvo je ispravljeno uz test.
- **`TemplatesOfThreeDaysOrMore_ReachMevAtTheReferenceLevel`:** latovi na Push/Pull/Legs na srednjem nivou imaju po
  propisu 4 uz MEV 5 (jedan zgib); izuzetak imenom, uz napomenu da ih balansiranje diže iznad MEV-a.
- **`SessionVolumeCeilingTests`:** tri nedelje Push/Pull/Legs x2 na srednjem nivou (hipertrofija, obrnuti i linearno
  rastući model) menjaju predlog sa granicom i bez nje iako nijedan trening ne prelazi granicu: dve vežbe za biceps od po
  5 serija prelaze MRV, pa se dve serije skidaju, a granica menja samo redosled sečenja (4 + 3 naspram 3 + 4). Test
  sada beleži te nedelje imenom i dokazuje da propis prelazi MRV neke grupe. (Prva verzija podele ih je imala šest, na
  početniku i gornjim leđima; sa MRV 15 su nestale, a ove tri su nove, i podela im je promenila put, a ne uzrok.)
- **`SecurityPolicyTests.MuscleGroupCap_...`:** gornja granica broja mišićnih grupa po vežbi je sada 13.

## Migracija `SplitBackHeads`

Pre pisanja izmereno nad razvojnom bazom: 25 naučenih granica za „Back", 8 redova sistemskih vežbi i 6 redova ličnih
vežbi (sve „RLS Machine Row", dakle veslanje).

- Glave se upisuju u `MuscleGroups` u samoj migraciji, jer redovi koji prelaze trebaju kao strani ključ.
- **Naučene granice se ne kopiraju, nego se prevode.** Naučena granica je pomak od podrazumevane vrednosti (najviše 50%
  nje), a seed glave je drugi (5/9/15 naspram 10/18/25), pa bi kopija značila nešto drugo. Svaka glava dobija svoj seed
  plus **polovinu** pomaka zbirne grupe, a redosled MEV < MAV < MRV se vraća gde ga je zaokruživanje pokvarilo. (8/14/19
  postaje 4/7/12.)
- **Lična vežba sa „Back" prelazi na gornja leđa**, sa istim brojem. Koju glavu lična vežba radi, iz reda se ne vidi, a
  aplikacija nema izmenu mišića postojeće vežbe; veslanje je daleko najčešća lična vežba za leđa, pa su gornja leđa
  izabrana. Ko želi latove, pravi vežbu ponovo.
- „Back" se briše. Kaskada nosi njen seed, naučene granice i redove sistemskih vežbi, a usklađivanje kataloga pri startu
  (`ReconcileSystemExercisesAsync`) upisuje doprinose glava.
- `Down` vraća sve u jednu grupu, ali **nije tačan**: pomak od jednog koraka se polovljenjem zaokruži na nulu, pa
  povratak ne vraća ono što je bilo (10/17/24 → 10/18/25). Proveren je red po red: 25 naučenih granica postaje 25 + 25,
  6 ličnih vežbi prelazi, 13 grupa; Down pa Up vraća isto stanje.

Provera posle primene: 25 + 25 naučenih granica, MEV 4 do 6, MAV 7 do 11, MRV 12 do 18; 6 redova ličnih vežbi na gornjim
leđima; 13 mišićnih grupa. Pravi servis nad PostgreSQL-om nije pokretan iz testova (testovi rade nad SQLite-om), pa
migraciju brani samo ova provera i testovi koji drže seed-ove (`TheHeadsHaveTheSeedsTheMigrationAssumes`).

## Provera

REZULTATI

## Šta ovo ne rešava

- Opružači nisu glava, i to piše na ekranu.
- Napredni vežbač, sa jednom složenom vežbom po treningu, i dalje na dan za povlačenje bira ili vertikalno ili
  horizontalno: latovi na Push/Pull/Legs, gornja leđa na tri šablona. Zbirna grupa je to krila; sada se vidi.
- MEV i MAV po glavi nemaju izvor. Kad ga neko nađe, menja se jedan red kataloga i konstante u migraciji.
- Stari blokovi zadržavaju stare šablone. Novi važe od sledećeg generisanog bloka.
