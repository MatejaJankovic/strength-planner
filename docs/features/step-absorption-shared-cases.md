# Pravilo koraka: jedna tabela za server i ekran

**Grana:** `refactor/step-absorption-from-server`

Korak 5a plana posle revizije (6. 11. 2026), stavka U1.

## Problem

Ekran treninga drži kopiju domenskog pravila `StepAbsorption` (`features/workout/step-absorption.ts`).
Iz nje računa cilj ponavljanja na kartici („Ponavljanja · cilj N") i poruku ispod unosa serije, i to
za težinu koja se upravo unosi. Server o koraku odlučuje sam, u `ProgressionEngine`. Jedino što je
dve kopije držalo zajedno bila je tabela od dvanaest slučajeva, ručno prepisana iz serverskog testa
u klijentski.

## Odluka: kopija ostaje

Plan je predviđao da server šalje gotov cilj. Cilj se, međutim, računa iz težine na kartici pre nego
što je serija sačuvana, a poruka ispod unosa iz ponavljanja i rezerve koje vežbač upravo bira.
Server bi zato morao da odgovara na svaku izmenu, ili da uz svaki plan šalje tablicu „kapacitet
potreban za korak" po težini. Tablica bi bila mala: produžen cilj postoji samo ispod težine na
kojoj korak staje u propis, za 8–12 uz RIR 1 to je oko sedam težina bučice. Ali bi i dalje morala
da pokrije poruku ispod unosa za proizvoljna ponavljanja i rezervu, pa bi i ona bila druga kopija
istog pravila. Kopija na klijentu je zato zadržana, a ono što ju je činilo opasnom, tiho
razilaženje, rešeno je zajedničkom tabelom.

## Šta je pregled našao: kartica se nije slagala sa progresijom

Prva verzija ove grane je klijentsku kopiju proverila naspram serverskog `RepsToEarnStep`. Pregled
je pokazao da `RepsToEarnStep` **nema nijedan poziv u produkciji**, a da progresija pita nešto
drugo. Ona korak meri putem do sledeće težine na stalku (`WeightMath.StepAbove`), a ne punim
korakom. Na težini koja nije na mreži koraka put je kraći, pa je kartica tražila previše:

- 9 kg na koraku od 2 kg, 8–12 uz RIR 1: kartica **16**, a progresija diže na 10 kg već posle
  3 × 12;
- 5 kg na koraku od 2 kg, 10–20 uz RIR 1: kartica **25**, a progresija diže na 6 kg posle 3 × 20.
  Isti broj je stajao u testu i u uputstvu, kao da je sledeća težina 7.

Obe kopije sada računaju kao progresija: da li korak staje u propis sudi se po punom koraku, a da li
ga serija upija po putu do sledeće težine. Novi test `TheTargetOnTheCard_IsWhereProgressionTakesTheStep`
proverava baš to, nad samim `ProgressionEngine`. Na mreži koraka bez dela telesne mase put je ceo
korak, pa se tamo ništa ne menja. Primer u uputstvu je sada 6 kg (33%, posle 23 ponavljanja).

Koliko je to vidljivo, izmereno je uživo. Dugmad −/+ drže težinu na mreži koraka (sa 42.5 kg na
koraku od 5 kg prvi klik daje 40). Težina van mreže zato stiže na karticu samo kao predlog, kad je
korak vežbe promenjen posle upisanog opterećenja. Retko je, ali tada je kartica tražila pogrešan
broj.

## Zajednička tabela

`step-absorption.cases.json` se generiše iz serverskog pravila i ima tri tabele:

| Tabela | Slučajeva | Šta pokriva |
|---|---|---|
| cilj ponavljanja | 3930 | koraci 1.25–5 kg, težine na mreži i između koraka, deo telesne mase 0 i 51.2 kg, pet opsega, RIR 1–3, i tri težine na kojima klijent pogađa server samo uz toleranciju `EPSILON` |
| upijanje koraka | 1080 | ukupno opterećenje na dve decimale (deo telesne mase 51.2 i 68.37 kg), put do sledeće težine, rezerva 0–8 |
| sledeća težina na stalku | 56 | težine van mreže, koraci 0.5–10 kg |

Server (`StepAbsorptionSharedCasesTests`) i klijent (`step-absorption.spec.ts`) proveravaju svoje
pravilo nad istim fajlom. Kad se pravilo promeni na serveru, pada serverski test, i fajl se
generiše iznova:

```bash
STEP_CASES_REGENERATE=1 dotnet test --filter Regenerate
```

Zatim i klijent mora da ga prođe. Fajl se kopira uz serverski test (csproj), pa se ne traži
penjanjem po folderima: worktree bez njega bi tako tiho pročitao kopiju iz glavnog repozitorijuma.

## Provera

- `dotnet test`: **885** (bilo 882). `npm test`: **209** (bilo 204).
- Merenje vraćanjem, jedna izmena po merenju:

| Vraćeno | Obara |
|---|---|
| server: cilj meri pun korak umesto puta do sledeće težine | 3 (uključujući test nad progresijom) |
| klijent: isto | 2 |
| `set-feedback.ts` meri pun korak | 1 (pre ove grane 0) |
| server: rezerva u `Absorbs` ograničena na 4 | 1 (pre ove grane 0) |
| klijent: bez tolerancije `EPSILON` | 1 (pre ove grane 0) |
| server: granica `>=` u `Absorbs` zamenjena sa `>` | 3 (na prvoj verziji grane) |

- Uživo, na 375 px: kartica Barbell Curl na 42.5 kg posle promene koraka na 5 kg pokazuje cilj 20
  (korak tu staje u propis, pa se staro i novo pravilo slažu). Dugmad −/+ vraćaju težinu na mrežu.
  Korak je posle provere vraćen na podrazumevani.
