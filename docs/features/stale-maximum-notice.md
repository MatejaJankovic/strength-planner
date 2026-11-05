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
- **Ekran treninga:** ispod spiska vežbi bez maksimuma stoji napomena sa vežbama čija
  opterećenja još stoje na starom maksimumu, uz starost („Barbell Row (star 10 nedelja)").
  Broji se vežba koja čeka u nekom treningu, a na tom danu bloka još nije odrađena
  (`features/workout/stale-max-exercises.ts`): završen trening prepisuje opterećenje iste
  vežbe za isti dan sledeće nedelje, i to iz dignutih serija, pa za odrađen dan tvrdnja „plan
  polazi od starog maksimuma" više ne važi. Napomena predlaže da se prvi trening odradi
  opreznije i da se težina spusti ako treba.
- **Ekran maksimuma:** uz red piše „Star 10 nedelja, jer novijeg zapisa nema", i da nov unos
  ili test-serija menja maksimum, ali ne i opterećenja već upisana u plan.
- Uputstvo, odeljak „Start novog bloka", to sada kaže.

**Procenat pada se ne nudi.** Pad snage posle pauze zavisi od njene dužine i od treniranosti
(meta-analiza Bosquet i sar. 2013), pa jedan procenat za sve ne bi bio tačan, a izgledao bi
kao proračun. Zato ekran kaže samo koliko je zapis star.

## Posle pregleda

Prva verzija je napomenu vezivala za zapise, a ne za plan, i obećavala više nego što radi:

- Predlagala je test-seriju, i test-serija je zaista uklanjala napomenu, ali **ne i
  opterećenja**: ciljevi prve nedelje se upisuju pri generisanju i novi zapis ih ne prepisuje.
  Provera uživo je gledala napomenu, a ne cilj. Tekst sada kaže da se upisana opterećenja ne
  menjaju, a dugme vodi na pregled maksimuma umesto na „Uradi test-seriju".
- Navodila je i vežbe koje je plan već prestao da vodi po maksimumu (odrađene u bloku), i
  tvrdila da „prvi završen trening upisuje novu procenu". To ne važi za drugi dan iste nedelje,
  za izolacije iznad 12 ponavljanja ni za deload nedelju. Sada se gleda par dan + vežba.
  Prvi ispravak je gledao samo vežbu, i tada bi završen Upper A sakrio bench iz Upper B iste
  nedelje, koji i dalje stoji na starom maksimumu: sledeći plan se traži po `DayLabel`.
- „Ako je nisi radio" je stajalo i uz više vežbi; sada „ako ih".

Da li je zapis star odlučuje server, jer samo on zna prozor. Klijent samo broji nedelje za tekst
(`shared/stale-max-age.ts`, zaokruženo naniže, sa ispravnim padežom).

## Provera

- `dotnet test`: **856** (bilo 849). Novi testovi: rezervni izbor je izvan prozora; granica
  (0, 55, 56, 57 dana) je ista kao u `Select`; ista vrednost u prozoru nije stara; noviji zapis
  od 0 kg ne osvežava stari maksimum.
- Merenje vraćanjem (commit, `IsOutsideLookback` uvek `false`, rebuild): obara **2** od 854.
- Klijent: **201** (bilo 192): brojanje nedelja i padež, i pravilo napomene (vežba koja čeka,
  odrađen dan, drugi dan, preskočen trening, svež maksimum, redosled).
- Uživo, na 375 px, nalog za testiranje kome su zapisi za Barbell Row pomereni 70 dana unazad:
  - ekran treninga: „Za ovu vežbu plan polazi od starog maksimuma, jer novijeg nema: Barbell
    Row (10 nedelja)", bez prelivanja;
  - „Uradi test-seriju" otvara ekran maksimuma, gde red kaže „Star 10 nedelja";
  - test-serija 55 kg × 5 uz RIR 1 upisuje svež zapis: oznaka nestaje sa reda, a napomena sa
    ekrana treninga.
- Posle pregleda: merenje vraćanjem pravila dan + vežba (samo vežba) obara **1** od 201 na
  klijentu. Uživo, na bloku u kome je Barbell Row odrađen na svom danu, a svi zapisi su mu
  stari 70 dana, napomena se **ne** prikazuje, jer plan tu vežbu više ne vodi po maksimumu.
  Pozitivan slučaj na novom planu (prva nedelja iz starog maksimuma) posle ove izmene još nije
  proveren uživo.
- Stilovi nisu dodati: napomena koristi postojeće klase, pa se upozorenja o budžetu
  stilova nisu promenila.

## Šta ostaje

- Serverski deo koji postavlja `IsOutsideLookback` u `OneRepMaxService` nema test: projekat
  nema harness na nivou servisa (isto kao u rundi 12). Proveren je uživo.
- Test-serija ne prepisuje već upisana opterećenja. Da bi to radila, novi zapis bi morao da
  preračuna ciljeve treninga koji čekaju i još nisu vođeni progresijom; to je veća izmena i
  ovde nije urađena.
- Napomena ne zna da li je vežbač stvarno pravio pauzu. Vežba koja dva meseca nije bila u
  bloku dobija istu napomenu, i tu je ona jednako tačna: broj je star.
