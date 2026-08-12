# Stimulativni volumen i MAV

**Grana:** `feature/stimulative-volume`

Prva od izmena izvedenih iz *Džepnog priručnika o programiranju treninga*
(vidi [analizu](../analiza-prirucnika.md), stavke 1 i 2).

## Problem 1: nije se svaka serija računala isto

Priručnik na dva mesta kaže da broj serija sam po sebi nije volumen:

> *"Svaka serija treba da bude urađena sa minimalno RIR 4, jer veći RIR neće omogućiti
> dovoljno stimulativnih ponavljanja."*

> *"Jedini volumen koji se računa je volumen koji sadrži mehaničku tenziju, odnosno serije
> koje su odrađene do ili blizu otkaza."*

Razlog je mehanička tenzija — stvarni pokretač hipertrofije — koja se javlja tek na
poslednjim ponavljanjima pred otkaz. Serija zaustavljena pet ponavljanja ranije donosi
zamor, ali ne i stimulus koji broj serija treba da predstavlja.

**Šta je aplikacija radila:** `VolumeService` je sabirao doprinose svih serija jednako.
Serija sa RIR 5 ulazila je u nedeljni volumen isto kao serija sa RIR 1. Korisnik koji
odradi dvadeset laganih serija dobijao je poruku *"iznad MRV"* i savet da smanji volumen —
iako po priručniku nije uradio **nijednu** stimulativnu seriju.

**Rešenje:** doprinos serije se množi njenom blizinom otkaza (`StimulativeVolume`):

| Blizina otkaza | Udeo |
|---|---|
| RIR 0–3 | 1.0 — stimulativna serija |
| RIR 4 | 0.5 — granica koju priručnik postavlja |
| RIR 5+ | 0.0 — zamor, ali ne i volumen |
| do otkaza | 1.0, koji god RIR stajao uz nju |

Nezavisna literatura se slaže sa ovim pragom: serije više od 4–5 ponavljanja od otkaza
znatno slabije stimulišu rast, dok je pojas RIR 0–3 „sweet spot".

**Zašto je baš ovo bila najvrednija izmena:** ista skala popravlja **tri** mehanizma koji
su već postojali, a svi su verovali broju serija umesto stimulusu:

1. nedeljni volumen u analitici,
2. učenje ličnih MEV/MAV/MRV granica,
3. signal „volumen naspram MRV" u oceni umora za auto-deload.

## Problem 2: nedostajala je srednja granica

Priručnik navodi **tri** granice, ne dve:

> *"Maksimalni adaptivni volumen (MAV) — Optimalan volumen treninga pri kojem se telo
> adekvatno oporavlja i stimuliše hipertrofiju, bez ulaska u prekomeran zamor... obično
> se kreće u rangu 8-20 serija nedeljno po mišićnoj partiji."*

**Šta je aplikacija radila:** znala je samo MEV i MRV, pa je „optimalno" bilo sve između —
za grudi raspon od 10 do 22 serije. To korisniku kaže gde **nije**, ali nikad gde da cilja.

**Rešenje:** `VolumeLandmarkValues` nosi i MAV. Seed vrednosti su unutar raspona iz
priručnika (Chest 16, Back 18, Shoulders 16, Quads 14, Hamstrings 11, Glutes 10,
Biceps 14, Triceps 12, Calves 13, Abs 12), a adaptacija ga uči isto kao i ostale dve:

- nedelja odrađena na MAV-u ili iznad njega, uz preostalu rezervu → MAV +1
- ista takva nedelja sa znacima umora → MAV −1
- nedelja odrađena znatno ispod MAV-a ne govori o njemu ništa, pa se ne dira

MAV je jedina granica oko koje se zaista trenira, pa je i jedina o kojoj svaka takva
nedelja nešto kaže — za razliku od MEV-a i MRV-a, koji se dodiruju retko.

## Šta je urađeno

- `StimulativeVolume` — novo, čisto domensko pravilo blizine otkaza.
- `VolumeLandmarkValues` proširen sa `Mav`; `VolumeAdaptation` ga pomera po istom pravilu
  od najviše jedne serije nedeljno i drži ga **strogo** unutar pojasa (van pojasa ne bi
  bio cilj nego još jedna granica).
- `VolumeService` i `VolumeLandmarkService` množe doprinos serije stimulativnim udelom.
  Zbrajanje u `VolumeService` je premešteno u memoriju: pravilo živi kao domenska funkcija
  koju EF ne ume da prevede u SQL, a bolje je jedan izvor istine nego isto pravilo
  napisano drugi put kao `CASE`.
- Prosek odstupanja RIR-a sada koristi **isti** ponder kao i imenilac. Bez toga to ne bi
  bio ponderisani prosek nego mešavina dve različite mere.
- `VolumeResponse` nosi **dve** mere: `PerformedSets` (stimulativna) i `RawSets` (sve
  odrađene serije). Podela nije kozmetička — vidi ispravke posle revizije.
- Migracija `AddMaxAdaptiveVolume` dodaje kolonu i **popunjava je pre** nego što novo
  `CHECK` ograničenje (`Mev < Mav < Mrv`) počne da važi — inače bi pala na svakoj bazi
  koja već ima naučene granice, jer nova kolona ulazi sa nulom.
- Ekran „Volumen": treći marker na traci, tekst *„MEV 10 · cilj 16 · MRV 22"* i
  objašnjenje da se broje samo stimulativne serije.

## Provera

- `dotnet build`, `dotnet test` (119 testova, bilo 100), `npm run build` — sve prolazi.
- `StimulativeVolumeTests` pokriva svaki prag, otkaz kao uvek pun doprinos, i dva slučaja
  koja objašnjavaju zašto pravilo postoji: dvadeset serija sa RIR 5 vredi **0**, a obična
  naporna nedelja (RIR 0–2) vredi **tačno onoliko koliko i pre**, pa pravilo nije tiho
  oborilo volumen svim korisnicima.
- `VolumeAdaptationTests` dopunjen za MAV: podizanje, spuštanje, ignorisanje nedelje
  daleko ispod cilja, ostanak strogo unutar pojasa i zaustavljanje na +50% od seed-a.
  Iscrpni test kroz pet seed vrednosti i četrdeset nedelja traži da pojas nikad ne padne.
- Migracija nad zatečenom bazom: svih **100** ličnih redova prošlo sa `Mev < Mav < Mrv`,
  seed vrednosti tačne (Chest 10/16/22, Back 10/18/25, …).
- End-to-end, ista nedelja odrađena četiri puta uz promenu samo blizine otkaza:

  | RIR | Chest | Back | Quads |
  |---|---|---|---|
  | 1 | 6.0 | 12.0 | 6.0 |
  | 3 | 6.0 | 12.0 | 6.0 |
  | 4 | 3.0 | 6.0 | 3.0 |
  | 5 | 0.0 | 0.0 | 0.0 |

- U pretraživaču: mezociklus u kome je Push odrađen sa RIR 1 a Pull i Legs sa RIR 5 —
  Chest 6, Shoulders 9, Triceps 10.5 serija, a Back, Biceps, Quads i ostali **0**.
  Traka nosi marker cilja, a legenda objašnjava pravilo.


## Ispravke posle revizije koda

### Auto-deload je dobio regresiju, a ne ispravku

Prvo sam napisao da je auto-deload „ispravku dobio besplatno" jer čita `PerformedSets`.
Bilo je obrnuto. Signal `volumen naspram MRV` je mera **zamora**, a `PerformedSets` je
posle ove izmene postao mera **stimulusa** — a sopstvena dokumentacija klase kaže da
serija daleko od otkaza *„donosi zamor, ali ne i stimulus"*. Stvarni zamor je time nestao
iz ocene umora.

Revizija je izračunala konkretan slučaj: grudi sa MRV 22 i nedeljom od 20 serija (12 na
RIR 4, 8 do otkaza). Sirovo 20/22 = 0.909; stimulativno 14/22 = 0.636, ispod praga od
0.80, pa signal doprinosi nulom. Ocena pada sa **0.6418 na 0.5600** — deload koji je
trebalo da se aktivira više se ne aktivira.

Rešenje je razdvajanje mera: `VolumeResponse` sada nosi i `RawSets`, i pravilo je
jednostavno — *pitanja o stimulusu koriste stimulativnu meru, pitanja o zamoru sirovu*.
MRV je granica **oporavka**, a oporavak troši svaka odrađena serija.

### Udeo otkaza je promenio značenje i survavao MEV

Otkazana serija uvek nosi pun doprinos, pa preživljava u brojiocu, dok serije sa nultim
doprinosom nestaju iz imenioca. `FailureShare` je time prestao da znači „koliki deo
nedelje je otkazao" i postao „koliki deo *stimulativnog rada* je otkazao" — a pragovi
(0.25 / 0.125) su kalibrisani za ono prvo.

Nedelja od 12 serija (10 na RIR 5, 2 do otkaza) je pre izmene prijavljivala udeo 0.167,
a posle **1.0**. Time se pali `showedFatigue`, a volumen je ispod MEV-a, pa MEV pada:
**10 → 9 → 8 → 7 → 6 → 5** za pet takvih nedelja, i tu ostaje. Model je zamor deset
laganih serija pripisao dvema stimulativnim i zaključio da se korisnik umara na dve serije.

Sada `FailureShare` deli sirovim zbirom i pragovi zadržavaju kalibraciju.

### Cilj se lepio za plafon

Prag za pomeranje MAV-a je slabiji od praga za MRV (jer je MAV niži), pa je MAV rastao i
na nedeljama na kojima MRV ne raste. Revizija je pustila deset seed vrednosti kroz 20
dobrih nedelja: **7 od 10 završava na tačno `MAV = MRV − 1`**, a udeo u pojasu ide sa
~0.50 na ~0.92. Ekran bi kao cilj nudio volumen jednu seriju ispod onog koji je sistem
proglasio nepodnošljivim — suprotno definiciji MAV-a.

Prva verzija ispravke (fiksiranje MAV-a na mesto koje seed daje u pojasu) je bila
pretesna: MAV više uopšte nije mogao da se pomeri, pa ne bi bio *naučen* nego samo izveden
iz MEV-a i MRV-a. Konačno rešenje dozvoljava lutanje od 15 procentnih poena iznad seed
pozicije — dovoljno da se uči, premalo da se slepi za plafon. Dva testa čuvaju obe strane.

### Sitnije

- `DbSeeder` je upisivao vrednost iz priručnika ne gledajući pojas samog reda; sada je
  poravnava. Uz to, i `VolumeLandmarks` je dobila isto `CHECK` ograničenje kao lična
  tabela — bez njega bi marker cilja mogao da završi iza plafona na ekranu.
- Ograničenje je preimenovano u `CK_..._LandmarkOrder`; staro ime
  (`MevBelowMrv`) više nije opisivalo ono što ograničenje proverava.
- Legenda je tvrdila da zelena znači „na cilju"; sada kaže da boja pokazuje pojas, a cilj
  je marker unutar njega. `GetStatus` namerno i dalje deli po MEV/MRV granicama.
- Marker cilja je bio u primarnoj boji, koja na zelenoj traci ima kontrast ispod 3:1 —
  najslabije vidljiv baš tamo gde je najvažniji. Sada je akcentne boje. Svi markeri su
  centrirani na svoju vrednost (`translateX(-50%)`) umesto da počinju na njoj.
- `CreditFor(WorkingSet)` je bio pozvan samo iz testova; sada se koristi u produkciji,
  jednom po seriji umesto tri puta.

### Poznato ograničenje

`MesocycleGenerator` ne čita granice volumena — broj serija je i dalje fiksiran
(`DefaultTargetSets = 3`). MAV je zato za sada **prikazna** vrednost: plan se ne cilja na
njega. To zatvara sledeća grana, u kojoj nivo iskustva određuje početni volumen.
