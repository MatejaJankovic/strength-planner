# Nov maksimum preračunava opterećenja prve nedelje

**Grana:** `feature/recompute-pending-targets`

Četvrti korak plana posle revizije (6. 11. 2026), stavka R6.

## Problem

Pregled #99 je našao da test-serija posle pauze sklanja napomenu o starom maksimumu, ali **ne i
opterećenje**. Generator upisuje cilj samo za prvu nedelju bloka, iz maksimuma poznatog pri
generisanju, i nijedan kasniji zapis ga nije prepisivao. Ostale nedelje nisu imale taj problem:
njih upisuje progresija iz dignutih serija, ili se računaju pri čitanju iz najsvežijeg maksimuma
(runda 12: „predlog se računa pri čitanju, ne upisuje"). Prva nedelja je bila jedini izuzetak.
Ekran je to od #99 i govorio („opterećenja već upisana u plan se ne menjaju"), ali ponašanje nije
bilo ispravljeno.

## Šta je urađeno

- `GeneratedTarget.FollowsTheMaximum(weekNumber, status)` (domen): cilj prati maksimum samo u
  prvoj nedelji i samo u treningu koji još nije započet. Započet trening pripada vežbaču, i
  opterećenje mu se ne pomera ispod ruku.
- `OneRepMaxService` posle svakog ručnog unosa, test-serije i brisanja zapisa preračunava te
  ciljeve za tu vežbu u aktivnom bloku. Koristi isti izbor maksimuma kao generator (najbolji u
  56 dana, prazan prozor pada na najnoviji zapis) i isto pravilo (`StartingLoad`), sa korakom i
  delom telesne mase vežbe. Kad posle brisanja maksimuma više nema, cilj ostaje prazan i računa
  se pri čitanju, kao i za ostale nedelje.
- Tekst je usklađen: ekran maksimuma kaže da nov unos „preračunava opterećenja prve nedelje koja
  još nisu započeta", napomena o starom maksimumu na ekranu treninga ponovo predlaže test-seriju
  (dugme „Uradi test-seriju"), a uputstvo kaže isto.

Uži zahvat je izabran svesno. Prva nedelja je mogla da prestane da se upisuje, pa bi se računala
pri čitanju kao i ostale. Ali upisani cilj prve nedelje čitaju i završetak treninga (vežba bez
ijedne serije prenosi planirano opterećenje u narednu nedelju) i `DeloadService` (rezerva za
deload posle nedelje bez serija), pa bi takva izmena pomerila i ta pravila.

## Provera

- `dotnet test`: **878** (bilo 869): šest slučajeva pravila i četiri testa nad servisom
  (test-serija, ručni unos, brisanje; započet trening i druga nedelja ostaju netaknuti).
- Merenje vraćanjem, uz rebuild:

| Vraćeno | Obara |
|---|---|
| preračun posle test-serije | 1 |
| preračun posle ručnog unosa | 1 |
| preračun posle brisanja | 1 |
| pravilo uvek „da" (i započeti treninzi i kasnije nedelje) | 6 |

- Klijent: 204, bez izmena u testovima.
- Uživo, na 375 px: na planu Full Body mrtvo dizanje na danu B prve nedelje (trening još nije
  započet) stoji na **55 kg**, izvedeno iz maksimuma od 65.1 kg. Posle ručnog unosa od 120 kg na
  ekranu maksimuma isti cilj je **100 kg** (120 / 1.2 za propis 3–6 uz RIR 3), a kartica na
  ekranu treninga ga prikazuje.
