# „Prati moj šablon" samo za lični šablon

**Grana:** `fix/allocation-follow-template`

Problem je zabeležen kao zaseban zadatak u
[experience-volume-consistency.md](experience-volume-consistency.md) (runda 14). Odluka vlasnika
(5. 11. 2026): izbor samo za lični šablon; za ugrađeni server uvek balansira, a klijent opciju
ne prikazuje.

## Problem

Izbor „Broj serija → Prati moj šablon" ([set-allocation-choice.md](set-allocation-choice.md))
uveden je zbog ličnog šablona: otkucano „3 serije" je namera. Ali čarobnjak ga je nudio uz
svaki šablon. Ugrađeni šablon nosi raspored vežbi, a ne nameru o volumenu, i njegov propis
nije pravljen da poštuje granicu oporavka. Kad se prati doslovno, periodizovani blokovi prelaze
MRV na svakom nivou. Izmereno na ovoj grani, preko svih ugrađenih trenažnih nedelja (propis
pre balansiranja naspram MRV-a nivoa, broj nedelja-mišića):

| Model | Napredni | Srednji | Početnik |
|---|---|---|---|
| ravan | 0 | 0 | 0 |
| linearan (rastući, predlog) | 10 | 72 | 48 |
| obrnut | 19 | 90 | 69 |
| linearan (stari) | 38 | 122 | 106 |
| **ukupno** | **67** | **284** | **223** |

U modelima koje čarobnjak nudi to je 29 / 162 / 117. (Beleška iz #92 je navodila 68 / 305 /
244; doprinosi mišića su se od tada menjali, pa je broj izmeren ponovo.) Tekst uz izbor je
pritom govorio samo da volumen „može ostati ispod cilja".

## Šta je urađeno

- `SetAllocationRule.For(requested, isCustomTemplate)` (domen): lični šablon zadržava traženi
  izbor, a ugrađeni uvek dobija balansiranje po ciljnom volumenu.
- `MacrocycleService` primenjuje pravilo na dva mesta:
  - kad se blok pravi, pa se u bazu ne upisuje izbor koji ne važi;
  - kad se blok generiše, jer blokovi upisani pre ovog pravila mogu nositi „Prati moj šablon"
    uz ugrađeni šablon. Blok se generiše tek kad mu dođe red, pa ovo pokriva i takve blokove.
- Čarobnjak plana prikazuje polje „Broj serija" samo uz lični šablon. Prelaz bloka na ugrađeni
  šablon vraća izbor na balansiranje, da ne ostane upisan izbor koji meni više ne prikazuje.
- Pravilo se primenjuje na jednom mestu u servisu (`AllocationFor`), a pri generisanju se
  ispravljen izbor upisuje i nazad na blok, da blok i mezociklus ne kažu dve različite stvari.
- **Tekst uz izbor je ispravljen i za lični šablon.** Ranije je govorio „ostaje tačno onoliko
  serija koliko si uneo", ali raspored i dalje pomera broj serija (linearan: 2 / 3 / 3 / 4 / 4 uz
  unetih 3), a bez balansiranja nema ni granice po treningu ni MRV-a. Sada kaže da serije
  polaze od šablona, menjaju se po rasporedu i mogu ostati ispod cilja ili preći MRV. Isto
  kaže i uputstvo, u pitanju o ličnom šablonu.

**Već generisani blokovi se ne prepisuju.** U razvojnoj bazi 10 blokova nosi „Prati moj
šablon" uz ugrađeni šablon. Svi su već generisani, a 3 su aktivna. Pravilo iz runde 12 važi i
ovde: blok koji je već propisan se ne menja ispod vežbača. Cena je da ta 3 aktivna bloka do
kraja zadržavaju nedelje iznad MRV-a, bez balansiranja.

## Provera

- `dotnet test`: **860** (bilo 856), testovi za `SetAllocationRule`. Pravilo vraćeno na
  „uvek traženo" obara **1**.
- Klijent: **204** (bilo 201): izbor se nudi samo uz lični šablon, polje se u DOM-u ne
  prikazuje uz ugrađeni, a prelaz na ugrađeni vraća izbor na balansiranje. Vraćanje `@if` u
  šablonu obara **1** od 204.
- Servisni deo nema test, jer projekat nema harness na nivou servisa (isto kao u rundi 12).
  Proveren je uživo:
  - zahtev za plan sa Upper/Lower i „Prati moj šablon" upisuje blok sa balansiranjem
    (`setAllocation: 0`), a i generisan mezociklus ima `SetAllocation = 0`. Posle ispravki iz
    pregleda, plan sa dva bloka, ugrađenim i ličnim, oba sa „Prati moj šablon", upisuje 0 i 1;
  - na 375 px, sa ličnim šablonom na nalogu za testiranje: oba bloka sa ličnim šablonom imaju
    polje „Broj serija". Kad se prvi blok prebaci na Upper/Lower posle izbora „Prati moj
    šablon", polje nestaje, a drugi blok ga zadržava. Bez prelivanja.

  *(Kasnije: pokriveno testom nad servisom, vidi [service-harness.md](service-harness.md).)*