# Jedan uzrok je punio dva signala

Nalaz C16 iz pregleda logike treninga.

## Pravilo koje je sistem tvrdio o sebi

Ocena umora spaja četiri signala i sopstvena dokumentacija objašnjava zašto nijedan ne sme
sam da pređe prag: najteži nosi 0.35 naspram praga 0.60, pa se **bar dva moraju složiti**.
Svaki je pojedinačno bučan, a nepotreban deload košta nedelju treninga.

Zato se odstupanje RIR-a meri **samo nad dovršenim serijama** — serija do otkaza bi inače
pomerila i prosek RIR-a i udeo otkaza, i „bar dva" bi bilo zadovoljeno jednim događajem.

## Šta je taj safeguard rušilo

**Poseban slučaj za krajnost.** Nedelja bez ijedne dovršene serije čitala je RIR signal kao
**1.0**, najgore moguće očitavanje — a udeo otkaza je iz istog razloga već bio 1.0:

```
0.35 (RIR) + 0.25 (otkazi) = 0.60 = tačno prag
```

Dakle „sve je išlo do otkaza" je samo pokretalo deload, u pravilu koje kaže da to nijedan
signal ne može sam.

**Isti slučaj kao litica.** Izmereno nad `FatigueEvaluator.Score`:

| Nedelja | Ocena | Deload |
|---|---|---|
| 20 serija, sve do otkaza | **0.60** | da |
| 19 do otkaza + **jedna** dovršena na cilju | **0.25** | ne |
| 19 do otkaza + jedna dovršena poen ispod cilja | **0.60** | da |

Jedna serija od dvadeset je pomerala ocenu za 0.35 i sama odlučivala o deload-u.

**I najvažnije: postojeći test je tvrdio tačno ovo pravilo i prolazio.**
`ShouldDeload_IsFalse_WhenOnlyOneSignalIsMaxedOut` ima red `[InlineData(0, 1, 0, 0)]` —
udeo otkaza 1.0, ostalo nula, tvrdnja „nema deload-a". Prolazio je zato što je test
postavljao `allSetsFailed: false` uz udeo 1.0, a **servis tu kombinaciju nikada ne
proizvodi**: `BuildWeeklyFatigueAsync` postavlja zastavicu iz istog brojanja iz kog izvodi
udeo. Test je pokrivao ulaz koji ne postoji, pa je pravilo bilo tvrđeno i prekršeno u isto
vreme. (Isti rod greške kao test od 100 kg iz devete runde.)

## Druga polovina: ista mera, dve definicije

Granice volumena su računale **svoju** verziju prosečnog odstupanja RIR-a — nad **svim**
serijama, uključujući otkaze. Nad istim serijama:

| Iste tri serije (dve na cilju, jedna otkaz 5 ponavljanja ispod dna) | Odstupanje |
|---|---|
| `FatigueEvaluator` (samo dovršene) | **0** |
| `VolumeLandmarkService` (sve serije) | **−2** |

Tamo je to i više značilo: uslov „imao je rezerve" je **I**-uslov nad odstupanjem RIR-a i
udelom otkaza (`dev ≥ +1 && share ≤ 0.125`), pa je jedan otkaz zatvarao **obe** polovine.
Jedan događaj, dva glasa, u testu koji traži dva nezavisna.

## Pravilo

- Nedelja bez dovršenih serija ne nosi RIR dokaz, pa taj signal doprinosi **nulom**.
  Činjenicu „sve je išlo do otkaza" nosi udeo otkaza, i samo on.
- `WeeklyFatigue.AllSetsFailed` je obrisan: bio je drugi glas iz istog uzroka.
- `FatigueEvaluator.AverageRirDeviation` prima **ponder po seriji** i postaje jedina
  definicija signala. Ocena umora pita o nedelji, pa je ponder 1; granice volumena mere
  doprinos mišiću × blizinu otkaza, pa im je ponder to.

## Šta se promenilo za korisnika

Deload i dalje može da se pomeri unapred, ali sada **uvek** treba saglasnost dva signala —
i u krajnjem slučaju. Izmereno u živoj aplikaciji, ista putanja (full-body-2, srednji nivo,
linearan model, svaka serija do otkaza četiri ponavljanja ispod dna), isti kod osim ove
izmene:

| | Nedelja 1 | Deload | Nedelja 2 | Deload |
|---|---|---|---|---|
| Staro čitanje | **0.703** | da, nedelja 2 | — | — |
| Novo čitanje | **0.353** | ne | **0.65** | da, nedelja 3 |

Razlika u prvoj nedelji je **0.35** — tačno udeo koji je punila činjenica o otkazima.
Sistem nije oslepeo: ista nedelja ponovljena daje 0.65, jer tada postoji porediva prethodna
nedelja i član o padu snage progovori. Vežbač koji se dva puta iscrpi dobija deload nedelju
kasnije nego ranije, a nedelja u kojoj je jedini signal „sve do otkaza" ga više ne dobija
sama.

Ono što nedelja sa otkazima **i** drugim signalom daje:

| Nedelja | Ocena |
|---|---|
| Sve do otkaza | 0.25 |
| Sve do otkaza + pad snage 5% | **0.50** |
| Sve do otkaza + volumen na MRV-u | **0.40** |
| Sve do otkaza + oba | **0.65** → deload |

## Šta je izmereno

- Vraćanje starog čitanja obara **4 od 554** testa, među njima i
  `ShouldDeload_IsFalse_WhenOnlyOneSignalIsMaxedOut` — test koji je postojao pre ove grane i
  prolazio samo zbog nedostižnog ulaza.
- Litica je nula: `OneCompletedSet_NoLongerSwingsTheScoreByATerm` meri da razlika između
  dvadeset otkaza i devetnaest otkaza uz jednu dovršenu seriju sada iznosi **tačno 0**
  (udeo otkaza dostiže punu težinu već na 0.5, pa i 0.95 i 1.0 nose isti maksimum).
- Živa aplikacija: 0.703 → 0.353 u prvoj nedelji, 0.65 u drugoj.

## Testovi

`FatigueEvaluatorTests` (24 tvrdnje): novo čitanje krajnjeg slučaja, da drugi signal i
dalje pokreće deload, litica kao nula, ponder koji pomera prosek u odnosu na svoju sumu, i
da ponder ne vraća otkaze u prosek — sa starom računicom ostavljenom u testu kao oracle
(−2 naspram 0).

Ukupno: 550 → 554 testa na serveru, 139 na klijentu (bez izmena).
