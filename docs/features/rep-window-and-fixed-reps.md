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

## Nedelja 1 od sada polazi teže

Polazno opterećenje se izvodi iz maksimuma i iz propisa te nedelje, pa širi prozor nosi
veću težinu: za 1RM od 140 kg prva nedelja linearnog bloka daje **105 kg** umesto 97.5.

| Propis nedelje 1 | Efektivna ponavljanja | Težina |
|---|---|---|
| 11–12 @RIR 2 (bilo) | 11 + 2 = 13 | 140 / (1 + 13/30) = 97.67 → **97.5** |
| 8–12 @RIR 2 (sada) | 8 + 2 = 10 | 140 / (1 + 10/30) = **105.0** |

Nije bilo u nalazu, ali je direktna posledica: nedelja koja ne traži jedanaest ponavljanja
nosi veće opterećenje pri istoj snazi. Izmereno u živoj aplikaciji — kartica Bench Press u
nedelji 1 pokazuje 105.0 kg.

## Posledica za obrtanje pomeraja

`Periodization.BaseSetsFrom` obrće pomeraj serija (deload polovi **polazni** broj bloka, a
ne ono što nedelja nosi). Pomeraj sada zavisi i od opsega, pa metoda prima
`baseRepRangeMax`. Od revizije ovo je **rezerva**, a ne glavni put — vidi tačku 1 ispod:

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

## Šta je revizija našla

Iscrpna proba nad **svim** opsezima koje korisnik može da unese (3–12 × 3–12, serije 2–10,
ciljni RIR 1–4, sva tri modela) našla je tri stvari. Dve su popravljene u ovoj grani, treća
je zapisana.

### 1. Obrtanje pomeraja pogrešno čita zatečene redove

Ovo je greška koju je uvela sama ova grana. `PrescribedSets` u bazi je upisala ona verzija
pravila koja je tada radila. Kada obrtanje odbije i bonus koji tada nije postojao, dobije
se osnova koju blok nikada nije imao:

| Blok iz dev baze | Nedelja 1 | Nedelja 3 (osnova) | Izvedeno iz nedelje 1 | Deload |
|---|---|---|---|---|
| stari, linearan | 5 | **4** | 5 − 2 = **3** | 2 umesto 2 (slučajno isto) |
| stari, linearan | 4 | **3** | 4 − 2 = **2** | **1 umesto 2** |
| stari, obrnut (nedelja 5) | 4 | **3** | 4 − 2 = **2** | **1 umesto 2** |
| nov, ova grana | 6 | 4 | 6 − 2 = 4 | 2 (tačno) |

Izmereno nad dev bazom: **440** redova stoji u nedelji koja od sada nosi bonus, **392** njih
je upisala starija verzija pravila, a **168** tih nosi prozor `11-15` — broj ponavljanja koji
ovaj kod ne ume ni da proizvede. Pogođeno je **12 blokova**, a **188** tih redova stoji u
bloku koji je i dalje aktivan — i jedan od njih već ima deload u nedelji 2, što može biti
samo auto-deload, jer planirani uvek stoji poslednji. Dakle tačno ta putanja.

(Ovde je prvo pisalo „5 aktivnih": to je bio broj aktivnih blokova samo u preseku sa
`BaseRepRangeMax = 12`, a ne mera koju je upit dao. Izmeren je broj REDOVA u aktivnim
blokovima.)

Migracija je razmatrana i odbačena: podaci nose anchor-e iz više verzija pravila, a
oslobođena nedelja posle auto-deload-a nosi propis *druge* nedelje, pa nijedan upit ne može
sa sigurnošću da razluči ko je koji red upisao.

Popravka je zato u kodu, i kraća je od migracije: polazni broj serija se **čita** iz nedelje
koja nosi osnovu (`Periodization.BaseWeekNumber` — treća u periodizovanom bloku, prva u
ravnom), a ne izvodi iz pomeraja. Nijedna verzija ovog fajla nikada nije pomerala tu nedelju,
pa je njen `PrescribedSets` osnova i za stare i za nove redove. Izvođenje ostaje samo kao
rezerva, za slučaj da je i sama osnovna nedelja postala deload.

### 2. Obrtanje ne može da razveza ni donju granicu serija

Zatečena greška, ne uvedena ovde, ali je popravka iz tačke 1 i njoj skraćuje domašaj.
Osnova 2 i osnova 3 u nedelji koja skida seriju daju **istu** vrednost (obe dve, jer
`MinSets` secka), pa izvođenje vraća tri u oba slučaja. Lični šablon sa dve serije je zato
dobijao deload od dve serije — dakle nikakvo rasterećenje. Pinovano testom
`RecoveringTheBase_CannotUndoTheMinimumSetClamp`; čitanje osnovne nedelje daje 2 i deload od
jedne serije.

### 3. Jedan par identičnih nedelja ostaje, i to na dnu

Na svim opsezima koje korisnik može da unese ostaje **tačno jedan** par identičnih nedelja:
linearan model, fiksna **3** ponavljanja — nedelje 3 i 4 su obe `4×3-3 @RIR1`. To je isti
sudar koji je granica pravila na vrhu, samo na podu: ponavljanja ne mogu ispod tri, RIR ne
može ispod jedan, pa nedelji prelaza nema čime da se razlikuje. Bonus tu ne pomaže — pomeraj
je nadole, a nadole se ništa ne pojede, nego se odlučeno odseca. Zapisano, ne popravljeno:
fiksne tri ponavljanja su donja ivica onoga što obrazac unosa dozvoljava.

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

`TheBaseWeek_CarriesTheBasePrescriptionItself` (osnovna nedelja po modelu, i da njen propis
zaista jednak osnovi za oba cilja i za fiksan broj) i
`RecoveringTheBase_CannotUndoTheMinimumSetClamp` (granica koju obrtanje ne ume da razveze).

Ukupno: 500 → 514 testova na serveru, 136 → 139 na klijentu. (Ovde je prvo pisalo
„503 → 514": 503 je bio broj usred grane, koji nijedan commit ne nosi. Runda je počela na
500 — ista vrsta greške kao u devetoj rundi, pa isto ispravljena naglas.)
