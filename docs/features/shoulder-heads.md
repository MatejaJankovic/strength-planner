# Rame po glavama

**Grana:** `feature/shoulder-heads`

Korak 7 plana posle revizije (6. 11. 2026), stavka R5. Odluka vlasnika: RP procene po glavi,
prvo probe nad svim šablonima, pa izmena; posle probe: podela uz izmenu šablona.

## Problem

„Shoulders" je bila jedna mišićna grupa sa jednim ciljem (8/16/26) za tri glave koje grade
različite vežbe. Runda 13 je to rekla vežbaču na ekranu volumena, ali razdvajanje je ostavila
vlasniku, jer priručnik ne daje granice po glavi.

Proba je pokazala šta je zbirna grupa krila. Taj cilj su punili potisak iznad glave (ceo) i rad
za zadnji deltoid, a bočni deltoid, koji gradi samo bočno podizanje, dobijao je 6–9 serija
nedeljno. Većina šablona je imala jedno bočno podizanje nedeljno, a balansiranje vežbu pomera
najviše za dve serije, pa nije moglo da ga podigne do cilja.

## Granice: RP, srednji nivo

| Glava | RP raspon (MEV / MAV / MRV) | U aplikaciji |
|---|---|---|
| prednji deltoid | 0–2 / 4–8 / 8–12 | **0** / 6 / 10 |
| bočni deltoid | 6–8 / 8–24 / 24–30 | 8 / 16 / 26 |
| zadnji deltoid | 0–4 / 4–12 / 12–20 | **0** / 8 / 16 |

Izvori su RP članci o prednjem, bočnom i zadnjem deltoidu (rpstrength.com,
`front-delt-hypertrophy-training-tips`, `side-delt-hypertrophy-training-tips`,
`rear-delt-hypertrophy-training-tips`). Zvanične tabele po glavi nema, a brojevi trećih strana se
međusobno ne slažu, pa su uzeti samo RP-ovi članci.

- **MAV i MRV** su sredina raspona.
- **MEV prednjeg i zadnjeg deltoida je nula**, kao u RP-u. RP kaže da prednji deltoid većinu
  stimulusa ili ceo dobija od potisaka za grudi, a zadnji od veslanja. Model te serije glavama
  namerno ne pripisuje: horizontalni potisak ne daje udeo prednjem deltoidu, a veslanje ne daje
  udeo zadnjem. RP-ove granice važe za direktan rad povrh tih vežbi, a i balansiranje bi inače
  skidalo direktan rad. Direktan rad za te dve glave je zato dodatak, a ne minimum.
- **Bočni deltoid zadržava stare vrednosti zbirne grupe.** One su u njegovim rasponima, a
  naučene granice ramena se na njega prenose bez preračuna (vidi Migracija).
- **Leđa ostaju zbirna.** RP nema posebne granice za latove i gornja leđa, pa za podelu nema
  izvora.

MEV od nule model ranije nije znao: `ScaleLandmark` nije išao ispod jedne serije, a CHECK u bazi
je tražio `Mev >= 1`. Sada seed od nule ostaje nula na svakom nivou i pri učenju granica (drift
oko nule je nula), a CHECK traži `Mev >= 0`. Svaki seed veći od nule zadržava staru donju
granicu od jedne serije.

## Doprinosi

| Vežba | Ranije | Sada |
|---|---|---|
| Overhead Press, Dumbbell Shoulder Press | Shoulders 1.0, Triceps 0.5 | prednji 1.0, bočni 0.5, Triceps 0.5 |
| Lateral Raise | Shoulders 1.0 | bočni 1.0 |
| Rear Delt Fly | Shoulders 1.0 | zadnji 1.0 |
| Face Pull | Shoulders 1.0, Back 0.5 | zadnji 1.0, Back 0.5 |

Horizontalni potisci i veslanja ostaju bez udela u ramenima (runda 13 i #100). Razlog je sada
drugi: ne zbog zbirnog budžeta, nego zato što MEV nula već računa sa njima.

## Šabloni: mesto bira pretraga

Proba (`TemplateWeekSimulation`, svih 972 trenažnih nedelja: šablon × nivo × cilj × model ×
nedelja) je za svaki šablon isprobala svako umetanje, zamenu i pomeranje bočnog podizanja posle
poslednje složene vežbe dana. Merila je parove nedelja-mišić ispod MEV-a za **sve** mišiće i
iznad MRV-a. Uzeta su samo pomeranja bočnog podizanja. Pomeranja Rear Delt Fly-a koja je
pretraga nudila popravljala su druge mišiće kao usputnu posledicu, a obrnula bi raspored iz
runde 13.

| Šablon | Izmena |
|---|---|
| Full Body (2 dana) | Day B: bočno podizanje posle pregiba (ispred bi srednjem nivou uzelo jedini biceps) |
| Full Body | Day A: bočno podizanje prva izolacija; dodato na Day B i Day C (na C posle Hammer Curl-a, zbog #105) |
| Push/Pull/Legs | Push: bočno podizanje prva izolacija; Pull: umesto Cable Crunch-a |
| Upper/Lower | Lower A: umesto Leg Extension-a, posle listova (pre pregleda je stajalo ispred njih, pa je početnik ostajao bez listova) |
| Upper/Lower + PPL | Lower: umesto Leg Extension-a (ostaje na Legs danu) |
| Push/Pull/Legs x2 | Legs A: umesto Leg Extension-a (ostaje na Legs B) |
| Legs Specialization | Legs A: dodato bočno podizanje (dan za noge ima vremena, a nema volumena za noge, runda 13) |
| Full Body (4 dana), Upper/Lower x3 | bez izmene |

### Rezultat, parovi nedelja-mišić

Brojano na dva načina. Prva kolona broji samo mišiće koje nedelja trenira, kako je merila
pretraga. Druga mišić bez ijedne serije broji kao nula serija, dakle ispod MEV-a. Pregled je
pokazao da prvi način ne vidi trbuh ili listove koje izmena izbaci u celosti.

| | bočni deltoid / rame | svi mišići, samo trenirani | svi mišići, i netrenirani | iznad MRV-a |
|---|---|---|---|---|
| zbirna grupa, stari šabloni | 58 (rame) | 803 | 1451 | 0 |
| glave, stari šabloni | 486 | 1231 | 1879 | 0 |
| **glave, novi šabloni** | **70** | **716** | **1454** | **0** |

Ukupno je to **ravno**: 1451 → 1454. Izmena ne popravlja volumen celog tela, nego ga preraspodeljuje.
Bočni deltoid, koji je zbirna grupa samo skrivala, dobija rad, a plaćaju ga trbuh i listovi, po
pravilu koje šabloni već imaju: kad u treningu nema mesta, oni ustupaju. Prema istim šablonima
bez izmene (1879), izmena je jasno bolja.

Bočno podizanje preko svih ugrađenih nedelja: 4598 → 9246 serija. Srednji nivo, hipertrofija, ravan
blok: bočni deltoid 15 uz cilj 16 na Full Body, PPL, Upper/Lower + PPL i PPL x2, a 12 na
Upper/Lower i Legs Specialization. Pre izmene šablona ti šabloni su davali 6–9. Full Body (4 dana)
i Upper/Lower x3 su već imali dva bočna podizanja (15 i 12), pa nisu menjani.

### Cena, rečena testovima

- **Pet parova nivo × cilj bez ijedne serije trbuha ili listova**: trbuh naprednog u
  hipertrofiji na Full Body i Full Body (2 dana), listovi naprednog u hipertrofiji na Full Body
  i trbuh početnika na Push/Pull/Legs, u hipertrofiji i u snazi. Na oba Full Body šablona
  trbuh je i pre ove grane imao samo napredni u hipertrofiji, pa ga sada nema nijedan nivo; na
  Full Body isto važi i za listove. Nivo koji uzima manje vežbi više
  ne stigne do vežbe ispred koje je stalo bočno podizanje. Test
  `OnlyAbsAndCalves_AreEverLeftWithoutASet` drži tačan spisak (41 par, ranije 36) i pravilo
  da bez serija smeju ostati samo trbuh i listovi. Na Upper/Lower je početnik prvo ostajao bez
  listova; posle pregleda bočno podizanje na Lower A stoji posle njih, i ta rupa ne postoji.
- **Trbuh na Push/Pull/Legs, srednji nivo, propis 4 naspram MEV-a 6.** Srednji nivo uzima dve
  složene vežbe i četiri izolacije po treningu, pa tri dana imaju dvanaest mesta za izolacije, i
  sva su bila zauzeta. Drugo bočno podizanje je moralo nekome da uzme mesto. Uzelo ga je od
  trbuha, po istom pravilu koje testovi već imaju za listove i trbuh: kad nema mesta, oni ustupaju.
  `TemplatesOfThreeDaysOrMore_ReachMevAtTheReferenceLevel` ga ima kao imenovan izuzetak, a
  izuzetak mora i da postoji, sa tačno tim brojem.
- **Napredni u bloku snage na Full Body:** leđa 11.5 i zadnja loža 6.5, svaki u tri nedelje
  (prva nedelja linearnog modela, prve dve obrnutog). Zadnja loža: bočno podizanje na Day C stoji ispred Leg Curl-a,
  a napredni uzima četiri izolacije. Leđa: cilj zadnjeg deltoida u bloku snage je na pola puta od
  MEV-a 0 do MAV-a, pa balansiranje skida seriju Face Pull-a, a sa njom i pola serije leđa. To
  delimično vraća ono što je #105 popravio (leđa 12.5), za pola serije. Zapisano u
  `AnAdvancedStrengthBlockOnFullBody_FallsBelowMev_OnlyWhereRecorded`.
- **Zadnji deltoid na PPL-u, 4 serije uz cilj 8.** Granica po treningu na Pull danu skida rad
  za leđa, a Face Pull ga nosi upola. Uputstvo to sada kaže u tabeli PPL-a.

Spisak naprednog u hipertrofiji (`AnAdvancedHypertrophyWeek_...`) se samo smanjio: gluteus na
Upper/Lower + PPL i PPL x2 više nije ispod MEV-a, a novih stavki nema.

## Migracija `SplitShoulderHeads`

Pre pisanja izmereno nad razvojnom bazom: 25 naučenih granica za „Shoulders", 5 redova sistemskih
vežbi i nijedan red lične vežbe.

- Glave se upisuju u `MuscleGroups` u samoj migraciji, jer im redovi koji prelaze trebaju kao
  strani ključ.
- Naučene granice ramena se kopiraju na bočni deltoid nepromenjene. Naučena granica je pomak
  od seed-a, a seed je isti. To čuva test `SideDelts_KeepTheSeedTheShoulderGroupHad`.
  Prednji i zadnji deltoid polaze od svojih seed vrednosti, jer o njima zasebno ništa nije
  naučeno.
- Lična vežba sa udelom „Shoulders" prelazi na bočni deltoid: ostaje u istom budžetu, sa istim
  brojevima. Koju glavu lična vežba radi, iz reda se ne vidi. Aplikacija nema izmenu mišića
  postojeće vežbe, pa vežbač koji želi drugu glavu pravi vežbu ponovo.
- „Shoulders" se briše. Kaskada nosi njen seed, naučene granice i redove sistemskih vežbi, a
  usklađivanje kataloga pri startu (`ReconcileSystemExercisesAsync`) upisuje doprinose glava.
- `Down` vraća sve u jednu grupu: naučene granice bočnog deltoida postaju granice ramena, a lična
  vežba zadržava najveći udeo neke glave.

Provera posle primene: naučene granice bočnog deltoida su red po red iste kao stare granice ramena
(`diff` 25 redova), ukupno 230 redova i pre i posle. Seeder je pri startu upisao 0/6/10, 8/16/26 i
0/8/16 i sedam doprinosa glava, tačno po katalogu.

Analitika ne čuva zbirove, nego ih računa pri čitanju, pa se i istorija sada prikazuje po glavama.

## Šta je pregled još našao

- **Učenje granica sa MEV-om nula.** Pravilo koje spušta MRV traži da je nedelja trenirala bar
  na minimumu. Sa minimumom nula to uvek prolazi, pa su tri teške serije prednjeg deltoida
  spuštale MRV svake nedelje (10 → 5 za pet nedelja), a MAV za njim, bez ikakvog dokaza o
  snazi. Za mišić sa MEV-om nula o oporavku sada govori samo nedelja blizu cilja (90% MAV-a),
  isto kao za sam cilj. Svaki MEV veći od nule zadržava staro pravilo. Vraćanje obara 1 test.
- Komentari u šablonima su govorili da jedno podizanje nedeljno daje 6 serija. To je propis;
  posle balansiranja je 9, uz cilj 16.
- Prikaz: prednji deltoid sa 0 serija je zelen („optimalno"), jer je minimum nula. To je
  namera, ali ume da izgleda čudno kad plan nema nijedan potisak iznad glave (srednji nivo na
  Upper/Lower uzima samo dve složene vežbe). Ostavljeno tako.

## Provera

- `dotnet test`: **893** (bilo 885); `npm test`: 209; `npm run build` prolazi.
- Novi testovi: MEV nula ostaje nula pri skaliranju (3 nivoa) i pri učenju granica; laka teška
  nedelja ne spušta MRV mišiću sa MEV-om nula; podela zadržava seed ramena na bočnom deltoidu;
  samo direktan rad ulazi u prednji i zadnji deltoid; bez serija ostaju samo trbuh i listovi.
- Merenje vraćanjem, jedna izmena po merenju:

| Vraćeno | Obara |
|---|---|
| učenje granica: donja granica opet 1 | 1 |
| skaliranje: nula opet postaje jedna serija | 6 |
| šabloni sa `main`-a (podela ostaje) | 4 |
| veslanje opet upola zadnjem deltoidu | 2 |
| seed bočnog deltoida 6/16/27 | 1 |

- Uživo, na 375 px, nalog za testiranje: ekran volumena pokazuje tri glave sa 0/6/10, 8/16/26 i
  0/8/16. Stari Upper/Lower blok raspoređuje istorijske serije po glavama, a bočni deltoid tu
  stoji na 6, „ispod", što je upravo ono što je zbirna grupa krila. Prednji deltoid sa 0 serija
  je „optimalno", bez prelivanja. Čarobnjak za blok pokazuje novi Legs A na PPL x2. Novi blok
  (PPL x2, hipertrofija, ravan) u bazi daje prvoj nedelji prednji 6, bočni 15 i zadnji 8, isto
  kao simulacija.

## Šta ovo ne rešava

- Leđa ostaju zbirna (nema izvora za podelu), i ekran volumena to i dalje kaže.
- Stari blokovi zadržavaju stare šablone. Novi šabloni važe od sledećeg generisanog bloka, kao i
  svaka izmena kataloga šablona.
