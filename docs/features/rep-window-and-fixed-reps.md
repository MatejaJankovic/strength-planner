# Prozor ponavljanja: granica ga pomera, ne sužava

Dva nalaza iz pregleda logike treninga, oba u istom izračunu u `Periodization.ForWeek`.

## Problem

**Faza volumena hipertrofije je bila opseg od dva ponavljanja.** Hipertrofija se propisuje
kao 8–12, a gornja granica ponavljanja je 12 (Epley). Linearni model prvu nedelju pomera za
`+3` ponavljanja, pa je od 11–15 ostajalo **11–12**:

| Nedelja | Bilo | Sada |
|---|---|---|
| 1 | 5 × **11–12**, RIR 2 | 6 × **8–12**, RIR 2 |
| 2 | 5 × **11–12**, RIR 1 | 6 × **8–12**, RIR 1 |
| 3 | 4 × 8–12, RIR 1 | 4 × 8–12, RIR 1 |
| 4 | 4 × 7–11, RIR 1 | 4 × 7–11, RIR 1 |
| 5 | 3 × 6–10, RIR 1 | 3 × 6–10, RIR 1 |
| 6 (deload) | 2 × 8–12, RIR 1 | 2 × 8–12, RIR 3\* |

\* Ciljni RIR deload-a menja **druga** grana (B12), ne ova.

Pomeraj je, pošto gornja granica nije mogla da se digne, **dizao donju** — i to za tri
ponavljanja. Nedelja koja po modelu treba da bude lakša tražila je jedanaest ponavljanja
umesto osam, a dupla progresija je u prozoru od dva ponavljanja praktično nestala: nema po
čemu da se raste pre nego što se doda kilogram. Isto se dešavalo u obrnutom modelu
(nedelja 5 je bila 11–12, nedelja 4 → 9–12).

**Lični šablon sa fiksnim brojem ponavljanja je dobijao opseg.** Provera unosa odbija samo
donju granicu iznad gornje, pa je 5–5 uredno čuvano. Ali je `ForWeek` gornju granicu
odsecao na **najmanje `min + 1`**, tako da je plan dobijao 5–6 (isto 12–12 → 11–12,
8–8 → 8–9). Klasičan 5×5 se nije mogao propisati.

| Uneto 5–5, linearni blok | Bilo | Sada |
|---|---|---|
| Nedelja 1 | 8–9 | 8–8 |
| Nedelja 3 | **5–6** | 5–5 |
| Nedelja 5 | 3–4 | 3–3 |
| Deload | 5–5 | 5–5 |

Deload je bio jedina nedelja koja je pokazivala uneto — on osnovu ne pomera, pa je kroz
`ForWeek` prolazio bez pomeraja i bez odsecanja.

## Pravilo

Dve granice prozora se odsecaju iz **različitih razloga**, i od sada se različito i
ponašaju:

- **Gornja (12) je granica merenja.** Iznad nje Epley procena ne važi, pa serija ne daje
  podatak ni trendu snage, ni rekordima, ni oceni umora. To je razlog da nedelja ne pređe
  granicu — ali nije razlog da joj se prozor sužava. Prozor se zato **spusti** i zadrži
  širinu opsega iz koga je propisan.
- **Donja (3) je trenažna odluka.** Ispod tri ponavljanja blok više nije ono što piše da
  jeste, pa tamo prozor zaista sme da se suzi. Nedelja intenziteta snage i dalje ispada
  3–4, a ostatak intenziteta nosi RIR — to je zapisano u `Periodization` od kada modeli
  postoje i ovaj nalaz to ne dira.

**Širina sme da bude nula.** Fiksan broj ponavljanja je propis, ne nemarno unet opseg.

**Pomeraj koji granica pojede vraća se kao serija.** Ovo je odluka koja nije bila u nalazu,
a bez nje popravka pravi novu grešku: ako prozor samo spadne, obrnuti blok dobija **dve
identične nedelje** — osnovnu (3.) i prelaznu (4.), jer je kod hipertrofije i RIR pomeraj
već na svom podu. Degenerisani prozor od dva ponavljanja je bio jedino po čemu su se te
nedelje razlikovale, dakle sužavanje je bilo način da model *izgleda* kao da koristi
nedelju koju ne koristi.

Faza volumena znači više rada dalje od otkaza. Ponavljanja su prva poluga; kada ih nema,
rad ide u serije:

| Obrnuti model, hipertrofija | Serije po nedelji |
|---|---|
| Bilo | 3, 3, 4, 4, 5 |
| Sada | 3, 3, 4, 5, 6 |

Blok snage (3–6) granicu ne dodiruje, pa tamo bonusa nema i broj serija je nepromenjen
(3, 3, 4, 4, 5). Jedna serija, koliko god ponavljanja bilo pojedeno: serije i ponavljanja
nisu zamenljivi jedno za jedno, a bonus treba da nedelju učini čitljivom, ne da je
preceni.

## Šta je izmereno

- **Prozor**: vraćanje starog odsecanja obara **6 od 510** testova
  (`TheEpleyCap_MovesTheRepWindow_InsteadOfNarrowingIt`,
  `Linear_StartsWithVolumeAndEndsWithIntensity`, i tri slučaja fiksnog broja ponavljanja).
- **Bonus serije**: vraćanje na „pomeraj serija je konstanta oblika" obara **3 od 510** —
  među njima `EveryTrainingWeekOfAPeriodizedBlock_HasItsOwnPrescription`, test koji je
  postojao i pre ove grane. To je merenje koje potvrđuje da bonus nije kozmetika: bez
  njega blok ima nedelju koja ne radi ništa.
- **Oba vraćena zajedno**: **7 od 510**, a ne 9. Razlika su dva testa koja u toj
  kombinaciji prolaze, uključujući onaj o različitim nedeljama — staro sužavanje je bilo
  ono što je nedelje činilo različitim. Dve greške su se međusobno pokrivale.

## Posledica za obrtanje pomeraja

`Periodization.BaseSetsFrom` obrće pomeraj serija (deload polovi **polazni** broj bloka, a
ne ono što nedelja nosi). Pomeraj sada zavisi i od opsega, pa metoda prima
`baseRepRangeMax`:

```csharp
Periodization.BaseSetsFrom(model, weekNumber, weekSets, plan.BaseRepRangeMax)
```

Isti je razlog zbog koga `ExercisePlan` uopšte pamti `BaseRepRangeMin/Max` (šesti krug):
odsecanje se ne može obrnuti iz svog rezultata. `SetsForWeek` je obrisan — nije imao
pozivaoce, a bez opsega bi od sada davao pogrešan broj.

Nije se sve promenilo: dve različite osnove i dalje mogu da daju istu nedelju, samo je za
sudar sada potrebna **ista širina**. Primer u testu je morao da se promeni sa 11–12 / 12–12
(danas daju različite nedelje) na 9–11 / 10–12, gde su obe širine dva i obe iznad granice.
Tvrdnja je ostala, primer nije.

## Prikaz

`repRangeLabel` (u `shared/`) piše `8–12`, ili samo `5` kada je propisan tačan broj. Stoji
u `shared` jer isti broj prikazuju tri ekrana — trening, pregled bloka i spisak ličnih
šablona — a četvrta kopija pravila bi bila mesto na kome se ono razilazi. Napomena uz unos
serije je preformulisana sa „vrh uskog opsega" na „vrh propisanih ponavljanja", jer kod
fiksnog broja opsega nema; samo pravilo je nepromenjeno (korak nosi rezerva).

## Testovi

`FixedRepTargetTests` (7 tvrdnji): fiksan broj kroz sva tri modela, pomeranje kroz linearni
blok bez razvlačenja u opseg, provera unosa, dupla progresija na fiksnom broju (5 na ciljnom
RIR-u → korak, 5 sa poenom manje rezerve → drži), i propisana ponavljanja za sledeći put.

`PeriodizationTests`: `TheEpleyCap_MovesTheRepWindow_InsteadOfNarrowingIt` (hipertrofija
zadrži širinu 4 u svakoj nedelji; snaga sme da suzi, ali samo kada stoji na podu od tri),
`TheThreeRepFloor_StillNarrowsTheWindow`, `ASwallowedRepShift_ComesBackAsASet`, i
`BaseSetsFrom_InvertsEveryTrainingWeek` proširen na oba cilja i na fiksan broj.

Ukupno: 503 → 510 testova na serveru, 136 → 139 na klijentu.
