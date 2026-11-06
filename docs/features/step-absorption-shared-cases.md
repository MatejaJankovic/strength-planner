# Pravilo koraka: jedna tabela za server i ekran

**Grana:** `refactor/step-absorption-from-server`

Korak 5a plana posle revizije (6. 11. 2026), stavka U1.

## Problem

Ekran treninga drži kopiju domenskog pravila `StepAbsorption` (`features/workout/step-absorption.ts`).
Iz nje računa cilj ponavljanja na kartici i poruku posle serije, i to za težinu koja se upravo
unosi. Server o koraku odlučuje sam, pa se dve kopije mogu tiho razići: ekran bi tada obećavao
jedan cilj, a progresija bi radila po drugom. Jedino što ih je držalo zajedno bila je tabela od
dvanaest slučajeva, ručno prepisana iz serverskog testa u klijentski.

## Odluka: kopija ostaje, tabela postaje zajednička

Plan je predviđao da server šalje gotov cilj, a da klijent kopiju izbaci. Cilj se, međutim,
računa iz težine koja se kuca, pre nego što je serija sačuvana: kad vežbač uzme drugu bučicu,
menja se i cilj (8 kg na koraku od 2 kg traži 17 ponavljanja, 10 kg traži 15). Server bi zato
morao da odgovara na svako kucanje, ili da uz svaki plan šalje tabelu težina. Oba su skuplja od
rizika koji otklanjaju.

Rizik je bilo tiho razilaženje, i to je ovde rešeno:

- `step-absorption.cases.json` je **3328 slučajeva** generisanih iz serverskog pravila: koraci 1,
  2, 2.5 i 5 kg, težine od jednog do 64 koraka, osam opsega (uključujući 5×5, 11–12 i 10–20) i
  ciljni RIR 1–4. U 540 slučajeva cilj je produžen preko vrha opsega.
- Server (`StepAbsorptionSharedCasesTests`) i klijent (`step-absorption.spec.ts`) proveravaju
  svoje pravilo nad istim fajlom.
- Ako se pravilo promeni na jednoj strani, pada test na toj strani; fajl se tada generiše iznova
  iz servera, i klijent mora da ga prođe.

Testovi klijenta dobijaju `resolveJsonModule` samo u `tsconfig.spec.json`; aplikacija ga ne
koristi.

## Provera

- `dotnet test`: **883** (bilo 882). `npm test`: **205** (bilo 204). Klijentska kopija se slaže
  sa serverom u svih 3328 slučajeva.
- Merenje vraćanjem, jedna izmena po merenju:
  - granica `>=` u serverskom `Absorbs` zamenjena sa `>`: obara **3**, među njima i novi test nad
    zajedničkim fajlom; ranije bi takva izmena na serveru prošla sve klijentske testove;
  - na klijentu delilac pomeren za jedan: obara **9**;
  - na klijentu uslov za uzak opseg `>` zamenjen sa `>=`: obara **2**, zajednički fajl i jedan
    slučaj ručne tabele.
- Ponašanje aplikacije se ne menja, pa provera uživo nema šta da pokaže.
