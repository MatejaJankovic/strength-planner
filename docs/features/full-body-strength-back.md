# Leđa naprednog u bloku snage na Full Body

**Grana:** `fix/full-body-strength-back`

Treći korak plana posle revizije (6. 11. 2026), stavka R2.

## Problem

Posle #100 (mrtvo dizanje je vežba za gluteus, leđa dobijaju pola) blok snage na Full Body
(3 dana) ostavlja naprednog ispod MEV-a u nedeljama koje nose seriju manje, a to su prva nedelja
linearnog i prve dve nedelje obrnutog bloka:

- leđa: **11** naspram MEV-a **12**;
- biceps: **9.5** naspram **10**. To je proba tek sada pokazala; uputstvo je ranije navodilo
  samo leđa.

## Šta je proba pokazala

Napredni u bloku snage dobija dve složene vežbe po treningu i šest vežbi ukupno. Na danu B to su
Overhead Press i Deadlift, pa Leg Extension, Rear Delt Fly, Barbell Curl i Skull Crusher. Vežba za
leđa na tom danu mora nešto da istisne. Probane su dve varijante, nad svim šablonima, nivoima,
ciljevima i modelima:

| Varijanta | Leđa naprednog | Nedelja ispod MEV-a, svi šabloni | Test referentnog nivoa |
|---|---|---|---|
| bez izmene | 11 u četiri lakše nedelje | **491** | prolazi |
| Straight-Arm Pulldown posle Leg Extension | iznad MEV-a | 526 | **pada**: triceps srednjeg nivoa ispod MEV-a |
| Straight-Arm Pulldown umesto Leg Extension, ona kasnije | iznad MEV-a | 530 | **pada**: triceps, a početniku i kvadriceps |

U obe varijante pulldown istiskuje Skull Crusher, jedinu vežbu za triceps tog dana. Triceps tada
pada ispod MEV-a na sva tri nivoa, u gotovo svakoj nedelji. Rupa od jedne serije u četiri lakše
nedelje je dakle jeftinija od svake izmene šablona. To je isti ishod kao nalaz o rasporedu vežbi
u rundi 13.

## Šta je urađeno

- **Šablon se ne menja.**
- Novi test `AnAdvancedStrengthBlockOnFullBody_FallsBelowMev_OnlyWhereRecorded` beleži rupu tačno
  onoliku kolika je: leđa i biceps, u prvoj nedelji linearnog i prve dve obrnutog bloka. Ako
  rupa poraste ili nestane, na primer zato što se promeni doprinos mrtvog dizanja ili sastav dana
  B, test pada i traži da se odluka ponovo izmeri.
- Uputstvo navodi i biceps i kaže zašto ispravke nema.

## Provera

- `dotnet test`: **867** (bilo 866). Klijent 204, bez izmena.
- Obe varijante iz tabele obaraju novi test, jer menjaju spisak (dodaju triceps), a uz njega i
  test referentnog nivoa.
