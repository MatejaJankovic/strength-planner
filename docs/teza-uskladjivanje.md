# Usklađivanje teze sa aplikacijom

Teza (`Diplomski - Mateja Jankovic - verzija 2.1.docx`) opisuje aplikaciju kakva je bila pre
prve runde rada. Od tada je spojeno više od osamdeset PR-ova, i dobar deo rečenica u tezi više ne
opisuje ono što aplikacija radi. Ovaj dokument skuplja ta mesta na jedno mesto: za svako je dat
navod iz teze, šta aplikacija radi danas (sa PR-om), i predlog novog teksta.

**Tezu menja vlasnik.** Ovde nije menjan `.docx`, samo je predložen tekst. Predlozi su na
ćirilici, kao teza. Gde postoji više načina da se mesto popravi, predlog je najkraći koji je
tačan; detalji su u beleškama iz `docs/features/`.

Navodi su provereni nad tekstom v2.1. Broj u zagradi (npr. ¶265) je redni broj pasusa u
izvučenom tekstu dokumenta, samo da se mesto lakše nađe.

## 1. Trajanje bloka i deload

**Gde:** uvod (¶188), 4.4 (¶235), slučaj korišćenja 4 (¶265), 5.2.1 `Mesocycle` (¶307),
poglavlje 6 uz listing 6.4 (¶399), slike 7.6 i 7.7 (¶559, ¶561), zaključak (¶573).

**Teza:** „аутоматски генерише четворонедељни мезоциклус са планираном недељом растерећења"
(¶188); „три недеље прогресивног повећања оптерећења праћене су четвртом, недељом планираног
растерећења" (¶235); „креира четири недеље - три акумулационе и завршну deload недељу" (¶265);
„Mesocycle представља тренажни блок од четири недеље" (¶307); „четврта недеља аутоматски
означена као недеља за рестерећење" (¶399); „Четврта недеља носи ознаку DELOAD" (¶561);
„аутоматски гради четворонедељне мезоциклусе" (¶573).

**Danas:**

- Postoje tri modela periodizacije: ravan (4 nedelje), linearan (6) i obrnut (6), a za
  postojeće blokove i stari linearan (6). Predlaže se linearan za svaki blok (#11, #90).
- Ravan blok početnika nema planirani deload: četiri trenažne nedelje (#93).
- Deload može da se pomeri i ranije, kad ocena umora pređe prag, najviše jednom po bloku (#5,
  #90). Početnik taj prag nema (#9).

**Predlog (5.2.1):** „Mesocycle представља тренажни блок са једним циљем (снага или
хипертрофија) и моделом периодизације: раван блок траје четири недеље, а линеарни и обрнути
шест. Последња недеља је планирано растерећење, осим у равном блоку почетника, а систем може да
уведе растерећење и раније, када процена умора пређе праг (осим за почетника, који праг нема)."

**Predlog (slučaj korišćenja 4):** „... и креира недеље блока према изабраном моделу
периодизације, са завршном deload недељом (осим у равном блоку почетника)."

**Predlog (4.4):** „У раду су примењена три модела периодизације: раван, у коме је свака
тренажна недеља иста, линеарни, у коме серије расту а понављања и резерва падају кроз блок, и
обрнути. Блок се завршава недељом планираног растерећења (осим равног блока почетника), у којој
се број радних серија преполовљава, а тежине смањују на око 90% стварно коришћених."

**Predlog (7.7 i uvod):** „четворонедељни" zameniti sa „мезоциклус према изабраном моделу
периодизације", a „Четврта недеља носи ознаку DELOAD" sa „Последња недеља носи ознаку DELOAD".

## 2. Korak opterećenja

**Gde:** 4.3 (¶232), 6.1 uz `WorkingWeightFor` (¶325), 6.2 (¶344), listing 6.3 (¶389),
zaključak (¶575).

**Teza:** „тежина се повећава за најмањи практични корак (у овом раду 2,5 kg)"; „заокружује је
на корак од 2,5 kg".

**Danas:** korak se bira po vežbi i podrazumevano dolazi od opreme: šipka, bučica, mašina,
sopstvena masa (#2). Kod lakih tegova korak mora da stane u ono što serija može, pa se uzima
kad ga sve serije apsorbuju (#85).

**Predlog (4.3):** „... тежина се повећава за корак оптерећења те вежбе (подразумевано према
опреми: 2,5 kg за шипку, 2 kg за бучицу), а циљ понављања враћа се на min."

## 3. Korekcija po RIR-u

**Gde:** 6.2 (¶344), zaključak (¶575).

**Teza:** „3% по поену, ограничено на ±10%"; „при циљном RIR 1 највише 3% по тренингу".

**Danas:**

- Korekcija čita Epley krivu: jedan RIR vredi 1 / (30 + sredina propisanog opsega + ciljni
  RIR), dakle po propisu, a ne po ponavljanjima sesije. To je oko 2.7% za 3–6, 2.4% za 8–12 i
  2.2% za izolaciju na 10–20 (#95). Granica od ±10% ostaje.
- Neuspela ponavljanja se beleže, pa korekcija naniže nije ograničena nulom RIR skale (#3, #7).
- Polazna težina progresije je najteža dignuta, a ne prosek težina sesije (#61).

**Predlog (6.2):** „... и коригује тежину онолико колико по Epley формули вреди одступање од
циљаног RIR-а (око 2,4% по понављању у опсегу 8–12), ограничено на ±10%."

**Predlog (zaključak):** rečenicu o užoj korekciji naniže izbaciti, jer je beleženje neuspelih
ponavljanja (prvi pravac razvoja iz ¶576) urađeno.

## 4. Granice volumena

**Gde:** 4.6 (¶241), 5.2.1 `VolumeLandmark` (¶306), slučaj korišćenja 7 i slika 7.10,
zaključak (¶575).

**Teza:** „минимални ефективни волумен ... испод кога стимулус није довољан за напредак";
„VolumeLandmark чува ... MEV и MRV"; „Волуменске границе MEV и MRV су статичке вредности".

**Danas:**

- Granice su tri: MEV, MAV i MRV (#8). Skalirane su nivoom iskustva (#9) i uče se iz odgovora
  na volumen (#4).
- Cilj i minimum uče iz trenda snage, a gornja granica iz RIR-a, otkaza i pada (#72).
- Snaga se sudi po proseku dve uporedive nedelje, uz prag od 2.5% (#94, #97).
- Volumen se broji kao stimulativan, po blizini otkaza (#8).
- Serije se biraju tako da nedelja padne na cilj (#37), a jedan trening nosi najviše 11 serija
  jednog mišića (#87).
- Vrednosti su procene iz priručnika, a ne merenja, i aplikacija ih tako i naziva (#95).

**Predlog (4.6):** „... минимални ефективни волумен (MEV), испод кога је напредак мали,
максимални адаптивни волумен (MAV), на коме је напредак највећи, и максимални опорављиви
волумен (MRV), изнад кога се вежбач више не опоравља адекватно. Вредности су оријентационе
процене [18], па систем полази од њих и учи их из корисниковог одговора на волумен."

## 5. Registracija i profil

**Gde:** slučaj korišćenja 1 (¶250), 5.2.1 `Profile` (¶303), opis slike 7.1 (¶549).

**Teza:** „... ниво искуства и жељени број тренинга недељно".

**Danas:** polje za broj treninga nedeljno je uklonjeno: taj broj bira šablon (#40, #42).
Registracija je čarobnjak od osam ekrana: sedam pitanja, posle kojih se pravi nalog, i unos
početnih maksimuma (#49). Profil ima
i visinu, ime za prikaz i sliku (#49, #50).

**Predlog (slučaj korišćenja 1):** „Кораци: корисник одговара на седам питања, по једно на
сваком екрану (име за приказ, адреса е-поште и лозинка, пол (опционо), телесна маса, висина,
узраст и ниво искуства); налог се креира тек после последњег одговора, а корисник се преусмерава на
унос почетних максимума, осми корак." Iz `Profile` izbaciti „жељени број тренинга недељно".

## 6. Šabloni

**Gde:** slučaj korišćenja 4 (¶265), poglavlje 6 (¶399), opis slike 7.5 (¶557).

**Teza:** „Понуђена су три шаблона - Full Body (три тренинга недељно), Upper/Lower (четири) и
Push/Pull/Legs (три)".

**Danas:** devet ugrađenih šablona (#10 i kasnije) i lični šabloni (#41). „Prati moj šablon"
se nudi samo uz lični šablon (#101).

**Predlog (7.5):** „Креирање плана почиње избором шаблона за сваки блок. Понуђено је девет
уграђених шаблона, од два до шест тренинга недељно, и шаблони које је корисник сам направио,
уз преглед вежби по данима."

## 7. Preduslovi slučaja korišćenja 4

**Gde:** slučaj korišćenja 4, izuzeci (¶267).

**Teza:** „већ постоји активан мезоциклус - систем захтева да се текући заврши или обрише; за
неку вежбу не постоји процена 1RM - систем тражи допуну уноса".

**Danas:**

- Trening se pravi samo kroz plan (makrociklus). Nov plan gasi tekući, ali ga ekran pre toga
  imenuje i traži potvrdu (#46, #78). Runda 12 je odlučila da se ovde menja teza, a ne
  aplikacija: brisanje istorije kao uslov za nov blok nije dobro pravilo.
- Vežba bez maksimuma ne blokira plan. Ekran treninga ih navodi i nudi unos ili test-seriju
  (#77, #78).

**Predlog:** „Изузеци: већ постоји активан план - систем га именује и тражи потврду пре него што
га угаси; за неку вежбу не постоји процена 1RM - систем то наводи и нуди унос максимума или
тест-серију (за вежбу са сопственом масом само тест-серију), а вежбач може и да први пут
упише тежину по осећају."

## 8. Deload i opterećenje (slučaj korišćenja 6)

**Gde:** slučaj korišćenja 6, izuzeci (¶277), listing 6.5 (¶432).

**Teza:** „у deload недељи предлози се изводе из стварно коришћених тежина треће недеље (90%
тежине, упола мање серија)".

**Danas:**

- Deload polazi od poslednje trenažne nedelje, koja god da je (#5, #90).
- Ciljni RIR deload nedelje je ciljni plus dva, jer 10% opterećenja po Epley-u vredi oko tri
  ponavljanja (#66).
- Nedelja posle deload-a nastavlja od poslednje trenažne nedelje, a ne od deload-a (#61).

**Predlog:** „Изузеци: у deload недељи предлози се изводе из стварно коришћених тежина последње
тренажне недеље (90% тежине, упола мање серија, циљни RIR већи за два), без корекција; недеља
после растерећења наставља од те тренажне недеље."

## 9. Procena maksimuma

**Gde:** 4.1, 6.1 (¶325), poglavlje 6 (¶399).

**Danas, što teza ne kaže:**

- Procena se pravi samo iz serije do 12 ponavljanja i najviše 3 RIR-a (#62).
- Jedno ponavljanje do otkaza je sam maksimum, a ne Epley procena (#98).
- Početni maksimum bloka je najbolji iz poslednjih 56 dana, uz pravilo za usamljen ekstrem i
  prednost ručnog unosa (#62).
- Kad u prozoru nema ničega, uzima se najnoviji zapis, a ekran to kaže (#99).

**Predlog (6.1):** posle opisa `EstimateOneRepMax` dodati „Процена се прави само из серије до 12
понављања и највише 3 понављања у резерви, јер се даљој екстраполацији не може веровати;
једно понављање до отказа је сам максимум."

## 10. Ocena umora i automatski deload

**Gde:** zaključak (¶575–576), poglavlja 4 i 6 nemaju opis.

**Danas:**

- Ocena umora sabira četiri signala: RIR 0.35, otkaze 0.25, pad snage 0.25 i volumen 0.15.
  Prag je 0.60, a za naprednog 0.50. Početnik prag nema, pa ni automatski deload.
- Nijedan signal sam ne pokreće deload, a pad snage traži potvrdu prethodne nedelje (#5, #70,
  #95).
- Težine i pragovi su heuristika sistema. Lažni i propušteni deload-ovi su izmereni simulacijom
  (`docs/simulations/fatigue_signal_noise.py`). Šum u njoj je modelovan prema literaturi, ali
  deo je pretpostavka modela: koliko vežbačeva procena RIR-a varira iz nedelje u nedelju nijedan
  od korišćenih radova ne meri.

**Predlog (novi pasus u 4.x):** „Аутоматски deload се покреће када недељна процена умора пређе
праг; почетник праг нема. Процена сабира четири сигнала - одступање RIR-а, удео серија до отказа, потврђен пад
процењене снаге и волумен према MRV-у - са тежинама изабраним тако да ниједан сигнал сам не
покреће растерећење. Тежине и прагови су хеуристика система, а не резултат истраживања;
провераване су симулацијом са шумом процене RIR-а и дневне варијације снаге моделованим према
литератури, при чему је део тог шума претпоставка модела."

## 11. Listinzi

**Gde:** listinzi 6.1–6.5 i tekst uz listing 6.5 (¶415): „прогресија полази од просека тежина
које је корисник стварно користио".

Listinzi prikazuju kod iz v2.1. Najveće razlike:

- 6.1: zaokruživanje na korak opreme i provera `CanEstimateFrom` (#2, #62);
- 6.2: korekcija po Epley-u, ne 3% po poenu (#95), i pravilo gornjeg kraja opsega (#60);
- 6.3: `WeightStepKg = 2.5m` više nije jedan korak za sve (#2);
- 6.4: deload po modelu, a ne „četvrta nedelja" (#11, #93);
- 6.5: polazna težina je najteža dignuta, ne prosek (#61); lakša serija se računa samo ako je
  išla do otkaza. U ¶415 „од просека тежина" zameniti sa „од најтеже стварно коришћене тежине".

**Predlog:** listinge zameniti fragmentima iz trenutnog koda, ili ih zadržati uz napomenu da
prikazuju prvu verziju algoritma, a da su kasnije izmene opisane u tekstu.

## 12. Poglavlje 8 (zaključak)

**Gde:** ¶575–576.

Četiri od pet ograničenja iz ¶575 su rešena u rundi 1, a od ostalih pravaca razvoja iz ¶576
urađeni su makrociklusi (#6, #46):

| Ograničenje iz teze | Rešeno u |
|---|---|
| uža korekcija naniže (RIR staje na nuli) | beleženje neuspelih ponavljanja (#3, #7) |
| statične MEV i MRV | adaptivne granice (#4), MAV (#8), nivo iskustva (#9) |
| korak od 2,5 kg za sve vežbe | korak po vežbi (#2) |
| deload vezan za četvrtu nedelju | auto-deload (#5) |
| RIR „kod početnika nepouzdan" | vidi ispod |
| makrociklusi (pravac razvoja) | #6, #46 |

Rečenica „Ауто-регулација почива на субјективној RIR процени, која је код почетника непоуздана"
ne slaže se sa Halperin i sar. 2022: ta meta-analiza ne nalazi da iskustvo utiče na tačnost
procene RIR-a. Prosečna greška je potcenjivanje od oko jednog ponavljanja, uz veliku razliku
među vežbačima (#93).

**Predlog:** poglavlje 8 prepisati oko onoga što i dalje važi:

- RIR je samoprocena, i jedna nedelja je pretežno šum. Zato granice volumena sude po dve
  nedelje, a ocena umora traži potvrdu (#94, #95, #97).
- Vrednosti MEV/MAV/MRV i težine ocene umora su procene i heuristika.
- Zbirna grupa „leđa" ne razlikuje latove, gornja leđa i opružače (#81, #100). Rame je
  podeljeno na prednji, bočni i zadnji deltoid, sa RP procenama granica (shoulder-heads.md).

Van obima su ostali: višejezičnost, analitika cele istorije, talasasta periodizacija, PWA,
slanje e-pošte i tamna tema.

## 13. Ostala mesta

**Preskakanje treninga.** ¶309: „са ознаком дана и статусом (планиран, у току, завршен)". Od #75
trening može biti i preskočen: nedelja se tada zatvara, ali ne uči granice volumena. Predlog:
„... и статусом (планиран, у току, завршен, прескочен)", a u slučaj korišćenja 5 dodati korak
„корисник може да прескочи тренинг који неће одрадити".

**Model podataka.** ¶300 i slika 5.2 nemaju `Macrocycle`, `MacrocycleBlock` ni
`UserWorkoutTemplate`, a ¶307 kaže da `IsActive` „обезбеђује да корисник у сваком тренутку има
највише један активан мезоциклус". Danas je aktivan plan (makrociklus), a blokovi se generišu
kad dođu na red (#6, #46), uz lične šablone (#41). Predlog: dijagram dopuniti tim entitetima, a
`IsActive` opisati na planu.

**Završetak treninga.** ¶317 i ¶565 (slika 5.4) nabrajaju procenu e1RM, progresiju i upis
predloga. Danas završetak radi i ovo: računa ocenu umora i može da povuče deload (#5),
balansira serije ostatka nedelje ka cilju volumena (#37) i, kad je nedelja gotova, uči granice
volumena (#4). Sve i dalje u jednoj transakciji. Predlog: tačke (4)–(6) dodati u ¶317.

**Opsezi ponavljanja.** ¶559: „за хипертрофију: 8-12 понављања по серији са једним понављањем у
резерви". Danas izolacije nose 10–20 u oba cilja (#91), a raspored pomera opseg iz nedelje u
nedelju (#65, #67). Predlog: „за хипертрофију: 8-12 понављања на вишезглобним вежбама и 10-20
на изолацијама, уз један до два понављања у резерви, а распоред помера опсег кроз блок".

**Stranice klijenta.** ¶498 navodi stranicu „мезоциклус" i `MesocycleService`. Stranica
`/mesocycle` je uklonjena u #46, a trening se pravi samo kroz plan. Predlog: „мезоциклус"
zameniti sa „план".

## 14. Literatura

Teza ne navodi priručnik (*Džepni priručnik o programiranju treninga*), iako je on izvor
pravila za granice volumena, nivo iskustva i deload (vidi
[analiza-prirucnika.md](analiza-prirucnika.md)). Izvori na koje se aplikacija danas oslanja, sa
mestom gde je svaki upotrebljen:

| Izvor | Za šta | Beleška |
|---|---|---|
| Džepni priručnik o programiranju treninga | MEV/MAV/MRV, nivo iskustva, deload, podrazumevani izvor (odluka D1) | analiza-prirucnika.md |
| Halperin i sar. 2022 | tačnost procene RIR-a, granica od 12 ponavljanja | beginner-deload.md, estimate-edges.md |
| Grgic i sar. 2020 | dnevna varijacija 1RM (medijana CV 4.2%) | landmark-signal-noise.md |
| Remmert i sar. 2025 (**preprint**, SportRxiv, nije recenziran) | granica od oko 11 serija jednog mišića po treningu | session-volume-ceiling.md |
| Pelland i sar., *Sports Medicine* (online 2025, u štampi 2026, 56(2):481–505; DOI 10.1007/s40279-025-02344-w) | doza volumena i učestalost; pomoćni mišić broji pola serije | evidence-notes.md, session-volume-ceiling.md, strength-block-composition.md, analiza-prirucnika.md |
| Prestes i sar. 2009, González-Ravé i sar. 2022 | linearan model kao predlog | periodization-shapes.md |
| Pancar i sar. 2026, Coleman i sar. 2024 | deload početnika i trenirani vežbači | beginner-deload.md |
| Bosquet i sar. 2013 | pad snage posle pauze nije jedan broj | stale-maximum-notice.md |

Ime fajla beleške je u `docs/features/`. Pre unosa u tezu svaki izvor treba proveriti u
originalu (godina, časopis, strana), jer su beleške pisane za pregled koda, a ne kao
bibliografija.
