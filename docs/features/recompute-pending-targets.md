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

- `GeneratedTarget.FollowsTheMaximum(weekNumber, status, hasLoggedSets)` (domen): cilj prati
  maksimum samo u prvoj nedelji i samo u treningu koji vežbač nije dirnuo, a to je ista
  definicija koju koristi `WeeklySetPlanner`: planiran ili preskočen, bez ijedne upisane serije.
  Preskočen trening se preračunava zato da se po vraćanju na plan ne vrati staro opterećenje.
  Započet trening pripada vežbaču, i opterećenje mu se ne pomera ispod ruku.
- `OneRepMaxService` posle svakog ručnog unosa, test-serije i brisanja zapisa preračunava te
  ciljeve za tu vežbu u aktivnom bloku, u istoj transakciji sa samim zapisom. Upis ponavlja
  uslov (planiran ili preskočen, bez serija), pa trening započet između čitanja i upisa ne dobija
  novo opterećenje. Generator i preračun čitaju isti broj nedelje (`GeneratedTarget.StoredWeek`). Koristi isti izbor maksimuma kao generator (najbolji u
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

## Posle pregleda

Pregled je potvrdio da preračun daje tačno ono što bi generator upisao (isti izbor maksimuma,
korak sa podešavanjem, deo telesne mase, isti propis) i da nijedan drugi deo koda ne upisuje cilj
prve nedelje. Našao je i ovo, sve ispravljeno:

- Napomena na ekranu treninga je govorila da test-serija preračunava „opterećenja koja još nisu
  odrađena", a započet trening se ne preračunava. Sada: „treninga koji još nisu započeti".
- Zapis i preračun nisu bili u istoj transakciji: pad preračuna bi vratio grešku za već upisan
  zapis, a ponovni pokušaj bi ga udvostručio.
- Preskočen pa vraćen trening je zadržavao staro opterećenje.
- „Nije započet" je gledalo samo status, a API prima seriju i na planiranom treningu.
- **Testovi su prolazili i za pogrešnu implementaciju.** Probom je pregled zamenio korak i deo
  telesne mase podrazumevanim vrednostima i izbacio filtere vlasnika i aktivnog bloka, a svih
  878 testova je ostalo zeleno. Očekivane vrednosti su sami unosili korak od 2.5 kg, isti kao
  podrazumevani: isto pravilo iz runde 9 o testu koji unosi konstantu koju aplikacija izvodi.
  Novi testovi koriste korak od 5 kg i sklek sa delom telesne mase od 51.2 kg.

Napomena „Kasnije nedelje opterećenje dobijaju kad se isti dan prethodne nedelje završi" je i u
uputstvu preciznija nego pre: u periodizovanom bloku taj upis dolazi iz maksimuma poznatog u tom
trenutku, ne iz dignutih serija.

## Provera

- `dotnet test`: **882** (bilo 869): sedam slučajeva pravila i šest testova nad servisom
  (test-serija, ručni unos, podešen korak, vežba sa telesnom masom, neaktivan blok, brisanje).
- Merenje vraćanjem, uz rebuild:

| Vraćeno | Obara |
|---|---|
| preračun posle test-serije | 1 |
| preračun posle ručnog unosa | 1 |
| preračun posle brisanja | 1 |
| pravilo uvek „da" (i započeti treninzi i kasnije nedelje) | 6 (na prvoj verziji) |
| preskočen trening isključen iz pravila | 2 |
| deo telesne mase zanemaren (0) | 1 |
| podrazumevan korak umesto koraka vežbe | 1 |
| bez filtera za aktivan blok | 1 |

- Klijent: 204, bez izmena u testovima.
- Uživo, na 375 px: na planu Full Body mrtvo dizanje na danu B prve nedelje (trening još nije
  započet) stoji na **55 kg**, izvedeno iz maksimuma od 65.1 kg. Posle ručnog unosa od 120 kg na
  ekranu maksimuma isti cilj je **100 kg** (120 / 1.2 za propis 3–6 uz RIR 3), a kartica na
  ekranu treninga ga prikazuje.
