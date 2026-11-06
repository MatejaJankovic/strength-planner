# Uputstvo i komentari, drugi prolaz

**Grana:** `docs/guide-accuracy-2`

Prvi korak plana posle revizije (6. 11. 2026). Celo uputstvo je pročitano red po red, a svaka
brojčana tvrdnja je proverena nad kodom. Netačno je bilo samo ono ispod; ponašanje aplikacije
se ne menja.

## Uputstvo

| # | Bilo | Sada |
|---|---|---|
| G1 | red vežbe sa sopstvenom masom na ekranu maksimuma „piše *Ne unosi se*" | ekran piše „Samo iz serije", i maksimum nastaje i iz test-serije, ne samo iz prvih serija |
| G2 | napomena dvodnevnog šablona navedena u verziji od pre #102 („ispod minimalnog volumena za rast") | navedena kako je danas („ispod minimalnog efektivnog volumena, pa je rast mali") |
| G3 | „Kartica *Poznati maksimumi* na profilu vodi na isti ekran" | takve kartice nema: ekran se otvara adresom `/onboarding` ili dugmetom iz napomene na ekranu „Trening" (#78, #99). Stara rečenica je protivrečila i odeljku iz Koraka 2, koji kaže da ekran nema stavku u navigaciji |
| G4 | kod „Prati moj šablon" „ništa se ne pomera" | balansiranje se ne pokreće, pa ni granica po treningu; serije menja samo raspored (#101) |

## Komentari u kodu

- **C1** `VolumeLandmarkValues`: „below MEV nothing happens" → „below MEV growth is small".
  Parametar u istom fajlu je već govorio „unlikely to drive growth", a uputstvo od #95 i #102
  kaže da je MEV procena.
- **C2** `FatigueEvaluator`: planirani deload više nije „week four of four" nego poslednja
  nedelja bloka. Modeli traju 4 ili 6 nedelja (#11, #90).

## Izvori

- **S1:** granica od 11 serija po treningu (#87) se oslanja na Remmert i sar. 2025, a to je
  **preprint** (SportRxiv). Beleška [session-volume-ceiling.md](session-volume-ceiling.md) i
  komentar u `TrainingConstants` to već kažu. Sada to kažu i README tabela i dokument za tezu.
- **Pelland i sar.** je proveren u primarnom izvoru: *Sports Medicine*, online 2025, u štampi
  2026, 56(2):481–505, DOI 10.1007/s40279-025-02344-w. „Pelland i sar. 2025" u uputstvu je
  tačno (godina online izdanja). Dokument za tezu sada ima i DOI i obe godine.

## Posle pregleda

Pregled je potvrdio sve četiri rečenice i oba komentara nad kodom. Ispravljeno je i:

- komentar u `one-rep-max-setup.html` je i dalje govorio da ekran „sa profila stiže
  samostalno";
- Korak 2 uputstva je za ponovno otvaranje ekrana pominjao samo adresu, ne i dugme iz napomene;
- „Prati moj šablon" menja serije po rasporedu **i deload-u**: auto-deload polovi i takav blok;
- `FatigueEvaluator` je i u sledećoj rečenici govorio o „četvrtoj nedelji";
- `analiza-prirucnika.md` je bio jedini dokument u kome Remmert i sar. nije označen kao
  preprint.

## Provera

- `dotnet test`: 860, bez izmena. `npm test` i `npm run build` prolaze. Menja se samo tekst.
- `GuideTemplateTableTests` (čita uputstvo) i dalje prolazi.
