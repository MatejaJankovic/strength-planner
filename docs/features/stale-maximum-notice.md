# Star maksimum

**Grana:** `feature/stale-maximum-notice`

Iz revizije nauke o treningu (odeljak F), nalaz F15.

## Problem

Blok polazi od najboljeg maksimuma iz poslednjih 56 dana. Kad u tom prozoru nema ničega,
generator namerno uzima najnoviji zapis ikada, da se vežbač koji se vraća posle pauze ne vrati
i na prazna opterećenja. To pravilo je dobro, ali je bilo nevidljivo: opterećenje izvedeno iz
maksimuma starog tri meseca izgledalo je isto kao ono iz jučerašnjeg treninga. Taj broj opisuje
vežbača od pre pauze, a snaga posle nekoliko nedelja bez treninga opada.

## Šta je urađeno

- `OneRepMaxBaseline.IsOutsideLookback` kaže da li je zapis izvan prozora. Koristi isti rez kao
  `Select`: zapis star tačno 56 dana je još u prozoru. Za zapis koji `Select` izabere to znači
  da ga je izabrao rezervni put, jer novijeg nema.
- `OneRepMaxDto.IsOutsideLookback` nosi odgovor, i to samo kod trenutnih maksimuma
  (`GET` trenutnih vrednosti), gde to pitanje ima smisla.
- **Ekran treninga:** ispod spiska vežbi bez maksimuma stoji napomena sa vežbama iz bloka čiji
  je maksimum star, uz starost u nedeljama („Barbell Row (10 nedelja)"). Predlaže opreznije
  prvi trening ili test-seriju, i kaže da prvi završen trening upisuje novu procenu. Dugme vodi
  na ekran maksimuma.
- **Ekran maksimuma:** uz red piše „Star 10 nedelja - plan ga koristi jer novijeg nema".
- Uputstvo, odeljak „Start novog bloka", to sada kaže.

**Procenat pada se ne nudi.** Literatura o prestanku treninga ne daje broj koji bi se mogao
oduzeti od maksimuma (pad zavisi od dužine pauze, iskustva i vrste snage), a izmišljen
procenat bi izgledao kao proračun. Zato ekran kaže samo koliko je zapis star.

Da li je zapis star odlučuje server, jer samo on zna prozor. Klijent samo broji nedelje za tekst
(`shared/stale-max-age.ts`, zaokruženo naniže, sa ispravnim padežom).

## Provera

- `dotnet test`: **854** (bilo 849). Novi testovi: rezervni izbor je izvan prozora, i granica
  (0, 55, 56, 57 dana) je ista kao u `Select`.
- Merenje vraćanjem (commit, `IsOutsideLookback` uvek `false`, rebuild): obara **2** od 854.
- Klijent: **195** (bilo 192), za brojanje nedelja i padež.
- Uživo, na 375 px, nalog za testiranje kome su zapisi za Barbell Row pomereni 70 dana unazad:
  - ekran treninga: „Za ovu vežbu plan polazi od starog maksimuma, jer novijeg nema: Barbell
    Row (10 nedelja)", bez prelivanja;
  - „Uradi test-seriju" otvara ekran maksimuma, gde red kaže „Star 10 nedelja";
  - test-serija 55 kg × 5 uz RIR 1 upisuje svež zapis: oznaka nestaje sa reda, a napomena sa
    ekrana treninga.
- Stilovi nisu dodati: napomena koristi postojeće klase, pa se upozorenja o budžetu
  stilova nisu promenila.

## Šta ostaje

- Napomena ne zna da li je vežbač stvarno pravio pauzu. Vežba koja dva meseca nije bila u
  bloku dobija istu napomenu, i tu je ona jednako tačna: broj je star.
- Serija koja ne prolazi `CanEstimateFrom` (npr. RIR 4) ne upisuje procenu, pa napomena tada
  ostaje i posle odrađenog treninga.
