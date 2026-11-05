# Ivice procene maksimuma

**Grana:** `fix/estimate-edges`

Iz revizije nauke o treningu (odeljak F), nalazi F13 i F17.

## Problem

- **F13.** Test-serija 100 kg × 1 do otkaza je davala procenu od **103.3 kg**. Epley formula
  služi za ekstrapolaciju iz serije od više ponavljanja. Za jedno ponavljanje do otkaza nema
  šta da se ekstrapolira, a 103.3 kg vežbač upravo nije mogao da digne dvaput. Ekran
  test-serije dozvoljava jedno ponavljanje, pa je to bio slučaj koji je davao pogrešan broj.
- **F17.** Granica od 12 ponavljanja za procenu važi za odrađena ponavljanja, a ne za
  efektivna (ponavljanja + RIR), pa efektivno ide do 15. To je bilo tačno, ali nigde nije
  bilo zapisano kao izbor.

## Rešenje

- `E1RmCalculator.EstimateOneRepMax`: jedno ponavljanje uz RIR 0 vraća samu težinu. Jedno
  ponavljanje uz rezervu i dalje ide kroz Epley, jer kaže da je vežbač imao više.
- `TrainingConstants.EpleyRepCap` dobija obrazloženje, a uputstvo isti pasus:
  - granica od RIR 3 (`E1RmMaxRir`) već drži ekstrapolaciju na najviše tri ponavljanja;
  - granica na efektivnim ponavljanjima bi odbila i seriju od 10 uz RIR 3, najobičniju seriju
    hipertrofije;
  - procena RIR-a je nešto tačnija do 12 ponavljanja do otkaza (Halperin i sar. 2022), a
    ekstrapolaciju kratkom drži upravo granica od RIR 3.
- `StrengthChange` (signal snage za granice volumena i ocenu umora) poredi procene preko
  `ImpliedOneRepMax`, jer je promena odnos dve tačke na krivoj. Inače bi par 100 × 2 pa 100 × 1
  čitao pad od 6.25% umesto oko 3.1%. Na to je ukazao pregled PR-a.

## Provera

- Razvojna baza: nijedna od 8.073 serije nema 1 ponavljanje, pa se nijedna sačuvana procena ne
  pomera. Ranija test-serija 100 × 1 je sačuvana kao 103.3, a zapis maksimuma ne čuva
  ponavljanja, pa se takvi zapisi ne mogu naknadno prepoznati i ispraviti. Njih zameni sledeća
  test-serija ili prozor od 56 dana.
- `dotnet test`: **849** (bilo 843). Novi testovi:
  - 100 × 1 uz RIR 0 je 100;
  - 1 × RIR 1, 1 × RIR 2 i 2 × RIR 0 i dalje računaju Epley;
  - jedno ponavljanje do otkaza nikad ne čita više od istog ponavljanja uz rezervu;
  - par 100 × 2 pa 100 × 1 u `StrengthChange` čita oko −3.1%, ne −6.25%.
- Stari test koji je zakucavao 100 × 1 = 103.33 sada proverava formulu na pet ponavljanja.
- Merenje vraćanjem (commit, pravilo uklonjeno, rebuild): obara **1** od 848 (pre ispravke iz pregleda).
- Uživo:
  - API: 100 kg × 1 uz RIR 0 → 100 kg; × 1 uz RIR 1 → 106.7 kg; × 3 uz RIR 0 → 110 kg.
  - Ekran maksimuma (375 px, bez prelivanja): test-serija 100 × 1 uz RIR 0 je poslata sa
    ekrana, a server je vratio 100 kg. Na ekranu je ostalo 110, jer je test-serija procena i
    takmiči se sa ostalim procenama u prozoru od 56 dana, gde je 110 najbolja.
