# Deload početnika

**Grana:** `fix/beginner-deload`

Osma grana iz revizije nauke o treningu (odeljak F), nalaz F10, odluka D5.

## Problem

Početnik je dobijao planirani deload na kraju svakog bloka, pa i ravnog bloka od četiri
nedelje. U takvom bloku je rasterećenje bilo **četvrtina** njegovog treninga.

- Priručnik (str. 13) o deload-u kaže: „Početnici ne treba da razmišljaju o ovome." Zatim
  dodaje da napredni vežbači, čiji treninzi značajno opterećuju CNS, mišiće i tetive, moraju
  da ga planiraju redovno.
- Pancar i sar. 2026 (Scientific Reports), 19 netreniranih mladića, 8 nedelja: deload sa
  smanjenim volumenom i učestalošću u 4. i 8. nedelji nije ni pomogao ni odmogao rastu
  mišića i snažnoj izdržljivosti. Za početnika deload dakle nije pokazao korist, pa
  literatura nije jasno na drugoj strani od priručnika, i važi priručnik (odluka D1).

Automatski deload zbog umora početnik ionako nema (`DeloadThreshold` je `null`), pa je
planirani bio jedini, i nije imao uporište.

## Rešenje

- `Periodization.HasPlannedDeload(model, nivo)`: false samo za ravan blok početnika.
- Nove verzije `ForWeek` i `ForBlock` primaju nivo. U bloku bez planiranog deload-a, nedelja
  koja bi bila deload nosi osnovni propis, isti kao prve tri. Blok zadržava dužinu, pa se ni
  datumi plana ni naredni blokovi ne pomeraju.
- Generator gradi blok preko verzija sa nivoom. Servisi `IsDeload` čitaju sa sačuvanih
  nedelja, pa njih nije trebalo menjati. Po kodu, granice volumena, cilj nedelje i ocena
  umora četvrtu nedelju tretiraju kao svaku trenažnu (uživo to nije posebno mereno).
- **Periodizovani blok početnika zadržava deload.** I priručnikova linearna i obrnuta šema
  završavaju sa „Nedelja 6: DELOAD", a najteže nedelje bloka vode ka njoj. Pošto je
  linearan model sada predlog za svaki blok (#90), većina početnika i dalje ima deload. Bez
  njega ostaje samo početnik koji sam izabere ravan blok.
- Obrazloženje praga umora više ne tvrdi da početnik loše procenjuje RIR. To je tvrdnja
  priručnika, a meta-analiza tačnosti RIR-a (Halperin i sar. 2022) nije našla da iskustvo
  utiče na tačnost. Pravilo ostaje, a razlog je sada priručnik i Pancar 2026.

## Šta je review ispravio

- **Coleman i sar. 2024 je bio pogrešno opisan.** Prva verzija ga je navodila kao dokaz za
  početnike („nedelja rasterećenja ... malo je umanjila snagu"). Studija je imala trenirane
  vežbače (bar godinu dana treninga), a „deload" je bio nedelja potpune pauze usred
  programa od devet nedelja. Hipertrofija donjeg dela tela je bila ista, a dobitak snage
  manji. To nije ni populacija ni deload o kome je reč, pa više nije razlog. Uz to je
  izbačena i tvrdnja da nepotreban deload početnika „košta nedelju napretka": Pancar 2026
  kod netreniranih nije našao ni korist ni štetu.
- **Testovi nisu držali reč „samo".** Tabela je imala 6 od 12 parova nivo × model, a drugi
  test je granu birao samim pravilom koje proverava. Mutant koji deload skida i naprednom u
  linearnom bloku je prošao svih 813. Sada test prolazi kroz svih 12 parova sa očekivanjem
  zapisanim nezavisno, i proverava i samu poslednju nedelju.
- Zastareli tekst: komentar u `DeloadService` (stari razlog i „ostaje samo planirani
  deload"), dva mesta u uputstvu (tabela ličnog šablona i „Na kraju bloka"), „četiri
  stvari" iznad tabele sa pet, opis ravnog modela u `PeriodizationModel` i beleške iz ranijih
  rundi (`experience-level.md`, `periodization-models.md`, `analiza-prirucnika.md`).
- Stranica priručnika je 13, ne 12.

## Šta rad kaže

Tri rečenice teze (verzija 2.1) treba uskladiti sa aplikacijom:

- zaključak: „Ауто-регулација почива на субјективној RIR процени, која је код почетника
  непоуздана" - Halperin i sar. 2022 ne nalaze da iskustvo utiče na tačnost;
- slučaj korišćenja 4: sistem „креира четири недеље - три акумулационе и завршну deload
  недељу" - danas postoje i šestonedeljni modeli, a ravan blok početnika deload nema;
- opis slike 7.7: „Четврта недеља носи ознаку DELOAD" - ne važi za ravan blok početnika.
- Čarobnjak za ravan model kaže da početnik četvrtu nedelju trenira kao ostale. Uputstvo
  ima novi red u tabeli nivoa i izuzetak u odeljku o modelima.

## Provera

- `dotnet test`: **808** (bilo 804); `npm test` 192 i `npm run build` prolaze.
- Novi testovi:
  - `HasPlannedDeload` za svih 12 parova nivo × model, sa očekivanjem zapisanim nezavisno;
  - ravan blok početnika ima četiri trenažne nedelje sa istim propisom, za oba cilja;
  - nivo ne menja nijedan drugi blok, a u ravnom bloku početnika je poslednja nedelja baš
    osnovna.
- Merenje vraćanjem (commit, rebuild):
  - pravilo uvek „ima deload" obara **4** od 808;
  - mutant iz review-a (deload skinut i naprednom u linearnom bloku) obara **2** od 808.
    Pre ispravke testova je prolazio svih 813.
- **Spoj u generatoru ne vidi nijedan test.** Vraćen samo generator na verzije bez nivoa:
  0 od 808 (i 0 od 813 pre ispravki), a uživo se deload vraća u četvrtu nedelju. To je isti oblik kao u rundi 12, gde
  pravilo živi u servisu, pa zato oba smera ide i uživo.
- End-to-end, uživo (Upper/Lower, hipertrofija):
  - početnik, ravan: 4 trenažne nedelje, bench 3 × 8–12 @RIR 1 svake nedelje, poslednji
    trening 30. 10. — isti datum kao i ranije;
  - početnik, linearan: deload ostaje u 6. nedelji (2 × 8–12 @RIR 3);
  - srednji nivo, ravan: deload ostaje u 4. nedelji;
  - ekran treninga (375 px, bez prelivanja): nedelja 4 bez oznake deload-a.

## Ograničenja

- Blokovi napravljeni ranije zadržavaju deload sa kojim su generisani.
- Početnik koji usred bloka pređe na srednji nivo zadržava blok kakav je generisan, jer se
  nivo čita sa bloka (runda 12).
