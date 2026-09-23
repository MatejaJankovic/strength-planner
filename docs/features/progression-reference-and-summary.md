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
  važi i za 100 kg, pa je to dokaz i o referentnoj težini;
- lakša serija sa rezervom **ne ulazi** — njen RIR je izmeren na drugoj težini.

`ExcludedLighterSets` pamti koliko ih je izostavljeno, da se to kasnije može prikazati.

Prvo obrazloženje u ovom zapisu je tvrdilo da uključena lakša serija „korekciju može samo da
povuče naniže". Revizija je pokazala da to nije tačno: ako su serije na referentnoj težini
otkazale **ispod** opsega (efektivni RIR −3), lakša serija sa RIR 0 unutar opsega (efektivni
RIR 0) diže prosek, pa i predlog — 90 kg umesto 90 × 0.9. Pravilo ostaje (lakši otkaz jeste
dokaz o težoj seriji), ali tvrdnja o smeru je uklonjena.

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

`UndoDeload` vraća težinu od koje je deload izveden (deli sa 0.90 i zaokružuje **naniže**),
a `ChangeKg` razliku za rezime.

### Izlaz iz deload-a

Kad se završava deload nedelja, `SessionService` pronađe **poslednju odrađenu trenažnu
sesiju istog dana**, na njenim serijama pusti `WorkingLoad` i `ProgressionEngine`, i taj
rezultat prosledi pravilu. Ako takve sesije nema, referenca se vraća iz same deload težine
(`UndoDeload`). Važi i kad je naredna nedelja opet deload — inače bi se 90% primenilo na već
rasterećenu težinu.

### Rezime se zaključuje na kraju

`RefreshSummariesAfterDeload` je zamenjen `FinalizeSummaries`, koji se poziva **posle**
auto-deload-a: predlog se čita iz upisanog plana naredne nedelje (praćena instanca, pa se u
njoj vidi i deload), pa se računa razlika prema `UsedWeightKg`. Kad naredne nedelje u bloku
nema, oznake nema — sledeći blok svoje težine izvodi iz procene maksimuma.

Oznaka se pravi u čistoj funkciji `next-weight-label.ts` (`102.5 kg ↑ +2.5`, `90 kg ↓ −10`,
`100 kg`), sa testom za nulu i za nedostajuću vrednost — dva slučaja koja izraz u šablonu ne
bi pokrio.

## Testovi

`WorkingLoadTests` (7 testova) i `NextWeekLoadTests` (21 slučaj): izbor reference,
uključivanje lakšeg otkaza i lakše serije koja je promašila opseg, sva pet pravila,
`UndoDeload` na sitnom i grubom koraku, `ChangeKg`. Svi brojevi su izračunati ručno i upisani
kao očekivanja, uključujući konverziju kroz implicitni maksimum (80 → 85 i 82.5 → 92.5) i
deload izveden iz tekućeg propisa (65 kg, a ne 70). `WeightMathTests` pokriva novi
`FloorToStep`. Frontend: `next-weight-label.spec.ts` (6).

`dotnet test`: 434 (bilo 401). Frontend: 128 (bilo 122).

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

## Šta je revizija koda našla

- **Dve deload nedelje jedna za drugom su množile 0.9 dva puta.** Kad planirani deload već
  počne, auto-deload ga ne oslobađa, pa blok može da ima dva rasterećenja u nizu. Pravilo za
  nastavak je tada bilo preskočeno (tražilo je da naredna nedelja NIJE deload), pa je druga
  padala na 81%. Sada se nastavak primenjuje kad god je tekuća nedelja deload, pa je i druga
  na 90% zarađene težine.
- **`UndoDeload` nije tačan inverz na grubom koraku.** Deload od 50 kg na koraku od 10 kg
  deljenjem daje 55.6, što se zaokruživalo na **60 kg** — težinu koja nikada nije podignuta.
  Zaokružuje se naniže (`WeightMath.FloorToStep`), pa vraćena referenca može da bude tačna
  ili jedan korak manja, nikad veća.
- **`ApplyDeloadAsync` je izgubio poslednju rezervu.** Kad nema ni serija ni propisa završene
  nedelje, sada se uzima cilj same nedelje koja postaje deload: progresija ga je upisala dok
  ta nedelja još nije bila rasterećenje, pa je to puna težina. Bez te grane bi u tom uglu
  ostala nedirnuta, dakle 100%.
- **Dva imena testa su tvrdila pojam koji domen ne poznaje** („preskočena vežba"); pravilo
  vidi samo referencu bez progresije, pa se tako i zovu.
- Pogrešna reč u komentaru: deload **spušta na 90%**, a polove se serije.

## Poznata ograničenja

- **Referenca je najteža težina, pa ascendentna piramida (80 / 90 / 100) napreduje samo iz
  gornje serije.** To je i namera: `SetLog` u dokumentaciji kaže da se zagrevanje ne upisuje.
- **Razlika se poredi sa onim što je podignuto danas.** Posle deload nedelje to znači veliku
  pozitivnu razliku (`102.5 kg ↑ +12.5` naspram 90 kg iz deload serija) — tačno, ali izgleda
  kao skok. Alternativa bi bila porediti sa zarađenom težinom, što bi značilo dva različita
  „pre" u istom ekranu.
- **Broj izostavljenih lakših serija se nigde ne prikazuje**, iako ga `WorkingLoad` vraća.
- **Nastavak posle deload-a ne gleda serije same deload nedelje.** Ako se deload odradi teže
  od zarađene težine (a to vežbač sme da upiše), taj podatak se ignoriše — nastavlja se od
  poslednje trenažne nedelje.
- **Redosled u `SessionService` nije zaključan testom.** Da `FinalizeSummaries` mora da se
  pozove posle ocene umora piše u komentaru i vidi se u prolazu kroz aplikaciju, ali za
  servise u projektu nema test harness-a, pa bi vraćanje poziva iznad ostalo zeleno.
- Zaokruživanje i dalje može da referencu van mreže koraka pomeri za manje od pola koraka
  (npr. 101 → 102.5 uz pozitivnu korekciju); pravilo iz prve grane garantuje samo da smer
  korekcije ostane isti.
