# Korak tega mora da stane u opseg

**Grana:** `fix/load-step-absorption`

Prva grana iz revizije nauke o treningu (odeljak F), nalazi F1 i F2. Revizija je gledala ono
što ranije runde nisu: sve prethodne tvrdnje o progresiji testirane su na težinama šipke
(60–300 kg), a nijedan test nije proveravao težinu ispod 20 kg. Tamo je korak tega veliki u
odnosu na teret, i tu su se tri pravila pokvarila iz istog razloga.

## Problem

### F2 — korak koji opseg ne može da upije

Dupla progresija daje jedan korak kad sve serije stignu do vrha opsega, jer sledeći trening
ionako kreće od dna. Taj povratak po Epley-u „plaća" oko 13% težine u opsegu 8–12 sa RIR-om 1.
Svaki korak šipke na stvarnoj težini staje u to. Korak bučice na lakoj težini ne staje:

| | Izmereno na nepromenjenom kodu |
|---|---|
| Bočno podizanje 8 kg, 3 × 12 @RIR1 | predlog **10 kg** (+25%) |
| Po Epley-u na 10 kg uz RIR 1 | **3.4 ponavljanja** — pet ispod dna opsega |

Komentar u `ProgressionEngine` je tvrdio da je uslov za korak „tačno uslov da se sledeći
propis podigne". Dokaz je važio za **korišćenu** težinu, a predlagala se korišćena plus korak.
Za šipku je razlika zaokruživanje, za laku bučicu je cela priča.

Koliko koji opseg upija (Epley, vrh opsega sa ciljnim RIR-om, dno opsega dostižno):

| Opseg | Upija do |
|---|---|
| 8–12 @RIR1 | ~13% |
| 3–6 @RIR2 | ~15% |
| 10–20 @RIR1 | ~27% |

Dakle: bučice ispod ~15 kg, sajla ispod ~19 kg, mašina ispod ~38 kg — skoro sve izolacije u
šablonima.

### F1 — korekcija koju korak ne može da izrazi

Korekcija po RIR-u je ograničena na ±10%, pa se zaokružuje na korak. Kad je 10% težine najviše
pola koraka — **težina ≤ 5 × korak** — i najveća korekcija se zaokruži nazad na istu težinu:

| Težina / korak | Serije 5/4/4 @RIR1 u 8–12 (korekcija −10%) | Pre | Sada |
|---|---|---|---|
| 10 kg / 2 (bučica) | 9 → zaokruženo 10 | **10** | 8 |
| 12.5 kg / 2.5 (sajla) | 11.25 → 12.5 | **12.5** | 10 |
| 25 kg / 5 (mašina) | 22.5 → 25 | **25** | 20 |
| 30 kg / 5 | 27 → 25 | 25 | 25 (nepromenjeno) |

Zajedno sa F2 to je bila zamka: vežbač skoči na 10 kg, ne stigne do opsega, a težina ostane
na 10 kg nedeljama — dok ponavljanja sama ne dođu do 8.

### Deload na 100% težine

Isti koren: 0.9 × 10 kg = 9, zaokruženo na korak od 2 kg = **10**. Deload lakog tega bio je
deload samo po serijama. Izmereno za 8, 10 i 12.5 kg (koraci 2 i 2.5) i 25 kg (korak 5).

## Rešenje

### Cilj ponavljanja koji donosi korak (`StepAbsorption.RepsToEarnStep`)

Korak se daje tek kad sve serije stignu do broja ponavljanja na kome korak staje. To je vrh
opsega kad god korak staje, a inače najmanji broj `r` za koji važi
`(30 + r + RIR) · težina ≥ (težina + korak) · (30 + dno)` — posle koraka dno opsega ostaje
dostižno, najgore do otkaza.

Između vrha opsega i tog cilja težina čeka i **ne pada**: sesija na vrhu opsega nikad ne
spušta opterećenje (pravilo iz runde 9). Pozitivna korekcija sme da prođe, jer je izvedena iz
stvarne rezerve.

**Zašto „dostižno do otkaza", a ne „dostižno uz ciljni RIR".** Stroža verzija je probana prva
i oborila je **13 od 613** postojećih testova, devet više od ove. Uske nedelje (11–12 @RIR2) i
fiksan cilj iz ličnog šablona (5 × 5) upijaju samo 2–6% uz ciljni RIR, pa bi i 100 kg na šipki
prestalo da napreduje. Tamo korak plaća rezerva, a trošenje jednog ponavljanja rezerve je
obična progresija. Predlog težine na kojoj je dno opsega iza otkaza nije.

### Korak naniže kad zaokruživanje obriše korekciju na granici

Korekcija naniže koja je dostigla −10% (najjači signal koji pravilo zna), a zaokruživanje ju je
vratilo na istu težinu, sada spušta težinu za **jedan korak**. Manja korekcija naniže (npr. −6%)
i dalje ostavlja težinu, pa se napreduje ponavljanjima. Naviše se ništa ne menja: držanje
težine tu samo znači da ponavljanja rastu ka koraku.

### Deload je uvek lakši (`NextWeekLoad.DeloadLoad`)

Ako se 90% zaokruži nazad na punu težinu, deload ide korak ispod: 8 i 10 su jednako daleko
od 9, a deload bira lakšu stranu. **Jedno pravilo za dva mesta**: nedelja posle završenog
treninga i auto-deload su nosili svaki svoju kopiju 90%, a u rundi 9 su dve kopije pravila
bile razlog da jedna kolona stigne do dva od tri mapiranja.

### Ekran

`ExercisePlanDto.RepsToEarnStep` je **izračunato** svojstvo, ne polje koje se puni pri
mapiranju: DTO se pravi na tri mesta. Kartica vežbe prikazuje „Ponavljanja · cilj 17" i ispod
težine objašnjenje:

> Korak od 2 kg je 25% ove težine - više nego što opseg 8–12 može da upije. Težina ostaje dok
> sve serije ne stignu do 17 ponavljanja, pa onda ide korak.

Napomena ispod unosa za seriju na vrhu opsega više ne obećava „sledeći put ide korak više"
kad korak nije zarađen.

**U deload nedelji cilj ostaje vrh opsega.** Posle deload-a se nastavlja od težine zarađene
pre njega, pa bi produžen cilj samo terao ponavljanja naviše u nedelji odmora. Nađeno u
prolazu kroz pregledač: deload nedelja je prvo tražila 15 ponavljanja.

## Provera

- `dotnet test`: **647** (bilo 613), `npm test`: **152** (bilo 150), oba build-a prolaze.
- Merenje vraćanjem starog pravila (commit, pa vraćanje, pa rebuild):

  | Vraćeno | Pada |
  |---|---|
  | samo upijanje koraka | 7 |
  | samo korak naniže | 4 |
  | samo stroži deload | 6 |
  | sva tri | **17** (= 7 + 4 + 6) |

- Stari rezultat (`ComputeNext_MatchesTheLegacyFormula_WhereNoChangeWasIntended`) i dalje važi
  za preko 100 000 slučajeva; iz poređenja su izuzeta samo dva nova, namerna slučaja.
- End-to-end kroz API i pregledač, ravan blok hipertrofije, lični šablon sa tri vežbe:

  | Nedelja | Bočno podizanje | Cable Fly | Bench Press (kontrola) |
  |---|---|---|---|
  | 1: odrađeno | 8 kg × 12 × 3 | 12.5 kg × 5/4/4 | 60 kg × 12 × 3 |
  | 2: predlog | **8 kg, cilj 17** (bilo 10) | **10 kg** (bilo 12.5) | 62.5 kg, cilj 12 |
  | 2: odrađeno | 8 kg × 17 × 3 | 10 kg × 8 × 3 | 62.5 × 10 × 3 |
  | 3: predlog | 10 kg, cilj 15 | 10 kg, cilj 17 | 62.5 kg |
  | 4 (deload) | **8 kg** (bilo 10) | **7.5 kg** (bilo 10) | 57.5 kg |

- Na 375 px: kartica sa ciljem 15 i objašnjenjem, napomena „Vrh opsega, ali korak težine je
  ovde prevelik…" posle unosa 12 ponavljanja; bez horizontalnog prelivanja (izmereno posle
  reload-a), bez grešaka u konzoli.

## Poznata ograničenja

- **Produžen cilj ide preko Epley granice od 12.** Serija od 17 ponavljanja ne daje procenu
  maksimuma, pa ni PR ni tačku u trendu. Za izolacije to nije gubitak (e1RM bočnog podizanja se
  nigde ne koristi), ali znači da za tu vežbu i granice volumena ne dobijaju signal snage.
  Pravi odgovor je širi opseg za izolacije (10–20 upija ~27%), koji je zasebna grana u ovoj
  rundi.
- **`UndoDeload` posle strožeg deload-a vraća manje.** Kad nema odrađene trenažne nedelje za
  taj dan, težina posle deload-a se vraća iz same deload težine (/0.9 pa naniže): iz 8 kg to je
  8, a ne 10. Taj put postoji samo kao rezerva (cela nedelja preskočena), a potcenjivanje je
  bezbedniji smer i ispravi ga prva sledeća sesija.
