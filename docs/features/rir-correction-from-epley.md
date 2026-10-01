# Korekcija po RIR-u čita Epley krivu

**Grana:** `fix/landmark-signal-noise-cda1md`. Ime je dodelilo okruženje sesije; po
konvenciji bi bila `fix/rir-correction-from-epley`.

Prva grana petnaestog kruga, nalaz G1 iz revizije posle runde 14.

## Problem

`ProgressionEngine` je sledeće opterećenje korigovao za `(prosečan RIR − ciljni RIR) × 3%`,
ograničeno na ±10%. Stopa je bila ista za trojku i za seriju od dvadeset ponavljanja.

Po Epley-u jedno ponavljanje rezerve vredi `1 / (30 + efektivna ponavljanja)` opterećenja,
gde su efektivna ponavljanja ponavljanja plus rezerva:

| Propis | Jedan RIR po Epley-u (sredina opsega) | Stara stopa |
|---|---|---|
| snaga 3–6, RIR 2 | 2.7% | 3% |
| hipertrofija 8–12, RIR 1 | 2.4% | 3% |
| izolacija 10–20, RIR 1 | 2.2% | 3% |

Od #91 izolacije idu 10–20, pa je laka serija izolacije dobijala oko 40% veću korekciju nego
što njena ponavljanja opravdavaju. Sledeća sesija je bila preteška, korekcija je išla naniže,
pa opet naviše.

Pravilo je bilo i u neskladu sa samim kodom. `TrainingConstants.EpleyRepDivisor` tvrdi: „Every
rule that trades reps for load reads it from here". Procena maksimuma, radna težina i upijanje
koraka ga čitaju, a korekcija po RIR-u ga nije čitala.

Priručnik kaže da je jedno ponavljanje „otprilike 2–3%" opterećenja. Stara stopa je bila
gornja ivica tog raspona, a nova ostaje u njemu.

## Rešenje

**Stopa je Epley-eva na sredini propisanog opsega:**
`(prosečan RIR − ciljni RIR) / (30 + sredina opsega + ciljni RIR)`, i dalje ograničeno na ±10%
(`ProgressionEngine.CorrectionPerRirPoint`). `RpeCorrectionPerPoint` je obrisan.

Izvod: vežbač koji je uradio r ponavljanja sa rezervom e na težini w ima e1RM
`w × (30 + r + e) / 30`. Za istih r ponavljanja sa ciljnom rezervom t treba
`w × (30 + r + e) / (30 + r + t)`. Korekcija je onda `(e − t) / (30 + r + t)`, dakle i dalje
linearna po odstupanju.

**Zašto sredina opsega, a ne ponavljanja same sesije.** Sa ponavljanjima sesije pravilo je
tačno za tu sesiju, ali ruši svojstvo iz runde 9 da više ponavljanja nikad ne daje lakši
predlog. 10 ponavljanja sa RIR 3 je dobijalo +4.9%, a 9 sa RIR 3 +5.0%. Po Epley-u je i to
tačno, jer će jači vežbač i sledeći put raditi više ponavljanja, ali razlika od 0.1% nije
vredna invarijante koju vežbač vidi. `ComputeNext_IsMonotoneInRepsRirAndTheFailureFlag` je to
uhvatio pri prvoj verziji. Sesija koja ne stigne do vrha stoji negde od dna do jednog
ponavljanja ispod vrha, a sredina prepolovi najveću grešku bilo kog kraja.

**Korak naniže kad zaokruživanje obriše korekciju sada zavisi od ponavljanja.** Taj korak
(#85) davao se kad korekcija udari u −10%, što je uz 3% značilo odstupanje od 3.33 RIR-a. Uz
Epley stopu granica od −10% stoji na 3.5–4.9 RIR-a, zavisno od propisa. Baš slučaj zbog koga je pravilo uvedeno
(bučica od 10 kg, 5/4/4 sa RIR 1 u 8–12, odstupanje −3.67) više nije stizao do granice, pa bi
se vratio na držanje 10 kg. Odluka je o sesiji, pa je sada izražena u ponavljanjima: korak
naniže dolazi kad je sesija **ispod dna opsega** i bar **tri ponavljanja** teža od ciljnog RIR-a
(`TrainingConstants.StepDownRirShortfall`). Otkaz na 7, jedno ispod dna (−2), i dalje drži
težinu, kao što je #85 odlučio. Tri je za trećinu ponavljanja blaže od starih 3.33: prvi slučaj
koji to dodaje je otkaz dva ispod dna (6 u 8–12), koji je ranije držao težinu, a sada spušta
korak.

## Šta se menja za vežbača

Izmereno uživo (srednji nivo, Upper/Lower, hipertrofija, ravan blok), ista sesija na kodu pre i
posle izmene, predlog za istu vežbu u 2. nedelji:

| Vežba | Odrađeno | Pre | Posle | Epley za ista ponavljanja uz cilj |
|---|---|---|---|---|
| Bench press (8–12) | 100 kg, 4 × 10 sa RIR 4 | **110 kg** | **107.5 kg** | 107.3 kg |
| Triceps pushdown, sajla (10–20) | 50 kg, 4 × 15 sa RIR 4 | **55 kg** | **52.5 kg** | 53.3 kg |
| Lateral raise, bučica (10–20) | 10 kg, 4 × 7 sa RIR 1 | **10 kg** | **8 kg** | — |

Prva dva reda su sama stopa: stari predlog je bio iznad onoga što Epley daje za ista
ponavljanja, a novi je najbliža težina na koraku. Treći red je okidač za korak naniže:
odstupanje je tačno −3. Stara korekcija od −9% nije stigla do granice, pa je težina ostala na
10 kg, ispod dna opsega.

Naviše granicu od +10% dostižu samo nedelje sa malo ponavljanja i ciljnim RIR-om 1: RIR na
ekranu ide do 5, pa je najveće odstupanje četiri poena, što je u 8–12 oko 9.8%, a u 6–10 10.3% i
u 3–5 11.4%. (Prva verzija ove beleške je tvrdila da se granica naviše više ne dostiže uopšte.
To važi samo za 8–12, i pregled je to uhvatio.) Granica je zaštita, a ne cilj.

## Provera

- `dotnet test`: **828** (bilo 814), posle pregleda **838**; `npm run build` bez izmena na
  klijentu.
- Merenje pre bilo kakve izmene testova. Prva verzija (stopa po ponavljanjima sesije) je
  oborila **12** od 814, među njima dva svojstva: monotonost i „vrh opsega nikad ne daje manje
  od iste serije jedno ponavljanje ispod". Zbog njih je stopa vezana za sredinu opsega.
  Konačna verzija obara **10** od 814, i svaki pad je pregledan:
  - pet su aritmetički primeri vezani za 3% (npr. zgib sa +10 kg: 16 → 15 kg na pojasu), i
    preračunati su;
  - četiri su redovi slučaja iz #85 iznad, i zbog njih je okidač izražen u ponavljanjima;
  - jedan je proročište stare formule. Ono sada dobija današnju stopu kao parametar, jer je
    stopa namerno promenjena svuda gde ima korekcije, a sve ostalo u starom pravilu i dalje
    mora da važi.
- **Test koji je prolazio slučajno.** `ComputeNext_ReachesSameCapDownwardAsUpward` je posle
  izmene i dalje bio zelen: 8 ponavljanja sa RIR 5 daje +9.76%, a 109.76 kg se na koraku od
  2.5 kg zaokruži na 110, tačno na „plafon" koji je test tvrdio. Prepravljen je u
  `ComputeNext_CorrectsAsFarDownwardAsUpward_AndCapsTheFall`, sa korakom od 0.25 kg: +4 i −4
  poena daju 109.75 i 90.25, a −6 poena staje na 90.
- Novi testovi (`EpleyCorrectionTests`): stopa po propisu; na sredini opsega sledeća težina
  tačno čuva procenu maksimuma; isto odstupanje pomera dužu seriju manje; slučaj sajle od 50 kg;
  granica za korak naniže od tri ponavljanja.
- Merenje vraćanjem, na commit-ovanom stanju, sa rebuild-om posle vraćanja:
  - samo stopa nazad na 3%: obara **17** od 828;
  - samo okidač nazad na granicu od −10%: obara **5** od 828.
- Uživo u oba smera, iz zasebnog worktree-a za stari kod, sa restartom API-ja između. Tabela
  iznad, i snimak ekrana sledećeg treninga u pregledaču.

## Posle pregleda

Pregled (agent, radio na kopiji repozitorijuma; radno stablo posle njega čisto) nije našao ništa
što blokira, a ponovio je sva tri merenja vraćanjem. Dva nalaza su ispravljena:

- **Korak naniže je okidao i unutar opsega.** Sama granica od tri poena se dostiže u opsegu kad
  je ciljna rezerva velika: izolacija 10–20 sa RIR 3 (rane nedelje bloka snage), 15 ponavljanja
  bez rezerve, je tri poena teža od cilja, i bučica od 10 kg je išla na 8, tamo prebacila vrh
  opsega i vratila se na 10. Pravilo iz #85 je za vežbača koji ne može da dosegne opseg, pa sada
  traži i da je sesija ispod dna, računajući rezervu. Proročište stare formule je ovaj slučaj
  sakrivalo, jer je izuzimalo isto što i nova granica. Sada izuzima tačno ono što motor radi, i
  vraćanje novog uslova obara **3** od 838, među njima i proročište. Uživo, blok snage
  (Upper/Lower, linearan po priručniku, 1. nedelja: izolacije 10–20 sa RIR 3), bočno podizanje
  10 kg × 15 × 3 bez rezerve: pre ispravke rezime i 2. nedelja kažu **8 kg**, posle ispravke
  **10 kg**.
- **„Granica naviše se ne dostiže" nije bilo tačno** za nedelje sa malo ponavljanja (vidi gore).
  Ispravljeno u uputstvu i ovde. Poruka commit-a sa dokumentacijom i dalje to tvrdi.

Sitnice koje su ispravljene: raspon granice (3.5–4.9, a ne 3.7–4.7), dva komentara koja su i
dalje tvrdila „isti plafon od 10% u oba smera", i opis granice od tri kao blažeg pravila. Test
`AtTheMiddleOfTheRange_TheNextLoadKeepsTheEstimatedMax` ne razlikuje sredinu opsega od
ponavljanja sesije. To sada piše uz njega, a razliku drže monotonost i
`TheSameDeviation_MovesALongSetLessThanAShortOne`.

## Ograničenja

- Epley preko 12 ponavljanja nije pouzdan kao procena maksimuma, i to ostaje tako. Ovde se
  koristi samo kao lokalni odnos ponavljanja i težine, kao i za upijanje koraka u 10–20 (#91).
- Stopa je vezana za sredinu opsega, pa je za sesiju na dnu opsega nešto manja nego što bi
  Epley dao za tu sesiju (8–12: 2.4% naspram 2.6%), a na vrhu nešto veća.
- `docs/features/bodyweight-load.md` i ostale beleške ranijih rundi navode brojeve po
  staroj stopi. Ostavljene su kakve jesu, jer opisuju šta je tada izmereno.
