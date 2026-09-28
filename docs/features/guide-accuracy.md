# Četiri tvrdnje koje više nisu bile tačne

Nalaz D22 iz pregleda logike treninga. Nijedna izmena ponašanja — samo mesta na kojima je
tekst ostao iza koda.

## 1. „Sedam ugrađenih" — ima ih devet

Uputstvo je tvrdilo sedam i nabrajalo sedam. Katalog ima **devet**: dva šablona iz kasnijih
rundi nikada nisu stigla do uputstva.

| Nedostajalo | Dana |
|---|---|
| Legs Specialization (5 dana) | 5 |
| Upper/Lower x3 (6 dana) | 6 |

**Ovo je jedina stavka koja je dobila test.** `GuideTemplateTableTests` čita
`HowToUseApp.md` i traži ime svakog šablona iz kataloga, plus broj napisan rečju — i da uz
„ugrađenih" ne stoji **nijedan drugi** broj, jer bi inače uputstvo moglo da protivreči samo
sebi a test da ostane zelen. Provereno da ima zube: sa vraćenim starim tekstom pada **2 od
2**. Kad fajl nije dostupan (spakovan izlaz van radnog stabla), test ćuti umesto da padne.

## 2. Tonaža „da se vidi da li blok stvarno raste" — ne u periodizovanom bloku

Faza intenziteta namerno nosi manje posla. Izmereno nad domenskim propisom, linearan
hipertrofijski blok (osnova 4 serije, 8–12):

| Nedelja | Propis | Ponavljanja rada |
|---|---|---|
| 1 | 6 × 8–12 @RIR 2 | 60 |
| 2 | 6 × 8–12 @RIR 1 | 60 |
| 3 | 4 × 8–12 @RIR 1 | 40 |
| 4 | 4 × 7–11 @RIR 1 | 36 |
| 5 | 3 × 6–10 @RIR 1 | **24** |
| 6 | 2 × 8–12 @RIR 3 | 20 (deload) |

Sa druge na petu nedelju propis pada za **60%**, dok opterećenje u istom razmaku poraste za
korak-dva. Tonaža zato **mora** da pada kroz periodizovan blok iako sve ide po planu.
Uputstvo sada kaže da se tonaža poredi sa istom fazom prethodnog bloka, i da samo u
**ravnom** bloku rast iz nedelje u nedelju znači da si jači.

## 3. Čarobnjak se otvara sa dva bloka, a odeljak A opisuje plan od jednog

Odeljak „A) Plan od jednog bloka (preporučeno za početak)" vodi korisnika na „Napravi plan",
a čarobnjak se otvori sa **dva** predložena bloka — smenjivanje ciljeva je trenažno pravilo
i dolazi sa servera. Početnik koji prati uputstvo zato vidi nešto drugo od onoga što je
pročitao.

Kod se ne menja: predlog je namerno takav. Uputstvo sada kaže šta će videti i kako da obriše
drugi blok.

## 4. `Periodization.cs`: „stays the default" — dva značenja iste reči

Komentar je tvrdio da je ravan model podrazumevan. Jeste — kao **zapisana** vrednost:
`Mesocycle.PeriodizationModel` pada na njega, i to je ono što blokove nastale pre uvođenja
modela drži čitljivim.

Ali nov plan ga **ne** dobija: čarobnjak predlaže linearan za blok snage i obrnut za
hipertrofiju, a ravan samo kada ga vežbač sam izabere. Komentar je govorio jedino ono
značenje koje se sa ekrana ne vidi.

## Šta je provereno

- `dotnet build` čist, **613** testova prolazi (611 → 613).
- Devet šablona prebrojano iz kataloga i potvrđeno uživo u padajućem meniju čarobnjaka.
- Brojevi u tabeli tonaže izmereni pozivanjem `Periodization.ForBlock`, ne procenjeni.
- Test za uputstvo dokazano pada na starom tekstu.
