# Nivo iskustva određuje program

**Grana:** `feature/experience-level`

Druga izmena izvedena iz priručnika (vidi [analizu](../analiza-prirucnika.md), stavka 4).

## Problem

Priručnik razdvaja tri nivoa vežbača konkretnom tabelom i zaključuje je rečenicom koja
je zapravo cela poenta:

> *"Kao što vidiš, napredni vežbač bi pregoreo od treninga početnika."*

| | Početnik | Srednji | Napredni |
|---|---|---|---|
| Složene vežbe | 2–3 po treningu | 1–2 po treningu | do 3 **nedeljno** |
| Volumen | srednji, visok intenzitet | veći | manji, uz napredne tehnike |
| Deload | *„ne treba da razmišlja o ovome"* | povremeno | obavezno planirati |

**Šta je aplikacija radila:** `Profile.ExperienceLevel` se traži pri registraciji i posle
se **nigde nije čitao**. Početnik i napredni vežbač dobijali su identičan plan — isti
šablon, isti broj vežbi, isti broj serija, iste granice volumena, isti prag za deload.

## Rešenje

Nivo sada određuje četiri stvari.

### 1. Koliko vežbi i kojih (`SessionComposition`)

Šablon nudi pun spisak, uređen tako da složene vežbe idu prve — što priručnik ionako
propisuje (*„Složene vežbe radiš pre izolacionih vežbi, jer su zamornije"*). Taj redosled
je ono što omogućava da jedno pravilo posluži svakom šablonu: uzmi složene vežbe do
dozvoljenog broja, pa popuni ostatak izolacijama.

Rezultat se oštro razlikuje po nivou, što i jeste smisao — početnikov trening je dve-tri
velike vežbe i malo šta drugo, a naprednom je jedan težak pokret okružen ciljanim
izolacijama.

**Kompromis koji vredi imenovati:** dan sastavljen samo od složenih vežbi (čučanj, mrtvo,
leg press) ne može istovremeno da poštuje budžet naprednog vežbača i da ostane trening.
Tada pobeđuje donji prag od tri vežbe — trening od jedne vežbe nije trening. To je jedini
slučaj u kome se budžet prekoračuje, i pokriven je testom.

### 2. Koliko serija po vežbi

Početnik 3 (srednji volumen uz visok intenzitet — napredak je još linearan i dolazi od
učenja pokreta), srednji nivo 4 (volumen je glavna poluga), napredni 3 (manje serija, ali
težih i preciznije biranih).

### 3. Gde počinju granice volumena

Seed vrednosti se množe sa 0.8 / 1.0 / 1.2. Serija početnika je slabiji stimulus — još ne
ume da aktivira najveće motorne jedinice — a oporavak mu je nerazvijen, pa mu ceo pojas
stoji niže. Adaptacija onda kreće od te tačke, a ne od populacionog proseka.

Redosled `MEV < MAV < MRV` se posle skaliranja **obnavlja**, a ne pretpostavlja:
zaokruživanje ume da sruči uzak pojas u istu vrednost, a to bi palo na `CHECK` ograničenju.

**Posledica za zatečene korisnike:** adaptacija ograničava lične granice na ±50% *seed*
vrednosti, a seed je sada skaliran. Korisnik koji je već naučio granice protiv
neskaliranog seed-a i vodi se kao početnik dobiće ih postepeno vraćene u novi pojas — pri
sledećoj adaptaciji, po jednu seriju nedeljno, a ne odjednom. Isto važi i kada korisnik
promeni nivo u profilu: granice ne skaču, nego se dovuku tokom narednih nedelja.

### 4. Da li umor uopšte povlači deload

| Nivo | Prag |
|---|---|
| Početnik | **nema** — ostaje samo planirani deload na kraju bloka |
| Srednji | 0.60 (nepromenjeno ponašanje) |
| Napredni | 0.50 — deload se aktivira ranije |

Početnik nema prag namerno. Priručnik je izričit (*„Početnici ne treba da razmišljaju o
ovome"*), a i sami signali od kojih se ocena gradi kod njega su najmanje pouzdani — RIR
procenjuje loše jer staje na pečenju misleći da je na otkazu. Nepotreban deload ga košta
nedelje napretka.

## Provera

- `dotnet build`, `dotnet test` (141 test, bilo 121), `npm run build` — sve prolazi.
- `ExperienceProgrammingTests` i `SessionCompositionTests` pokrivaju sve četiri poluge,
  uključujući očuvanje redosleda granica pri zaokruživanju uskog pojasa, poštovanje
  budžeta složenih vežbi, i to da **srednji nivo zadržava tačno ponašanje koje je sistem
  imao pre ove izmene** (prag 0.60, seed granice nepromenjene).
- End-to-end, isti šablon i cilj, menja se samo nivo:

  | Nivo | Vežbi | Serija | Vežbe (Upper A) | Chest granice |
  |---|---|---|---|---|
  | Početnik | 5 | 3 | Bench, Row, OHP, Curl, Pushdown | 8 / 13 / 18 |
  | Srednji | 4 | 4 | Bench, Row, Curl, Pushdown | 10 / 16 / 22 |
  | Napredni | 3 | 3 | Bench, Curl, Pushdown | 12 / 19 / 26 |

- End-to-end, ista iscrpljujuća nedelja za sva tri nivoa: ocena umora je kod sva tri
  **0.6**, ali auto-deload dobijaju samo srednji i napredni — početnik ostaje na
  planiranom deload-u, tačno kako priručnik traži.

## Poznato ograničenje — **zatvoreno**

Napredni vežbač je na tadašnjim šablonima dobijao svega tri vežbe, jer su ti šabloni imali
samo dve izolacije po danu. Pravilo je radilo ispravno — nije imalo čime da popuni trening.

Zatvoreno u grani [`feature/more-templates`](more-templates.md): šabloni sada nose dovoljno
izolacionog rada, a katalog je dobio izolacije za grudi i leđa kojih ranije uopšte nije
bilo. Napredni vežbač dobija pet do šest vežbi po treningu, zavisno od frekvencije šablona.

## Ispravke posle revizije koda

Revizija je prekinuta na sredini (dostignut limit sesije), pa sam njen spisak provera
prošao sam. Nađeno i ispravljeno:

- **Biranje vežbi je išlo po vrednosti, ne po indeksu.** `ordered.Where(chosen.Contains)`
  poredi po jednakosti, pa bi dan koji dvaput navodi istu vežbu uzeo obe pojave odjednom i
  probio broj mesta. Sada se bira po indeksu — uz to je i `O(n)` umesto `O(n²)`.
- **`Math.Clamp` je mogao da baci izuzetak.** Donji prag (3) i plafon (5–6) dolaze iz dve
  nezavisne konstante; da neko spusti `ExercisesPerSession` ispod praga, generisanje plana
  bi puklo. Prag se sada poravnava na plafon. Test vrti sve nivoe i dužine dana od 0 do 12.
- Provereno i **ispravno bez izmena**: ocena umora se upisuje *pre* provere praga, pa
  početnikova nedelja ne ostaje večno „neocenjena"; svi novi upiti su ograničeni po
  korisniku; polovljenje serija u deload nedelji prati novi početni broj serija; reset
  granica se vraća na skalirane vrednosti, što je i namera.
