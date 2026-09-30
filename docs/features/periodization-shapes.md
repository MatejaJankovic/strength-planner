# Linearan model po priručniku

**Grana:** `fix/periodization-shapes`

Peta grana iz revizije nauke o treningu (odeljak F), nalaz F3, odluka D2.

## Problem

Modeli periodizacije su nosili imena iz priručnika, ali ne i njegove sheme.

| | Priručnik (str. 13) | Aplikacija do sada |
|---|---|---|
| Linearan — serije | 3 → 4 → 4 → 5 → 5, **rastu** | +1, +1, 0, 0, −1, dakle **padaju** (hipertrofija, srednji nivo: 6, 6, 4, 4, 3) |
| Linearan — ponavljanja, RIR | padaju | padaju |
| Obrnut — RIR | 0–1 → 3–4, **raste** | 3 → 1, **pada** |
| Predlog modela | linearan „idealan za početnike", obrnut „za snagu i izdržljivost" | snaga → linearan, hipertrofija → obrnut, bez obzira na nivo |

Stari linearan model je klasična linearna periodizacija (od volumena ka intenzitetu), i kao
takva nije pogrešna. Ali je protivrečio okviru koji aplikacija sama koristi za granice volumena:
MEV → MRV znači da volumen raste kroz blok i da deload dolazi posle najteže nedelje. Linearan
hipertrofijski blok je dve najsvežije nedelje gađao MRV (cilj za grudi 22/22/16/16/12), a deload
je dolazio posle dve najlakše. I predlog „obrnut za hipertrofiju" nije imao oslonac: kad je volumen
izjednačen, nijedan model nije bolji za rast mišića (Grgic i sar. 2017; Moesgaard i sar. 2022), a
priručnik obrnut model vezuje za snagu.

## Rešenje

### Novi model, stari ostaje

`PeriodizationModel.LinearRising` je linearan model iz priručnika. `Linear` ostaje, sa istim
oblikom i istim imenom, za svaki blok koji ga već nosi: model se u bazi čuva kao tekst, i na
razvojnoj bazi ga nose 25 mezociklusa i 34 bloka plana. Novo ime umesto kolone „revizija"
(kako je plan predlagao) zato što model već putuje kroz svako mesto koje čita oblik nedelje —
generator, deload, cilj nedelje, trajanje bloka — pa nije trebala ni migracija ni novi parametar;
odluka je i dalje zapisana na redu koji opisuje. Čarobnjak stari model više ne nudi, a plan ga
prikazuje kao „Linearan (stari)".

| Nedelja | Pomeraj | Snaga, osnova 4 serije | Hipertrofija, osnova 4 serije |
|---|---|---|---|
| 1 | serija manje, rezerva +1 | 3 × 3–6 @RIR 3 | 3 × 8–12 @2 |
| 2 | rezerva +1 | 4 × 3–6 @3 | 4 × 8–12 @2 |
| 3 | osnova | 4 × 3–6 @2 | 4 × 8–12 @1 |
| 4 | serija više, ponavljanje manje, rezerva −1 | 5 × 3–5 @1 | 5 × 7–11 @1 |
| 5 | serija više, dva ponavljanja manje, rezerva −1 | 5 × 3–4 @1 | 5 × 6–10 @1 |
| 6 | deload | 2 × 3–6 @4 | 2 × 8–12 @3 |

Za srednji nivo to je tačno priručnikov talas serija, 3 → 4 → 4 → 5 → 5. Dve odluke u obliku:

- **Rane nedelje su lakše po rezervi, ne po ponavljanjima.** Priručnik počinje sa više
  ponavljanja (10 → 5), ali hipertrofija ovde već stoji na Epley granici (12): pomeraj naviše bi
  se vratio kao serija više (`CappedShiftSetBonus`) — baš u nedeljama koje treba da nose manje
  serija.
- **Rezerva raste za jedan, ne za dva.** Prva verzija je nedelju 1 bloka snage stavljala na
  RIR 4, gde serija prestaje da se broji kao ceo stimulus (`StimulativeVolume`); priručnik
  linearan blok počinje na RIR 2–3.

Cilj nedelje prati propis: kod grudi (MEV 10, MAV 16, MRV 22) 12 → 16 → 16 → 20 → 20, pa deload.
Signal volumena u oceni umora se sada puni pred kraj bloka, a ne na početku.

### Obrnut model

Oblik se ne menja: RIR i dalje pada kroz blok (3 → 1), dok ga priručnik diže (0–1 → 3–4). To je
RP akumulacija — nedelje bliže otkazu kako se volumen gomila — i u skladu je sa tim kako aplikacija
meri umor: nedelja propisana daleko od otkaza ne bi imala šta da izmeri. Zapisano kao namerno
odstupanje u [`analiza-prirucnika.md`](../analiza-prirucnika.md).

### Predlog modela

`Periodization.SuggestedModel(nivo, cilj)`: početnik dobija linearan za oba cilja; ostali linearan
za hipertrofiju (akumulacija ka MRV-u, okvir oko koga su granice volumena i napravljene) i obrnut
za snagu, kako priručnik kaže. Server predlaže blokove po nivou korisnika, a čarobnjak kad
promeni cilj bloka koristi predlog koji je server već dao — pravilo živi na jednom mestu.

## Provera

- `dotnet test`: **769** (bilo 753); `npm test` 188 i `npm run build` prolaze.
- Novi testovi: serije kroz trenažne nedelje ne padaju, a ponavljanja i rezerva ne rastu — za
  oba cilja i osnovu od 2 do 6 serija; talas 3/4/4/5/5 za srednji nivo; cilj nedelje 12 → 20;
  predlog po nivou i cilju i da stari model nikad nije predložen. Postojeći testovi koji prolaze
  kroz sve modele (granica ponavljanja, RIR iznad nule, sigurne granice, jedan deload na kraju,
  svaka trenažna nedelja različita) sada pokrivaju i novi.
- Jedan postojeći test je morao da se preciznije postavi: „granica po treningu menja samo
  nedelje u kojima je neko preko nje" je gledao samo predlog bez granice. Novi model u nedeljama
  4–5 dodaje seriju, pa Full Body (4 dana), početnik, snaga, kreće od propisa sa 12 serija jednog
  mišića i završava na 11 i bez granice — ali drugim putem, jer je granica cenila već prvi korak.
  Test sada broji i propis.
- Merenje vraćanjem (commit, vraćeno pravilo, rebuild): novi model sa starim oblikom — 3 od 769;
  stari predlog (snaga → linearan, hipertrofija → obrnut) — 7 od 769.
- End-to-end, uživo:
  - predlog blokova: početnik — hipertrofija i snaga linearan; srednji nivo — hipertrofija
    linearan, snaga obrnut;
  - novi linearan blok (Upper/Lower, srednji, hipertrofija): bench 3 → 4 → 4 → 5 → 5 serija,
    RIR 2 → 1, ponavljanja 8–12 → 6–10; ukupno serija u nedelji 80 → 103 → 103 → 120 → 120, deload
    48;
  - blok sa starim modelom (`periodizationModel: 1` preko API-ja): bench 6, 6, 4, 4, 3 — nepromenjen
    — i u planu „Upper/Lower · Linearan (stari), 6 ned.";
  - čarobnjak (375 px, bez prelivanja): nudi Ravan, Linearan i Obrnut; blok hipertrofije dobija
    Linearan, blok snage Obrnut, a promena cilja bloka menja i model po predlogu sa servera.

## Ograničenja

- API i dalje prima stari model (`1`), jer ga zapisani blokovi nose; čarobnjak ga samo ne nudi.
- Blokovi plana koji su napravljeni ranije, a još nisu generisani, generišu se modelom koji je
  tada izabran — i ako je to stari linearan, ostaje stari.
