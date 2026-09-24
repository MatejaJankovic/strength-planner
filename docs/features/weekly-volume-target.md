# Cilj volumena prati nedelju, a blok snage cilja niže

Nalazi B9 i B10 iz pregleda logike treninga.

## Problem

Balansiranje serija je svaku nedelju gađalo u **MAV**. MAV je jedna vrednost po mišiću, pa
je i cilj bio isti u svakoj nedelji bloka — a periodizacija upravo količinom rada razlikuje
fazu volumena od faze intenziteta. Rezultat: model je menjao ponavljanja i rezervu, a rad je
ostavljao na istom broju.

Izmereno na linearnom hipertrofijskom bloku, grudi:

| Nedelja | Propisano | Predloženo (staro pravilo) |
|---|---|---|
| 1 | 24 | **16** |
| 2 | 24 | **16** |
| 3 | 16 | 16 |
| 4 | 16 | 16 |
| 5 | 12 | **16** |

Talas je postojao u propisu i nije postojao u predlogu. U aplikaciji se to vidi i kao
poruka: *„Predlog serija je spušten sa 6 na 4, da bi nedelja ostala u ciljnoj zoni
volumena"* u nedelji volumena, i *„podignut sa 3 na 5"* u nedelji intenziteta — sistem je
uredno objašnjavao da ravna sopstveni plan.

Uz to, **granice volumena su hipertrofijske granice**. MAV je definisan kao volumen koji
pokreće rast; blok snage radi manje, teže serije koje skuplje koštaju oporavak, a
`WeeklySetPlanner` nije čitao cilj bloka. Blok snage je gađao broj koji ne pripada njemu.

## Pravilo

Dve korekcije, i obe su množenje istog broja.

**Nedelja.** Cilj se pomera onoliko koliko je periodizacija pomerila propis: odnos
propisanog volumena te nedelje i propisanog volumena **osnovne** nedelje. Osnovna nedelja se
čita, a ne izvodi — nijedna verzija `Periodization` je nikada nije pomerala, i isti razlog
je zbog koga je deload prešao na čitanje (vidi
[`rep-window-and-fixed-reps.md`](rep-window-and-fixed-reps.md)). Ravan blok daje odnos jedan,
dakle **tačno MAV**, pa se zatečeni ravni blokovi ne menjaju.

**Cilj bloka.** Hipertrofija gađa MAV; snaga gađa **na pola puta između MEV-a i MAV-a**. Za
grudi to je 13 umesto 16. Izvedeno iz granica koje korisnik ionako uči, a ne novim
množiocem — i pomera se sa njima kada se nauče.

Rezultat ostaje unutar pojasa: nikad ispod MEV-a, jer planirana nedelja treba najmanje da
održava, i nikad iznad MRV-a, jer je to mesto gde oporavak prestaje.

## Šta je izmereno

Ista dva bloka (Upper/Lower, linearan model, srednji nivo), grudi, posle izmene:

| Nedelja | Hipertrofija: cilj | propisano → predloženo | Snaga: cilj | propisano → predloženo |
|---|---|---|---|---|
| 1 | **22** (MRV) | 24 → **22** | 17.9 | 22 → **18** |
| 2 | 22 | 24 → 22 | 17.9 | 22 → 18 |
| 3 (osnova) | **16** (MAV) | 16 → 16 | **13** | 16 → **13** |
| 4 | 16 | 16 → 16 | 13 | 16 → 13 |
| 5 | **12** | 12 → 12 | **10** (MEV) | 12 → **10** |
| 6 (deload) | — | 8 → 8 | — | 8 → 8 |

Talas preživljava, blok snage stoji niže od hipertrofijskog u svakoj nedelji, a deload se ne
dira (balansiranje ga preskače — prepolovljene serije su smisao te nedelje).

Dve granice su se aktivirale i to je namerno vidljivo u brojevima:

- **Nedelja volumena hipertrofije gađa MRV** (22 naspram propisanih 24). Nedelja najvišeg
  volumena u bloku koji se završava deload-om i treba da stoji na ivici oporavka; MRV je
  definisan kao mesto iznad koga oporavak prestaje, pa je to plafon, a ne cilj po sebi.
- **Nedelja intenziteta snage pada na MEV** (10 naspram izračunatih 9.75). Planirana nedelja
  ispod minimalnog efektivnog volumena ne bi ni održavala.

Test `TheWaveSurvivesBalancing_WhereItUsedToBeFlattened` drži i staro i novo ponašanje u
istom slučaju: sa pomerljivim ciljem daje `22, 22, 16, 16, 12`, a sa fiksnim MAV-om
`16, 16, 16, 16, 16`.

## Ekran „Nedeljni volumen"

Ekran je kao „cilj" prikazivao MAV i kada plan nije gađao MAV. Sada prikazuje cilj **te**
nedelje, sa naučenom granicom u zagradi:

```
MEV 10 · cilj ove nedelje 22 (MAV 16) · MRV 22
```

Marker cilja na traci stoji tamo gde je cilj nedelje, a ne na MAV-u. U deload nedelji piše
`MAV 16 (deload - cilj se ne gađa)`, jer tamo cilja nema — manji volumen je namera.

`WeeklyVolumeTargetResolver` postoji kao **jedna** klasa upravo zato: balansiranje gađa te
brojeve i ekran ih prikazuje, a to je isti oblik greške kao `SetLogDto` iz devetog kruga —
vrednost građena na dva mesta, gde drugo zaostane za prvim.

## Posledica za učenje granica

`VolumeAdaptation` sudi o granici samo kada je nedelja bila blizu nje (MAV se pomera kada je
volumen ≥ 0.9 × MAV, MEV kada je ≤ MEV). Kod bloka snage nedelje sada sede niže, pa MAV iz
njih uglavnom ne uči — a nedelja intenziteta, koja stoji na MEV-u, uči o MEV-u. To je
promena u tome **koja** nedelja o čemu govori, i zapisana je ovde jer nije očigledna iz koda.
Nije nalog za izmenu `VolumeAdaptation`: signal za granice volumena je nalaz iz odeljka C,
koji nije rađen.

Deload nedelje se u učenje ionako ne uzimaju.

## Testovi

`WeeklyVolumeTargetTests` (13 tvrdnji): cilj po cilju bloka, odnos prema osnovnoj nedelji,
ograničenje na `[MEV, MRV]` nad celim opsegom propisa, ponašanje kada polazni volumen nije
poznat (nedelja tada gađa cilj bloka nepomeren), talas kroz alokator naspram fiksnog MAV-a, i
da blok snage dobije manje serija od hipertrofijskog uz iste granice.

Ukupno: 537 → 550 testova na serveru, 139 na klijentu.
