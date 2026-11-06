# Leđa naprednog u bloku snage na Full Body

**Grana:** `fix/full-body-strength-back`

Treći korak plana posle revizije (6. 11. 2026), stavka R2.

## Problem

Posle #100 (mrtvo dizanje je vežba za gluteus, a leđa dobijaju pola) blok snage na Full Body
(3 dana) ostavlja naprednog ispod MEV-a u nedeljama koje nose seriju manje, a to su prva nedelja
linearnog i prve dve nedelje obrnutog bloka:

- leđa: **11** naspram MEV-a **12**;
- biceps: **9.5** naspram **10**. To je proba tek sada pokazala; uputstvo je ranije navodilo
  samo leđa.

## Šta je urađeno

Full Body, dan B: **Rear Delt Fly → Face Pull**. Face Pull isto radi zadnji deltoid (Shoulders
1.0), a daje i pola serije za leđa. Napredni u bloku snage uzima samo dve složene vežbe po
treningu, pa mu leđa nose pulldown na danu A, zgib na danu C, pola mrtvog dizanja i sada dva
Face Pull-a.

Full Body, dan C: **Hammer Curl pre Face Pull-a**. Početnik uzima samo dve izolacije, pa ovako
dobija prvu direktnu vežbu za biceps u šablonu. Srednji i napredni nivo uzimaju sve četiri, pa
im redosled ne menja ništa.

Napredni u lakoj nedelji bloka snage sada ima leđa **12.5** (MEV 12), ramena 11 (10) i triceps 8
(7). Biceps ostaje na **9.5** naspram 10. Za njega nije nađena zamena koja ne obara grudi,
kvadriceps ili zadnju ložu u istim nedeljama, jer nijedan od tih mišića nema rezervu. Test
`AnAdvancedStrengthBlockOnFullBody_FallsBelowMev_OnlyWhereRecorded` beleži tu rupu tačno, pa
svaka kasnija izmena dana A–C mora ponovo da se izmeri.

## Šta je proba pokazala

Brojka je broj parova nedelja-mišić ispod MEV-a, preko svih ugrađenih šablona od tri dana naviše,
svih nivoa, ciljeva i modela (uključujući stari linearan), za mišiće koji te nedelje imaju cilj.

| Varijanta | Ukupno | Leđa naprednog | Test referentnog nivoa |
|---|---|---|---|
| bez izmene | 491 | 11 | prolazi |
| Straight-Arm Pulldown posle Leg Extension (dan B) | 526 | iznad MEV-a | **pada**: triceps |
| Straight-Arm Pulldown umesto Leg Extension, ona pre Skull Crusher | 530 | iznad MEV-a | **pada**: triceps, početniku i kvadriceps |
| isto, ali Leg Extension posle Skull Crusher | 487* | iznad MEV-a | prolazi, ali kvadriceps pada ispod MEV-a na sva tri nivoa |
| Rear Delt Fly → Face Pull (dan B) | 490* | 12.5 | prolazi; početniku tri nove rupe za biceps |
| **isto + Hammer Curl pre Face Pull-a (dan C)** | **459** | **12.5** | **prolazi; nijedna nova rupa** |

Prve dve varijante istiskuju Skull Crusher, jedinu vežbu za triceps dana B, pa triceps pada ispod
MEV-a srednjem i naprednom nivou (početnik Skull Crusher ni ranije nije dobijao). U njima je i
trbuh naprednog u bloku hipertrofije ostajao bez ijedne vežbe, pa je bez cilja ispadao iz brojke
i one su izgledale bolje nego što jesu. Usvojena varijanta tog artefakta nema: bez filtera je
959 → 927, isti pad od 32.

\* izmerio pregled PR-a; ostale redove i usvojenu varijantu izmerio sam.

Moju prvu verziju ove grane je pregled oborio. Ona je zaključila da se ispravka ne isplati i
zakucala obe rupe testom, jer sam probao samo dve varijante, obe na mestu Leg Extension. Varijantu
sa Face Pull-om je našao pregled; ovde je ponovo izmerena.

## Provera

- `dotnet test`: **867** (bilo 866). Klijent 204, bez izmena.
- Nijedna nedelja iznad MRV-a, pre ni posle.
- Novi test formatira broj serija nezavisno od kulture (na `sr-Latn-RS` bi `11.0` bilo `11,0`).
  Mišiće bez cilja ne preskače neprimećeno: posebno proverava da su to tačno trbuh i listovi.
- Uživo, na 375 px: nov plan Full Body ima na danu B Face Pull, a ne Rear Delt Fly.
