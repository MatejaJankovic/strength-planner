# Od koje težine progresija polazi, i šta rezime zaista kaže

Grana `fix/progression-reference-and-summary`. Druga od četiri grane iz pregleda logike
treninga (odeljak A). Pokriva nalaze A3 (prosek težina), A4 (vežba bez serija) i A5 (strelica
u rezimeu), plus jedan koji je revizija dizajna našla usput: **odskok posle deload-a**.

## Problem 1: progresija je polazila od proseka težina (A3)

`SessionService` je radio `logs.Average(set => set.WeightKg)`. Trening 100 / 100 / 80 kg
× 12 ponavljanja sa RIR 1 davao je referencu 93.33 kg i predlog **95 kg** — ispod težine
koju je vežbač u istom treningu podigao dva puta. Ista formula je određivala i osnovu
deload-a (`DeloadService`), pa je jedna back-off serija spuštala celu deload nedelju.

## Problem 2: vežba bez serija je nosila punu težinu (A4)

Grana `if (logs.Count == 0)` je planiranu težinu prepisivala u narednu nedelju i tu se
zaustavljala — preskačući pravilo za narednu nedelju u celosti:

- **deload nedelja** je dobijala 100% umesto 90%;
- **periodizovana nedelja** je dobijala težinu izvedenu za tuđi rep-opseg.

Teza to i zahteva izričito („u deload nedelji predlozi se izvode iz stvarno korišćenih
težina, 90%"), pa je ovo bio raskorak sa specifikacijom, ne samo nelogičnost.

## Problem 3: strelica u rezimeu nije govorila o prikazanom broju (A5)

`summary.WeightIncreased` je dolazio iz progresije, a posle njega je pravilo za narednu
nedelju umelo da **prepiše** predlog (90% pred deload, preračun iz e1RM-a). Rezime je zato
pokazivao „90 kg ↑". Prva grana je zastavicu izračunala iz konačnog broja; ova ide dalje i
prikazuje **razliku**, jer „↑" bez broja ne kaže koliko.

## Problem 4: odskok posle deload-a (nađeno u reviziji dizajna)

Kad auto-deload povuče rasterećenje ranije, planirani deload se oslobađa i postaje trenažna
nedelja — dakle **postoji nedelja posle deload-a**. Progresija je za nju polazila od
olakšanih deload serija, pa je opterećenje padalo ispod već zarađenog:

| | Pre | Sada |
|---|---|---|
| Nedelja 1: 3 × 12 na 100 kg (RIR 0) | zarađeno 102.5 kg | zarađeno 102.5 kg |
| Nedelja 2 postaje deload | 90 kg (90%) | 90 kg (90%) |
| Nedelja 3 posle deload-a | **97.5 kg** (90 × 1.06 + korak) | **102.5 kg** |

## Rešenje

Tri nova domenska pravila; sve što je testirano moralo je da izađe iz servisa, jer za
`SessionService` u projektu nema test harness-a.

### `WorkingLoad` — koja težina i koje serije

Referenca je **najteža** podignuta težina. Lakše serije nisu prosto odbačene:

- lakša serija koja je završila bez rezerve (RIR 0 ili otkaz) **ulazi** — otkaz na 90 kg
  važi i za 100 kg, pa takva serija korekciju može samo da povuče naniže;
- lakša serija sa rezervom **ne ulazi** — njen RIR je izmeren na drugoj težini.

`ExcludedLighterSets` pamti koliko ih je izostavljeno, da se to kasnije može prikazati.

### `NextWeekLoad` — pravilo za narednu nedelju, na jednom mestu

Privatni `NextTargetWeight` je obrisan. Novo pravilo prima referencu, predlog progresije,
oba propisa, da li je naredna nedelja deload, procenu maksimuma i korak vežbe:

1. **deload** → 90% reference; ako reference nema, 90% težine izvedene iz maksimuma;
2. **isti propis** → predlog progresije, pa referenca (preskočena vežba nosi plan), pa
   maksimum;
3. **drugi propis sa procenom maksimuma** → težina za taj propis (kao u prvoj nedelji);
4. **drugi propis bez procene** → preko *implicitnog* maksimuma same težine: 80 kg u nedelji
   11–12 @RIR1 znači maksimum 112 kg, a to za 8–12 @RIR1 daje 85 kg. Nošenje neizmenjenih
   80 kg u drugi opseg je jedino što je sigurno pogrešno;
5. **ništa poznato** → `null`, i zatečeni cilj se **ne** prepisuje praznom vrednošću.

`UndoDeload` vraća težinu od koje je deload izveden (deli sa 0.90), a `ChangeKg` razliku za
rezime.

### Izlaz iz deload-a

Kad se završava deload nedelja a naredna nije deload, `SessionService` pronađe **poslednju
odrađenu trenažnu sesiju istog dana**, na njenim serijama pusti `WorkingLoad` i
`ProgressionEngine`, i taj rezultat prosledi pravilu. Ako takve sesije nema, referenca se
vraća iz same deload težine (`UndoDeload`).

### Rezime se zaključuje na kraju

`RefreshSummariesAfterDeload` je zamenjen `FinalizeSummaries`, koji se poziva **posle**
auto-deload-a: predlog se čita iz upisanog plana naredne nedelje (praćena instanca, pa se u
njoj vidi i deload), pa se računa razlika prema `UsedWeightKg`. Kad naredne nedelje u bloku
nema, oznake nema — sledeći blok svoje težine izvodi iz procene maksimuma.

Oznaka se pravi u čistoj funkciji `next-weight-label.ts` (`102.5 kg ↑ +2.5`, `90 kg ↓ −10`,
`100 kg`), sa testom za nulu i za nedostajuću vrednost — dva slučaja koja izraz u šablonu ne
bi pokrio.

## Testovi

`WorkingLoadTests` (7) i `NextWeekLoadTests` (16): izbor reference, uključivanje lakšeg
otkaza, sva pet pravila, `UndoDeload`, `ChangeKg`. Svi brojevi u testovima su izračunati
ručno i upisani kao očekivanja, uključujući konverziju kroz implicitni maksimum (80 → 85 i
82.5 → 92.5). Frontend: `next-weight-label.spec.ts` (6).

`dotnet test`: 424 (bilo 401). Frontend: 128 (bilo 122).

## Provereno u živoj aplikaciji

Ravan blok hipertrofije (Upper/Lower), nalog srednjeg nivoa, širina telefona.

**A3 i A5** — Bench Press, nedelja 1: serije 100 / 100 / 80 kg × 12 @RIR1.

| | Pre | Sada |
|---|---|---|
| referenca | 93.33 kg (prosek) | 100 kg (najteža) |
| predlog za nedelju 2 | 95 kg | **102.5 kg** |
| oznaka u rezimeu | `95 kg ↑` | `102.5 kg ↑ +2.5` |

**A4** — bench preskočen u nedeljama 2 i 3 (planirano 102.5 kg): nedelja 3 nosi 102.5 kg, a
deload nedelja **92.5 kg** (90% od 102.5). Pre ove grane deload je nosio punih 102.5.

**A5 pred deload** — Back Squat, nedelja 3, tri serije po 12 na 100 kg: rezime pokazuje
`90 kg ↓ −10`, neutralnim tonom. Pre prve grane je tu stajala strelica naviše.

**Odskok posle deload-a** — nedelja 1: bench 3 × 12 @RIR0 na 100 kg (zarađeno 102.5) i
Barbell Row 4 × 5 do otkaza; ocena umora 0.60 je pretvorila nedelju 2 u deload i oslobodila
planirani u nedelji 4. Deload nedelja odrađena sa 2 × 12 @RIR3 na 90 kg → **nedelja 3 nosi
102.5 kg**, ne 97.5. Vežbe bez serija u nedelji 1 su se vratile na pređašnje planirano
(Cable Fly 110, Triceps Pushdown 45), umesto da ostanu na 90% deload vrednosti.

Bez vodoravnog preliva na 375 px; konzola bez grešaka.

## Poznata ograničenja

- **Referenca je najteža težina, pa ascendentna piramida (80 / 90 / 100) napreduje samo iz
  gornje serije.** To je i namera: `SetLog` u dokumentaciji kaže da se zagrevanje ne upisuje.
- **Razlika se poredi sa onim što je podignuto danas.** Posle deload nedelje to znači veliku
  pozitivnu razliku (`102.5 kg ↑ +12.5` naspram 90 kg iz deload serija) — tačno, ali izgleda
  kao skok. Alternativa bi bila porediti sa zarađenom težinom, što bi značilo dva različita
  „pre" u istom ekranu.
- **Broj izostavljenih lakših serija se nigde ne prikazuje**, iako ga `WorkingLoad` vraća.
- Zaokruživanje i dalje može da referencu van mreže koraka pomeri za manje od pola koraka
  (npr. 101 → 102.5 uz pozitivnu korekciju); pravilo iz prve grane garantuje samo da smer
  korekcije ostane isti.
