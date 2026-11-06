# „Prati moj šablon" samo za lični šablon

**Grana:** `fix/allocation-follow-template`

Iz revizije nauke o treningu (odeljak F), nalaz N1. Odluka vlasnika (5. 11. 2026): izbor samo
za lični šablon; za ugrađeni server uvek balansira, a klijent opciju ne prikazuje.

## Problem

Izbor „Broj serija → Prati moj šablon" ([set-allocation-choice.md](set-allocation-choice.md))
uveden je zbog ličnog šablona: otkucano „3 serije" je namera. Ali čarobnjak ga je nudio uz
svaki šablon. Ugrađeni šablon nosi raspored vežbi, a ne nameru o volumenu, i njegov propis
nije pravljen da poštuje granicu oporavka. Kad se prati doslovno, nedelje prelaze MRV na
svakom nivou: u reviziji je to bilo 68 nedelja-mišića za naprednog, 305 za srednji nivo i 244
za početnika. Tekst uz izbor je pritom govorio samo da volumen „može ostati ispod cilja".

## Šta je urađeno

- `SetAllocationRule.For(requested, isCustomTemplate)` (domen): lični šablon zadržava traženi
  izbor, a ugrađeni uvek dobija balansiranje po ciljnom volumenu.
- `MacrocycleService` primenjuje pravilo na dva mesta:
  - kad se blok pravi, pa se u bazu ne upisuje izbor koji ne važi;
  - kad se blok generiše, jer blokovi upisani pre ovog pravila mogu nositi „Prati moj šablon"
    uz ugrađeni šablon. Blok se generiše tek kad mu dođe red, pa ovo pokriva i takve blokove.
- Čarobnjak plana prikazuje polje „Broj serija" samo uz lični šablon. Prelaz bloka na ugrađeni
  šablon vraća izbor na balansiranje, da ne ostane upisan izbor koji meni više ne prikazuje.
- Uputstvo, u pitanju o ličnom šablonu, to sada kaže.

**Već generisani blokovi se ne prepisuju.** U razvojnoj bazi 10 blokova nosi „Prati moj
šablon" uz ugrađeni šablon. Svi su već generisani, a 3 su aktivna. Pravilo iz runde 12 važi i
ovde: blok koji je već propisan se ne menja ispod vežbača.

## Provera

- `dotnet test`: **860** (bilo 856), testovi za `SetAllocationRule`. Pravilo vraćeno na
  „uvek traženo" obara **1**.
- Klijent: **203** (bilo 201): izbor se nudi samo uz lični šablon, a prelaz na ugrađeni ga
  vraća na balansiranje.
- Servisni deo nema test, jer projekat nema harness na nivou servisa (isto kao u rundi 12).
  Proveren je uživo:
  - zahtev za plan sa Upper/Lower i „Prati moj šablon" upisuje blok sa balansiranjem
    (`setAllocation: 0`), a i generisan mezociklus ima `SetAllocation = 0`;
  - na 375 px, sa ličnim šablonom na nalogu za testiranje: oba bloka sa ličnim šablonom imaju
    polje „Broj serija". Kad se prvi blok prebaci na Upper/Lower posle izbora „Prati moj
    šablon", polje nestaje, a drugi blok ga zadržava. Bez prelivanja.
