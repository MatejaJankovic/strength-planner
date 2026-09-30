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
je dolazio posle dve najlakše. I predlog „obrnut za hipertrofiju" nije imao oslonac. Kad je
volumen izjednačen, periodizacija ne menja rast mišića (Moesgaard i sar. 2022), a linearna i
talasasta daju sličan (Grgic i sar. 2017). Obrnut model nijedan od ta dva rada ne poredi, a
priručnik ga vezuje za snagu.

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

`Periodization.SuggestedModel` je linearan model za svaki blok, bez obzira na cilj i nivo.

Prva verzija ove grane je pratila priručnik: početnik dobija linearan, a ostali linearan za
hipertrofiju i obrnut za snagu, jer priručnik obrnut model zove „idealna za izgradnju snage i
izdržljivosti". Review je to vratio na proveru, i literatura je tu jasno na drugoj strani:

- Prestes i sar. 2009 (JSCR 23(1)) porede baš ova dva smera. Linearan je dao veće povećanje
  snage od obrnutog, a samo linearan je povećao nemasnu masu.
- González-Ravé i sar. 2022 (Sports Med Open), sistematski pregled: obrnuta periodizacija nije
  efikasnija ni za maksimalnu snagu, a tradicionalni smer je efikasniji za snagu i hipertrofiju.

To je slučaj za koji odluka D1 ostavlja izuzetak (priručnik je podrazumevan, osim gde je
literatura jasno drugačija), pa je zapisan u
[`analiza-prirucnika.md`](../analiza-prirucnika.md). Obrnut model ostaje na izboru.

Pošto predlog više ne zavisi od cilja, čarobnjak ga i ne vezuje za cilj. Promena cilja bloka
više ne menja model, jer bi samo poništila izbor korisnika. Novi blok nasleđuje model
prethodnog, kao što je već nasleđivao šablon. Server predlog više ne računa iz profila.

### Jedan auto-deload po bloku

Review je našao i grešku koja postoji od ranije. Kad umor
povuče deload napred, planirani deload na kraju se oslobađa i postaje trenažna nedelja. Ništa
nije sprečavalo da umorna nedelja 5 i tu oslobođenu nedelju 6 pretvori u drugi deload. Ako je
prvi auto-deload uzeo baš osnovnu nedelju (3), polazni broj serija se više nije mogao pročitati,
pa se izvodio iz oblika nedelje 6. Taj oblik je deload, pa je `BaseSetsFrom` bacao izuzetak.

Pravilo je sada u domenu, `AutoDeloadPlacement.NextWeek`: deload može da padne samo na sledeću
nedelju koja postoji, nije deload i nije počela, i samo dok blok nema auto-deload. Ranije je ovo
živelo u upitu servisa, koji nema test.

## Provera

- `dotnet test`: **774** (bilo 753); `npm test` 190 i `npm run build` prolaze.
- Novi testovi pokrivaju:
  - serije kroz trenažne nedelje ne padaju, a ponavljanja i rezerva ne rastu, za oba cilja i
    osnovu od 2 do 6 serija;
  - svaka trenažna nedelja se razlikuje za podrazumevane opsege i osnovu od 3 do 10;
  - talas 3/4/4/5/5 za srednji nivo, čitan iz `StartingSetsPerExercise`;
  - cilj nedelje raste od 12 do 20;
  - predlog je linearan i nikad stari model;
  - `AutoDeloadPlacementTests`, šest slučajeva, uključujući drugi auto-deload.
- Postojeći testovi koji prolaze kroz sve modele sada pokrivaju i novi: granica ponavljanja,
  RIR iznad nule, sigurne granice, jedan deload na kraju, svaka trenažna nedelja različita,
  RIR deload-a i fiksan broj ponavljanja.
- Test „granica po treningu menja samo nedelje u kojima je neko preko nje" ima strogu premisu
  i imenovan spisak od dve nedelje: Full Body (4 dana), početnik, snaga, linearan, nedelje 4 i
  5. Tamo propis stavlja 12 serija jednog mišića u trening. Predlog bez granice završi na 11,
  ali drugim putem, jer granica ceni već prvi korak. Spisak mora da se poklopi tačno, a za obe
  nedelje test proverava da propis zaista prelazi granicu. Prva verzija ove grane je premisu
  proširila na propis, a to je opravdavalo svaku nedelju, ne samo ove dve.
- Merenje vraćanjem, na 774 testa (commit, vraćeno pravilo, rebuild):
  - novi model sa starim oblikom obara 4;
  - predlog vraćen na obrnut model obara 1;
  - pravilo za auto-deload bez provere postojećeg auto-deload-a obara 1.
- End-to-end, uživo:
  - predlog blokova, i za početnika i za srednji nivo: hipertrofija i snaga dobijaju linearan;
  - novi linearan blok (Upper/Lower, srednji nivo, hipertrofija, podrazumevane granice): bench
    3 → 4 → 4 → 5 → 5 serija, RIR 2 → 1, ponavljanja 8–12 → 6–10. Ukupno serija u nedelji
    80 → 103 → 103 → 120 → 120, deload 48. Posle scenarija ispod, isti blok je dao
    75 → 97 → 97 → 108 → 108, jer su granice naučile niže (MRV za grudi 22 → 20). Posle
    „Vrati podrazumevane granice" brojevi su se vratili.
  - drugi auto-deload, srednji nivo, linearan blok. Nedelja 2 je odrađena teško (pola serija do
    otkaza, ostale četiri ponavljanja ispod opsega) i dobila ocenu 0.63. Nedelja 3 je postala
    auto-deload, a nedelja 6 oslobođena. Nedelja 5 je odrađena isto i dobila 0.66:
    - **stari kod:** poslednji trening nedelje 5 nije mogao da se završi — `400`, „Base sets can
      only be recovered from a training week of this block", stvarna vrednost 6;
    - **novi kod:** trening se završava, a nedelja 6 ostaje trenažna;
  - blok sa starim modelom (`periodizationModel: 1` preko API-ja): bench 6, 6, 4, 4, 3,
    nepromenjen, i u planu „Upper/Lower · Linearan (stari), 6 ned.";
  - čarobnjak (375 px, bez prelivanja): nudi Ravan, Linearan i Obrnut; oba bloka dobijaju
    Linearan; blok prebačen na snagu zadržava izabran Obrnut, a novi blok nasleđuje model
    prethodnog (Ravan → Ravan).

## Ograničenja

- API i dalje prima stari model (`1`), jer ga zapisani blokovi nose; čarobnjak ga samo ne nudi.
- Blokovi plana koji su napravljeni ranije, a još nisu generisani, generišu se modelom koji je
  tada izabran — i ako je to stari linearan, ostaje stari.
- Dva slučaja u kojima dve nedelje linearnog modela nose isti propis, jer serija ne ide ispod 2 i
  ponavljanje ispod 3:
  - kod osnove od 2 serije nedelja 1 se poklapa sa nedeljom 2;
  - kod opsega sa vrhom do 4 ponavljanja nedelja 4 se poklapa sa nedeljom 5.

  Obe stvari nastaju samo u ličnom šablonu. Ugrađeni počinju od bar 3 serije, a snaga ima opseg
  3–6.
- Blok povlači deload napred najviše jednom. Vežbač koga umor stigne i posle ranog deload-a
  završava blok bez drugog, a sledeći blok kreće od novog propisa.
