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
- Auto-deload nije menjan — njegov signal `volumen/MRV` čita `PerformedSets`, pa je
  ispravku dobio besplatno.
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
