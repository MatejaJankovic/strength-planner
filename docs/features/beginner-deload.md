# Deload početnika

**Grana:** `fix/beginner-deload`

Osma grana iz revizije nauke o treningu (odeljak F), nalaz F10, odluka D5.

## Problem

Početnik je dobijao planirani deload na kraju svakog bloka, pa i ravnog bloka od četiri
nedelje. U takvom bloku je rasterećenje bilo **četvrtina** njegovog treninga.

- Priručnik (str. 12) o deload-u kaže: „Početnici ne treba da razmišljaju o ovome." Zatim
  dodaje da napredni vežbači, čiji treninzi značajno opterećuju CNS, mišiće i tetive, moraju
  da ga planiraju redovno.
- Coleman i sar. 2024 (PeerJ): nedelja rasterećenja usred devetonedeljnog programa nije
  dodala ništa rastu mišića, a snagu donjeg dela tela je malo umanjila.

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
  utiče na tačnost. Pravilo ostaje, a razlog je sada priručnikov i Colemanov.
- Čarobnjak za ravan model kaže da početnik četvrtu nedelju trenira kao ostale. Uputstvo
  ima novi red u tabeli nivoa i izuzetak u odeljku o modelima.

## Provera

- `dotnet test`: **813** (bilo 804); `npm test` 192 i `npm run build` prolaze.
- Novi testovi:
  - tabela `HasPlannedDeload` po nivou i modelu;
  - ravan blok početnika ima četiri trenažne nedelje sa istim propisom, za oba cilja;
  - nivo ne menja nijedan drugi blok.
- Merenje vraćanjem (commit, pravilo uvek „ima deload", rebuild): obara **3** od 813.
- **Spoj u generatoru ne vidi nijedan test.** Vraćen samo generator na verzije bez nivoa:
  0 od 813, a uživo se deload vraća u četvrtu nedelju. To je isti oblik kao u rundi 12, gde
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
