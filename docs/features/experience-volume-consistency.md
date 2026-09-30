# Volumen naprednog nivoa

**Grana:** `fix/experience-volume-consistency`

Sedma grana iz revizije nauke o treningu (odeljak F), nalaz F4, odluka D3.

## Problem

Kod naprednog nivoa su dve poluge vukle na suprotne strane.

- `StartingSetsPerExercise` je pratio priručnik („manji volumen, uz napredne tehnike") i
  naprednom davao **3** serije po vežbi, manje od srednjeg nivoa (4).
- `LandmarkScale` je naprednom množio granice volumena sa **1.2**, najviše od tri nivoa.

Propis je tako bio ispod vežbačevog MEV-a, a razliku je krpilo balansiranje. Priručnikov manji
volumen podrazumeva napredne tehnike (drop set, rest-pause), koje nose ostatak stimulusa, a
njih aplikacija ne modeluje. Bez njih je manje serija samo manje stimulusa, i to baš za vežbača
kome granice stoje najviše. Kod treniranih više serija daje više hipertrofije (Schoenfeld i sar.
2019).

Test `LevelConstants_CapTheWeekBelowTheAdvancedLifterOwnMev` je to beležio i tražio da bude
obrisan „kad se konstante usklade". Obrazloženje skale je za početnika bilo naopako: „serija
početnika je slabiji stimulus" bi tražila **više** serija, a ne manje.

## Rešenje

- Napredni nivo kreće sa **4** serije po vežbi, kao srednji. Granice ×1.2 ostaju.
- Obrazloženje skale je ispravljeno. Početnik raste i na manjem volumenu i ima manji radni
  kapacitet, a napredni treba više volumena da bi i dalje rastao.
- Test sa zbirom je zamenjen sa dva nova:
  - `AHigherVolumeBand_NeverStartsWithFewerSetsPerExercise`: nivo sa višim granicama ne
    kreće sa manje serija. To je pravilo koje je bilo prekršeno.
  - `AnAdvancedLifter_CannotReachChestAndBackMev_InOneSessionAWeek`: zapisana granica,
    opisana ispod.

## Šta je merenje reklo suprotno od očekivanja

1. **Test koji je trebalo da padne nije pao.** Stari test je poredio zbir serija (3 dana × 6
   vežbi × serije) sa zbirom MEV vrednosti svih grupa (90). Sa 4 serije je 72 < 90, pa je
   prolazio i dalje. Račun je bio pogrešan u samom temelju: serija složene vežbe puni više
   grupa, pa zbir MEV-a nije ono što nedelja mora da isporuči. Pravi manjak, meren po mišiću
   preko svake trenažne nedelje ugrađenih šablona od tri dana naviše, pao je sa 153 na 75
   nedelja-mišića (hipertrofija, ravan model, od 240).
2. **Na ekranu Analitika se broj grupa ispod MEV-a skoro ne menja.** Plan je tvrdio da vežbač
   koji odradi tačno propisano vidi „ispod MEV-a". Uživo, ista nedelja odrađena po planu sa
   starim i sa novim pravilom daje:

   | Šablon | 3 serije | 4 serije |
   |---|---|---|
   | Upper/Lower | 0 od 10 grupa ispod MEV-a | 0 od 10 |
   | Push/Pull/Legs (3 dana) | 4 od 10 (leđa 7.5, grudi 11, gluteusi 2.5, zadnja loža 5) | 4 od 10 (leđa 9, grudi 11, gluteusi 3, zadnja loža 6) |

   Balansiranje je manjak već krpilo. U simulaciji je nedelja hipertrofije podigla 155 od 195
   vežbi, i to 131 do granice pomeraja (+2). Sa 4 serije ih podiže 118, a do granice 80. Posle
   balansiranja je ispod MEV-a ostalo 36 nedelja-mišića sa 3 serije i 33 sa 4 (hipertrofija),
   odnosno 24 i 6 (snaga). Izmena je dakle najviše promenila **ko nosi serije**: propis umesto
   balansiranja, a kod bloka snage i sam ishod.
3. **Deo manjka je strukturan i nijedna konstanta ga ne popravlja.** MEV naprednog za grudi i
   leđa je 12 (10 × 1.2), a granica serija po jednom treningu je 11 (#87). Šablon koji mišić
   trenira jednom nedeljno, Push/Pull/Legs od tri dana, zato za grudi i leđa ostaje ispod
   MEV-a i posle balansiranja. Test to sada drži, a uputstvo naprednom preporučuje šablon koji
   svaki mišić trenira dva puta nedeljno.

Ostatak manjka (gluteusi na više šablona, listovi i trbuh na Full Body, zadnja loža na
Push/Pull/Legs) potiče iz sastava treninga. U hipertrofiji napredni dobija jednu složenu vežbu
po treningu (priručnikovo „do 3 složene nedeljno"), a gluteusi i zadnja loža volumen dobijaju
uglavnom iz složenih vežbi. To je pitanje modela, a ne greška u konstanti, pa ova grana to
ne menja.

## Provera

- `dotnet test`: **803** (bilo 802: jedan test obrisan, dva dodata); `npm test` 192 i
  `npm run build` prolaze.
- Merenje vraćanjem (commit, 4 → 3, rebuild): obara **1** od 803
  (`AHigherVolumeBand_NeverStartsWithFewerSetsPerExercise`). Test strukturne granice prolazi i
  sa 3 i sa 4 serije, jer beleži granicu, a ne pravilo.
- Proba sa 4 serije pre izmene: ceo paket zelen, uključujući i test MRV-a za svaki šablon na
  naprednom nivou. Nijedan šablon ne prelazi MRV.
- End-to-end, uživo (napredni, hipertrofija, ravan model, nedelja 1 odrađena tačno po planu,
  podrazumevane granice): propis 4 serije po vežbi, a Analitika daje brojeve iz tabele iznad.
  Isto je pušteno i sa vraćenim pravilom (3 serije), posle rebuild-a i restarta API-ja.

## Ograničenja

- Postojeći blokovi zadržavaju broj serija sa kojim su generisani. Nivo se čita sa bloka
  (runda 12), a serije sa plana.
- Push/Pull/Legs od tri dana ostaje za naprednog ispod MEV-a za grudi i leđa (vidi iznad).
