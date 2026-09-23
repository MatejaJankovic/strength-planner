# Vrh opsega više ne spušta težinu

Grana `fix/load-progression`. Prva od četiri grane iz pregleda logike treninga (odeljak A,
„progresija opterećenja"). Pokriva tri nalaza: A1 (vrh opsega), A2 (serija ispod opsega sa
rezervom) i A8 (savet o RIR-u u uputstvu).

## Problem 1: korekcija je poništavala korak duple progresije

`ProgressionEngine.ComputeNext` je radio ovo:

```
korekcija = clamp((prosečan efektivni RIR - ciljni RIR) × 3%, ±10%)
sledeće   = sve serije na vrhu ? upotrebljeno × (1 + korekcija) + korak
                               : upotrebljeno × (1 + korekcija)
```

Kad svaka serija stigne do vrha opsega, ali sa RIR-om za jedan ispod cilja (tipično: poslednje
ponavljanje izvučeno do otkaza), rezultat je `0.97u + korak`. Da li to diže, drži ili spušta
težinu zavisi samo od toga **koliko je težina velika**:

| Korak | Drži se | Ispod toga raste | Iznad toga pada |
|---|---|---|---|
| 2.5 kg (šipka) | ~42–125 kg | < 42 kg | > 125 kg |
| 2 kg (bučice) | ~33–100 kg | < 33 kg | > 100 kg |
| 5 kg (mašina) | ~83–250 kg | < 83 kg | > 250 kg |

Izmereno pokretanjem stvarnog koda:

| Scenario | Rezultat | Strelica |
|---|---|---|
| Snaga 3–6 @RIR2, 3×6 @RIR1, 140 kg | 137.5 kg | ↑ |
| Hipertrofija 8–12 @RIR1, 3×12 do otkaza, 160 kg | 157.5 kg | ↑ |
| Isto, osam treninga zaredom od 160 kg | 160 → 140 kg | ↑ svaki put |
| Snaga 3×6 @RIR1, osam treninga od 180 kg | 180 → 160 kg | ↑ svaki put |

Kod snage je ovo najjasnije pogrešno: 180 × 6 sa RIR 1 znači e1RM ≈ 222 kg, a iz toga za
3 ponavljanja sa RIR 2 izlazi ≈ 190 kg. Vežbač je uradio više nego što je propis tražio, a
sistem mu je osam nedelja zaredom spuštao težinu.

### Zašto testovi to nisu uhvatili

Runda 1 je već jednom imala istu vrstu greške (`100 → 80 kg` za osam treninga) i posle
ispravke dobila regresioni test
`ComputeNext_DoesNotDriveWeightDown_WhenEveryTopOfRangeSessionEndsInFailure`. Test je počinjao
od **100 kg** — a to je baš u pojasu u kome je `0.97u + 2.5` stabilno. Prolazio je slučajno.
Isto važi i za primer u `failed-reps-logging.md` („100 · 0.97 + 2.5 → 100 kg, zadrži") i za
napomenu na ekranu „Otkaz na vrhu opsega - opterećenje se zadržava, a ne diže".

## Problem 2: serija ispod opsega sa rezervom je dizala težinu

`WorkingSet.EffectiveRir` je broj promašenih ponavljanja pretvarao u negativan RIR samo kad je
serija bila otkaz. Serija ispod dna opsega **sa** rezervom zadržavala je upisani RIR, uz
obrazloženje da je vežbač stao namerno. Posledica: 3×5 sa RIR 2 u opsegu 8–12 (@RIR1) čitala
se kao „RIR 2 naspram cilja 1 — lakše od plana", pa je sledeći trening dobijao **102.5 kg**.
Vežbač koji nije mogao da stigne do opsega dobijao je težu šipku.

## Rešenje

### Vrh opsega

Kad sve serije stignu do vrha, sledeći trening kreće ispočetka od dna opsega. Taj povratak
vredi `(max − min)` ponavljanja rezerve, pa je manjak RIR-a do te veličine već „plaćen".
Negativna korekcija bi ga platila drugi put.

```
ako sve serije na vrhu:
    ako odstupanje + (max - min) >= 0:   sledeće = u × (1 + max(0, korekcija)) + korak
    inače:                               sledeće = u   (drži, tačno podignuto)
```

- Pozitivna korekcija i dalje ide povrh koraka (teza: lakše od plana → teže).
- Uslov nije izmišljen. Po Epley-u je sledeći propis (dno opsega sa ciljnim RIR-om) na
  težini `u × (30 + max + r) / (30 + min + t)`, a to je ≥ `u` tačno kada je
  `(r − t) + (max − min) ≥ 0`. Korak se, dakle, dodaje tačno onda kada Epley kaže da sledeći
  propis ide na istoj ili većoj težini.
- Za ugrađene ciljeve u ravnom bloku (8–12 @RIR1, 3–6 @RIR2) uslov je uvek ispunjen. Ne
  važi samo kad je opseg uži od ciljnog RIR-a — uske nedelje periodizacije (11–12 @RIR2,
  3–4 @RIR3) ili lični šablon sa uskim opsegom — i serije se izvuku preko cilja. Tu se
  težina **drži**: vrh opsega nikad ne spušta opterećenje.

### Ispod dna opsega

Efektivni RIR ispod dna je sada **kapacitet** (ponavljanja + stvarna rezerva) meren prema
dnu opsega, bez obzira na razlog zbog kog je vežbač stao:

```
rezerva     = otkaz ? 0 : RIR
efektivni   = rezerva - max(0, min - ponavljanja)
```

- Na dnu i iznad: upisani RIR, kao i do sada.
- Otkaz ispod dna: tačno pravilo iz runde 1, `-(promašena ponavljanja)`.
- Novo: 5 sa RIR 2 u 8–12 → kapacitet 7 → `-1`, isto kao otkaz na 7. Sledeći trening:
  −6% → **95 kg**, što je i Epley vrednost (94.9 kg).

`ImpliesFailure`, `SetLogService` i upisani `IsFailure` se ne menjaju: serija sa rezervom se i
dalje ne upisuje kao otkaz, pa nema ni migracije.

### `WeightIncreased`

Značilo je „sve serije su stigle do vrha", a rezime je to prikazivao kao strelicu uz težinu
koja je bila ista ili manja. Sada znači ono što piše: predlog je veći od upotrebljene težine.

`SessionService` je morao u istu granu, iako je rezime tema naredne: on posle progresije ume
da **prepiše** predlog — 90% pred planirani deload, ili preračun iz e1RM-a kad naredna nedelja
traži drugi propis — pa je zastavicu računao iz broja koji korisnik na kraju ni ne vidi.
Izmereno pred planiranim deload-om: pre ispravke `144 → 145 kg` uz strelicu naviše, sada bez
nje. Ostatak rezimea (razlika u kilogramima, preskočena vežba, izlaz iz deload-a) ide u
granu `fix/progression-reference-and-summary`.

### Ocena umora čita isti efektivni RIR

Posle pravila kapaciteta ista serija (5 sa RIR 2) je progresiji i učenju granica volumena
„teža od plana", a ocena umora za auto-deload ju je i dalje čitala sirovim RIR-om, kao
„lakšu". Prosek je premešten u `FatigueEvaluator.AverageRirDeviation` i koristi
`WorkingSet.EffectiveRir`. Otkazi su i dalje isključeni, da RIR i udeo otkaza ostanu dva
nezavisna signala.

Usput se menja i `VolumeLandmarkService`, jer čita isti `EffectiveRir`: serija ispod dna sa
rezervom se sada i tamo čita kao teža. To je namerno — jedna definicija.

### Napomene na ekranu

Pravila napomene ispod unosa serije su u čistoj funkciji `set-feedback.ts`, sa sopstvenim
testom, jer je napomena obećanje o sledećem treningu i mora da prati server:

- otkaz na vrhu: „ako sve serije stignu do vrha, sledeći put ide korak više"
- otkaz na vrhu uskog opsega: „ako su sve serije takve, opterećenje se zadržava"
- ispod dna bez rezerve (i bez kvačice): „računa se kao otkaz"
- ispod dna sa rezervom, kad kapacitet ne dostiže propis: „i sa rezervom je to manje nego što
  propis traži"

Napomene govore o **jednoj** seriji, a pravilo odlučuje po proseku cele vežbe, pa svaka
nosi uslov („ako je cela vežba takva", „ako sve serije stignu do vrha") umesto da obeća
broj. Revizija koda je izmerila zašto: u vežbi 5@RIR2 + 12@RIR4 + 12@RIR4 na 100 kg prosečan
efektivni RIR je 2.33 naspram cilja 1, pa server predlaže **105 kg** — iako je prva serija
bila ispod opsega. Prva verzija napomene je za tu seriju tvrdila da veće opterećenje ne
sledi. Granica je i zapisana u `set-feedback.spec.ts`.

Dodat je i slučaj koji nije postojao: vrh **uskog** opsega bez kvačice otkaza (11–12 @RIR2).
Tamo korak zavisi od rezerve, pa napomena to i kaže.

### Uputstvo (A8)

`HowToUseApp.md` je savetovao: „ako nisi siguran, proceni konzervativno (radije reci da je
ostalo više nego manje)". To je obrnuto: veći RIR znači lakše, pa sledeći trening dobija
**veće** opterećenje (+3% po poenu), a serije sa RIR 4–5 ne ulaze ni u stimulativni volumen.
Sada piše da se, kad nisi siguran, prijavi **manji** RIR. Savet „unesi konzervativno" za
1RM na ekranu maksimuma je tačan (manji 1RM = lakši start) i nije diran.

## Testovi

- Promenjene su tačno četiri tvrdnje, i sve četiri su bile posledica greške:
  - red `10@2, 11@2, 11@2` u `ProgressionEngineTests`: `WeightIncreased` `false → true`
    (težina jeste porasla, 100 → 102.5);
  - `EffectiveRir` za `6` sa `RIR 2` ispod dna: `2 → 0`;
  - otkaz na vrhu na 100 kg: `100 → 102.5`;
  - regresioni test od 100 kg je postao Theory preko mreže (100, 160, 300 kg na šipki, 180 kg
    kod snage, 110 kg na koraku 2, 260 kg na koraku 5, 20 kg na koraku 0.5, uska nedelja),
    koja proverava **svaki** trening, ne samo krajnju vrednost.
- `TopOfRangeProgressionTests`: korak na svim težinama i koracima, držanje u uskim nedeljama,
  težina van mreže koraka (101 kg) se ne zaokružuje ispod podignute, pozitivna korekcija
  ostaje povrh koraka.
- `ProgressionPropertyTests` nad mrežom (6 koraka × 164 težine × 5 opsega × ciljni RIR 1–4):
  - vrh opsega nikad ne spušta težinu, a korak ide kad god to uslov kaže;
  - monotonost: više ponavljanja, više RIR-a ili skinuta kvačica otkaza nikad ne daju manji
    predlog;
  - **stara formula kao proročište**: svuda gde izmena nije smela ništa da promeni (sesija
    koja nije na vrhu bez serije ispod dna sa rezervom, i sesija na vrhu bez manjka RIR-a),
    rezultat je identičan starom — preko 100.000 poređenja;
  - `WeightIncreased` odgovara brojevima.
- Ocena umora: četiri testa za `AverageRirDeviation`.
- `set-feedback.spec.ts`: sedam slučajeva napomene.

Provereno da testovi hvataju grešku: privremeno vraćanje stare formule u `WorkingSet` i
`ProgressionEngine` obara **34 testa**, među njima redove od 160, 300, 110 i 260 kg.

`dotnet test`: 401 (bilo 356). Frontend: 122 testa.

## Provereno u živoj aplikaciji

Nov nalog (Srednji nivo), ručni 1RM za Bench Press 208 kg (208 / 1.3 = 160), plan sa jednim
**ravnim** blokom hipertrofije na šablonu Upper/Lower. Ravan blok je namerno: u periodizovanom
bloku sledeća nedelja ima drugi propis, pa težinu preračunava iz e1RM-a i ova grana se ne bi
ni videla.

Upper A, nedelja 1, širina telefona (375 px):

| Vežba | Serije | Rezime | Pre ove grane |
|---|---|---|---|
| Bench Press | 3 × 12 na 160 kg, do otkaza | `Sledeće 162.5 kg ↑` | 157.5 kg ↑ |
| Barbell Row | 3 × 5 na 100 kg, RIR 2 | `Sledeće 95 kg` (bez strelice) | 102.5 kg |
| Triceps Pushdown | 3 × 10 na 40 kg, RIR 3 | `Sledeće 42.5 kg ↑` | 42.5 kg, bez strelice |

Nedelja 2, isti dan, nosi iste tri težine (162.5 / 95 / 42.5). Posle završene treće nedelje
(3 × 12 na 160 kg sa RIR 1) deload nedelja dobija **145 kg** (90% od 160, zaokruženo na korak)
i rezime to prikazuje **bez strelice**; pre ispravke zastavice tu je stajala strelica naviše. Na kartici Bench Press-a, uz
čekiran otkaz na 12 ponavljanja, stoji nova napomena „ako sve serije stignu do vrha, sledeći put
ide korak više"; na kartici Barbell Row-a, sa 5 ponavljanja i RIR 2, napomena da je to manje
nego što propis traži. Konzola bez grešaka.

## Poznata ograničenja

- **Uske nedelje drže, a ne spuštaju.** Po Epley-u bi 3×12 do otkaza u nedelji 11–12 @RIR2
  idealno dalo 97.7 kg; sistem drži 100. Izbor je svestan: vrh opsega nikad ne spušta težinu,
  a sledeća nedelja periodizovanog bloka ionako preračunava težinu iz e1RM-a.
- **Ponavljanja iznad vrha ne donose dodatni kredit.** 3×15 sa RIR 2 u 8–12 i dalje daje
  samo korekciju po RIR-u i korak (60 → 65 kg); računanje viška kao rezerve dalo bi 67.5 kg,
  ali menja ulaz i za učenje volumena. Ostavljeno za kasnije.
- Ostatak odeljka A — radna težina kad su serije na različitim težinama, vežba bez serija,
  strelica u rezimeu posle preračuna, e1RM iz serija sa velikom rezervom i vežbe sa telesnom
  masom — ide u naredne tri grane.
