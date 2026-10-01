# Šta je urađeno, po granama

Beleške za pregled, ne tekst za rad. Svaka strana ima isti raspored: **problem** (šta je
aplikacija radila i zašto to nije bilo dobro), **rešenje**, **provera** i **poznata
ograničenja** — uključujući ono što je revizija koda našla i kako je ispravljeno.

## Prvi krug — „future improvements" iz zaključka rada

| Grana | O čemu je | PR |
|---|---|---|
| [Korak opterećenja po vežbi](per-exercise-weight-step.md) | Šipka i bučice ne idu istim koracima; korak se izvodi iz sprave, uz mogućnost ručne izmene | #2 |
| [Serije do otkaza](failed-reps-logging.md) | Zastavica otkaza i broj urađenih ponavljanja, uz simetričnu korekciju opterećenja | #3 → #7 |
| [Adaptivne MEV/MRV granice](adaptive-volume-landmarks.md) | Granice volumena se uče iz odgovora korisnika umesto da stoje na populacionom proseku | #4 |
| [Automatski deload](auto-deload.md) | Rasterećenje se pokreće iz izmerenog zamora, a ne iz kalendara | #5 |
| [Makrociklusi](macrocycles.md) | Lanac blokova; sledeći se generiše kad prethodni završi, od 1RM vrednosti koje tada važe | #6 |

## Drugi krug — izvedeno iz priručnika

Analiza sa ishodom po stavci: [`../analiza-prirucnika.md`](../analiza-prirucnika.md).

| Grana | O čemu je | PR |
|---|---|---|
| [Stimulativni volumen i MAV](stimulative-volume.md) | Serija daleko od otkaza se ne broji isto; dodat MAV kao ciljna vrednost | #8 |
| [Nivo iskustva određuje program](experience-level.md) | Nivo iz profila konačno utiče na plan — četiri poluge umesto nijedne | #9 |
| [Više šablona](more-templates.md) | Sedam šablona za 2–6 dana nedeljno, plus izolacione vežbe kojih uopšte nije bilo | #10 |
| [Periodizacija po nedeljama](periodization-models.md) | Nedelje unutar bloka više nisu iste: ravan, linearan i obrnut raspored | #11 |

## Peti krug — predlog serija koji cilja volumen

| Grana | O čemu je | PR |
|---|---|---|
| [Predlog serija po nedeljnom volumenu](weekly-volume-set-targets.md) | Broj serija se bira tako da nedelja padne u ciljnu zonu svakog mišića, i prilagođava se kada trening ne ispuni predlog | #37 |

## Šesti krug — ispravke po spisku korisnika

| Grana | O čemu je | PR |
|---|---|---|
| [Raspored na telefonu i tekst](mobile-layout-and-copy.md) | Navigacija u dva reda, izbor nedelje prelomljen, naslov plana preko pola ekrana, meniji bloka nečitljivi; plus izmene teksta i uklanjanje dugih crta | #39 |
| [Polja profila](profile-fields.md) | Pol je bio slobodan tekst pa se izabrana vrednost nije prikazivala; sada je enum. Uklonjeno "Treninga nedeljno", koje je služilo samo oznaci "predlog za tebe" | #40 → #42 |
| [Lični šabloni treninga](custom-workout-templates.md) | Sam biraš dane, vežbe, serije i opseg ponavljanja; auto-regulacija nastavlja da radi nad tvojim brojevima | #41 |

## Sedmi krug — kretanje kroz aplikaciju

| Grana | O čemu je | PR |
|---|---|---|
| [Red sa serijama i ponavljanjima](template-editor-layout.md) | Oznaka "Ponavljanja od" se prelamala, pa su tri polja stajala na dve visine | #44 |
| [Šabloni u profilu](templates-in-profile.md) | "Moji šabloni" se otvaraju iz profila, a ne iz čarobnjaka za mezociklus | #45 |
| [Trening samo kroz plan](macrocycle-first.md) | Jedan ulaz umesto dva; brisanje na nivou plana, čime prestaje da se vraća obrisano | #46 |
| [Pregled bloka](macrocycle-block-preview.md) | Klik na blok pokazuje šta nosi - propis ako je generisan, šablon ako čeka red | #47 |

## Deveti krug — logika progresije opterećenja

Pregled logike treninga (odeljak A: sve što direktno menja predloženu težinu).

| Grana | O čemu je | PR |
|---|---|---|
| [Vrh opsega više ne spušta težinu](load-progression-top-of-range.md) | Korekcija po RIR-u je poništavala korak na vrhu opsega, a iznad 125 kg obarala težinu; serija ispod opsega sa rezervom je dizala težinu | #60 |
| [Radna težina i rezime](progression-reference-and-summary.md) | Progresija je polazila od proseka težina, vežba bez serija je nosila punu težinu u deload, a strelica u rezimeu nije govorila o prikazanom broju | #61 |
| [Pouzdanost procene maksimuma](e1rm-reliability.md) | Serija daleko od otkaza je davala e1RM, a jedna naduvana procena je osam nedelja bila polazna težina bloka | #62 |
| [Sopstvena masa je opterećenje](bodyweight-load.md) | Zgib se vodio kao 0 kg, pa je e1RM bio nula, tonaža nula, a progresija je nudila korak više (1 kg) na vežbi koja se ne opterećuje | #63 |

## Deseti krug — periodizacija, deload, volumen

Isti pregled logike treninga, odeljak B: sve što odlučuje o propisu nedelje — raspored kroz
blok, rasterecenje i cilj volumena.

| Grana | O čemu je | PR |
|---|---|---|
| [Prozor ponavljanja i fiksan broj](rep-window-and-fixed-reps.md) | Faza volumena hipertrofije je ispadala kao opseg od dva ponavljanja (11–12), a lični šablon sa 5–5 je u planu dobijao 5–6 | #65 |
| [Deload rasterećuje i napor](deload-intensity.md) | Deload je spuštao opterećenje na 90% ali držao ciljni RIR, pa je po naporu bio normalna radna serija | #66 |
| [Izolacija ne ide na opseg snage](isolation-rep-range.md) | Blok snage je propisivao 3–6 (u petoj nedelji 3–4) i za bočno podizanje, letenje i pregibe — pet od šest vežbi naprednog vežbača | #67 |
| [Cilj volumena prati nedelju](weekly-volume-target.md) | Balansiranje je svaku nedelju gadjalo u MAV, pa je talas serija iz periodizacije nestajao (24, 24, 16, 16, 12 → 16 svake nedelje); blok snage je gadjao hipertrofijski MAV | #68 |

## Jedanaesti krug — učenje granica i ocena umora

Isti pregled logike treninga, odeljak C: kako se signali čitaju — šta nosi umor, a šta
granice volumena.

| Grana | O čemu je | PR |
|---|---|---|
| [Nezavisni signali umora](independent-fatigue-signals.md) | Nedelja u kojoj je sve išlo do otkaza je istom činjenicom punila dva od četiri signala i sama pokretala deload; granice volumena su imale drugu definiciju iste mere | #70 |
| [Pad snage poredivim sa poredivim](comparable-strength-change.md) | Nedelja odrađena na vrhu opsega pa nedelja na dnu — obe po propisu — čitala se kao pad snage od 7.2% u ravnom bloku | #71 |
| [Granice uče iz snage](volume-limits-learn-from-strength.md) | MEV i MAV su se pomerali po osećaju serija (RIR), što progresija već ispravlja opterećenjem — jedan uzrok je dizao i cilj i težinu; pravilo za MEV je bilo obrnuto | #72 |

## Dvanaesti krug — kod, uputstvo i rad da govore isto

Isti pregled logike treninga, odeljak D: mesta na kojima aplikacija radi jedno, uputstvo
tvrdi drugo, a rad obećava treće.

| Grana | O čemu je | PR |
|---|---|---|
| [Nivo pripada bloku](level-locked-to-block.md) | Promena nivoa iskustva usred bloka je pomerala nedeljni cilj za grudi sa 16 na 19 serija i prag deload-a sa 0.60 na 0.50 — u planu koji je već propisan | #74 |
| [Preskočen trening](skip-workout.md) | Jedan trening koji vežbač nikada neće odraditi držao je nedelju otvorenom zauvek: bez ocene umora, bez učenja granica, i bez sledećeg bloka plana | #75 |
| [Ciljevi na kartici vežbe](workout-card-targets.md) | „Nema 1RM za ovu vežbu" je pisalo i kad maksimum postoji — 3 068 planova u razvojnoj bazi; ciljni broj ponavljanja se računao i nigde nije prikazivan | #76 |
| [Test-serija i brisanje maksimuma](one-rep-max-entry.md) | Rad nudi unos test-serije iz koje sistem procenjuje maksimum — ekran ga nije imao; zapis se nije mogao obrisati | #77 |
| [Uslovi pri pravljenju plana](plan-creation-preconditions.md) | Nov plan je bez reči gasio tekući, a vežba bez maksimuma je tiho čekala da je vežbač unese po osećaju — rad traži da se oba kažu | #78 |
| [Uputstvo koje je ostalo iza koda](guide-accuracy.md) | „Sedam ugrađenih" šablona kojih ima devet, tonaža koja „pokazuje da blok raste" a u periodizovanom bloku pada po planu, i dva značenja reči „podrazumevan" | #79 |

## Trinaesti krug — model vežbi

Isti pregled logike treninga, odeljak E: koji mišić koja vežba zaista radi, gde vežba stoji
u šablonu, i šta ekran prećutkuje o unosu.

| Grana | O čemu je | PR |
|---|---|---|
| [Doprinosi mišićima](muscle-contributions.md) | Potisak je punio budžet ramena a čučanj budžet zadnje lože, pa je balansiranje skidalo serije sa vežbi koje te mišiće jedine grade | #81 |
| [Raspored vežbi u šablonu](template-placement.md) | Jedina izolacija za leđa u jednom šablonu stajala je na danu za noge; u drugom je vežba za gornje telo tamo nužna, i merenje to pokazuje | #82 |
| [Napomene uz polje za težinu](load-input-notes.md) | Nigde nije pisalo da se kod bučica unosi jedna, iako je korak od 2 kg to i značio; a 0 kg na vežbi koja se opterećuje tiho je isključivalo vežbu iz svih računica | #83 |

## Četrnaesti krug — nauka o treningu

Revizija periodizacije, vežbi i napretka naspram priručnika, teze i novije literature
(odeljak F). Prve tvrdnje o progresiji su testirane samo na težinama šipke; ovaj krug gleda i
ono što je ispod 20 kg, ono što blok radi kroz nedelje i ono što se tvrdi o volumenu.

| Grana | O čemu je | PR |
|---|---|---|
| [Korak mora da stane u opseg](load-step-absorption.md) | Bučica 8 → 10 kg (+25%) posle 3 × 12 ostavljala je 3–4 ponavljanja u opsegu 8–12; korekcija i deload lakih tegova su se zaokruživali nazad na istu težinu | #85 |
| [Slične vežbe ne idu u uzastopne dane](session-spacing.md) | Legs Specialization je stavljao noge tri dana zaredom, a Full Body (4 dana) čučanj pa RDL i leg press pa front squat u uzastopne dane | #86 |
| [Granica serija po treningu](session-volume-ceiling.md) | Balansiranje je gledalo samo nedelju, pa je Push dan Push/Pull/Legs nosio 16–18 serija za grudi; sada jedan mišić dobija najviše ~11 u jednom treningu (Remmert i sar. 2025) | #87 |
| [Tekst na ekranu](ui-copy.md) | Upozorenje na 0 kg je pominjalo praznu šipku i uz sajlu i bučice (ispravka iz runde 13 nije stigla u kod); sedam tekstova (čarobnjak plana, registracija) bilo je bez dijakritika | #88 |
| [Sastav bloka snage](strength-block-composition.md) | Napredni je i u bloku snage dobijao jednu složenu vežbu po treningu, iskoraci su nosili 3–6, a balansiranje je seklo bench pre razvlačenja (373 puta) | #89 |
| [Linearan model po priručniku](periodization-shapes.md) | Linearan model je spuštao serije kroz blok, pa je hipertrofija gađala MRV u dve najsvežije nedelje; predlog „obrnut za hipertrofiju" nije imao oslonac. Sada se za svaki blok predlaže linearan, a umor povlači deload najviše jednom po bloku | #90 |
| [Opseg po ulozi vežbe](rep-ranges-by-role.md) | Izolacije su nosile 8–12 u oba bloka, pa laka bučica nije mogla da primi korak; sada 10–20, uz poređenje snage na istoj težini iznad 12 ponavljanja | #91 |
| [Volumen naprednog nivoa](experience-volume-consistency.md) | Napredni je imao najviše granice volumena (×1.2) i najmanje serija (3), pa je propis bio ispod MEV-a u 153 od 240 nedelja-mišića i balansiranje ga je krpilo; sada kreće sa 4 | #92 |
| [Deload početnika](beginner-deload.md) | Ravan blok početnika je četvrtinu vremena provodio u rasterećenju, iako priručnik kaže da početnici o deload-u ne treba da razmišljaju; sada je i četvrta nedelja trenažna | #93 |
| [Šum u signalu snage](landmark-signal-noise.md) | Jedno nedeljno čitanje snage je pretežno šum, pa su MAV i MRV vežbača koji napreduje klizili do poda; sada se dve uzastopne nedelje moraju složiti | #94 |

## Ako čitaš samo jedno

[Periodizacija po nedeljama](periodization-models.md) — to je bio najveći raskorak između
onoga što rad tvrdi i onoga što je kod radio.

## Ako te zanima šta je pošlo naopako

Svaka strana ima odeljak o ograničenjima i ispravkama posle revizije. Najzanimljivije:

- [Stimulativni volumen](stimulative-volume.md) — jedna skala nije bila dovoljna;
  razdvajanje pitanja o stimulusu od pitanja o zamoru došlo je tek pošto je prva verzija
  pokvarila automatski deload.
- [Više šablona](more-templates.md) — korisnička vežba istog naziva mogla je da spreči upis
  sistemske i time zaključa generisanje plana **svim ostalim** korisnicima.
- [Nivo iskustva](experience-level.md) — pravilo je radilo ispravno, ali nije imalo čime da
  popuni trening; ograničenje je zatvorila tek sledeća grana.

## Bezbednost

Pregled cele aplikacije, ispravke i ono što ostaje otvoreno:

- [`../security.md`](../security.md) — šta je zatvoreno i kako je provereno
- [`../deployment-security.md`](../deployment-security.md) — koraci pri isporuci (TLS,
  sertifikati, nadogradnja postojeće baze)

| Grana | O čemu je | PR |
|---|---|---|
| `fix/session-data-leak` | Keširani maksimumi prethodnog korisnika preživljavali su odjavu | #12 |
| `fix/account-security` | Lozinke, ograničenje zahteva, nabrajanje naloga, promena lozinke | #13 |
| `fix/deployment-hardening` | Zaglavlja, kontejneri bez root-a, nalog baze, zavisnosti | #14 |
