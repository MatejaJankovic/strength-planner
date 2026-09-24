# Deload rasterećuje i napor, ne samo opterećenje

Nalaz B12 iz pregleda logike treninga.

## Problem

Deload je do sada radio dve od tri stvari: prepolovio serije i spustio opterećenje na 90%
onoga što je vežbač zaista koristio. Treću — koliko blizu otkaza se radi — nije dirao.
Ciljni RIR je ostajao onaj cilja bloka, dakle **1** za hipertrofiju.

Te dve odluke se ne slažu. Sa maksimumom od 130 kg deload pada na 90 kg. Koliko
ponavljanja 90 kg nosi do otkaza? Po Epley-u `30 × (130/90 − 1) ≈ 13`. Da bi na 90 kg
ostalo jedno ponavljanje u rezervi, treba odraditi **dvanaest**. To je gornja granica
opsega i po naporu skoro normalna radna serija — dakle tačno ono što rasterećenje nije.

Obrnuto gledano: pad opterećenja od 10% po Epley-u vredi oko **tri efektivna ponavljanja**,
pa se isti opseg na 90% odrađuje sa približno tri više u rezervi. Propis je tražio jedno.

Da opseg ponavljanja ostaje isti bila je zapisana odluka (menjanje opsega menja i sam
pokret, a ne samo njegovu težinu). Za RIR nije bilo odluke — samo nije bio pomeran.

## Pravilo

Ciljni RIR deload nedelje je **ciljni RIR bloka + 2**, ograničen na pojas u kome RIR nosi
značenje (`MinRir` 1, `MaxRir` 4).

| Cilj bloka | Ciljni RIR | Deload RIR | Kredit u volumenu |
|---|---|---|---|
| Hipertrofija | 1 | **3** | pun (1.0) |
| Snaga | 2 | **4** | pola (0.5) |

Dva, a ne tri, i to namerno **potcenjuje** rezervu: opterećenje deload-a se izvodi iz onoga
što je stvarno podignuto, a ne iz procene, pa nema razloga graditi propis na gornjoj ivici
računa. Uz to nedelja treba da ostane trenažna, a ne da postane zagrevanje.

Hipertrofija staje tačno na `StimulativeVolume.FullCreditRir` (3) — poslednji RIR koji se u
volumen broji ceo. Snaga svesno prelazi u pola kredita: te serije su i prepolovljene i
lakše, i nedelja ne treba da izgleda kao pun stimulus. Deload nedelje se ionako ne uzimaju
u učenje granica volumena (`VolumeLandmarkService` ih izuzima), pa pola kredita ne pomera
MEV/MAV/MRV.

## Šta se nije promenilo

- **Opterećenje.** 90% stvarno korišćenog, po istom pravilu (`NextWeekLoad`, `WorkingLoad`).
- **Opseg ponavljanja.** Vraća se osnovni opseg **tog plana**, dakle i opseg iz ličnog
  šablona ostaje.
- **Serije.** Polovina polaznog broja bloka.
- **Nedelja posle deload-a.** Progresija i dalje polazi od poslednje trenažne nedelje, a ne
  od olakšanih deload serija, pa viši RIR u deload-u ne ulazi u sledeće opterećenje. Provereno
  u kodu: `SessionService` za deload sesiju uzima „resume point" prethodne trenažne nedelje
  i njime pregazi i referencu i progresiju.

## Nađeno usput

`DeloadService` je, kada red mezociklusa nije nađen, pretpostavljao ravan model i **nikakav**
cilj, a onda je pri vraćanju oslobođenog planiranog deload-a čitao
`goal?.TargetRir ?? plan.TargetRir`. Do ove grane je ta rezerva bila bezopasna: deload je
držao RIR cilja, pa je iz plana izlazila ista vrednost. Od ove grane plan deload nedelje
nosi **deload RIR**, pa bi ta rezerva trenažnoj nedelji dala propis dve rezerve lakši od
onoga što blok traži.

Upit za blok je ionako filtriran po korisniku, pa `null` znači „blok nije njegov" — a tada
nema šta da se rasterećuje. Sada se tu vraća `null`, a cilj je u oba pomoćna metoda
obavezan. Jedna pretpostavka manje.

## Šta je izmereno

- Vraćanje starog pravila (`TargetRir: baseTargetRir` u deload grani) obara **5 od 522**
  testova: `EveryModel_DeloadsAtTheRaisedRir` (tri modela),
  `DeloadWeek_KeepsTheGoalRepRange_AndRaisesTheReserve` i
  `Flat_KeepsExactlyTheBehaviourTheSystemHadBeforeModelsExisted` — poslednji je test koji
  čuva ponašanje ravnog bloka i sada beleži da je ovo jedina stvar koja se u njemu
  promenila od kada modeli postoje.
- U živoj aplikaciji: auto-deload izazvan umorom 0.609 daje nedelju sa `RIR cilj 3`
  (hipertrofija) i dalje 2 od 4 serije; blok snage daje `RIR cilj 4`.

## Testovi

`DeloadIntensityTests` (8 tvrdnji): pomeraj po cilju, deload svakog modela, pojas
`[MinRir, MaxRir]` i monotonost, šta viši RIR znači za brojanje volumena, i da oslobođena
nedelja uzima RIR **cilja** a ne deload-a.

Ukupno: 514 → 522 testa na serveru, 139 na klijentu (bez izmena).
