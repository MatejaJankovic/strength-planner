# Vežba za gornje telo na danu za noge — pola nalaza je palo na merenju

Nalaz E3 iz pregleda logike treninga.

## Šta je pregled prijavio

> *Face Pull je u „Lower C", a Straight-Arm Pulldown u „Legs C".*

Obe su vežbe za gornje telo, na danu za noge. Deluje kao greška u sastavljanju šablona.

## Šta je merenje pokazalo

Nije greška — **nužnost**. Treći dan za noge ima mesta u trajanju, ali ne i u volumenu.

Pokušao sam oba očigledna rešenja i oba su pala na zatečenim testovima:

| Pokušaj | Rezultat |
|---|---|
| Izbaciti vežbu, dan ostaje kraći | `EveryDay_GivesEveryLevelAFullSession`: trening naprednog vežbača pada na **4 vežbe**, a traži se najmanje 5 |
| Zameniti je vežbom za noge (`Leg Press`, `Goblet Squat`) | `EveryTemplate_StaysUnderMrvForEveryMuscle`: kvadriceps **16.5 uz MRV 16** na skali početnika; kod Legs Specialization i gluteusi 13.5 uz MRV 13 |

Razlog stoji u komentarima koji su tu bili i pre: `Leg Curl` i `Leg Extension` su svaki
jednom potrošeni na prva dva dana za noge, a treći put gura kvadriceps ili zadnju ložu preko
MRV. Nema ni jedne izolacije za noge ili trup koja je ostala slobodna — `Calf Raise`,
`Machine Crunch` i `Cable Crunch` su već na tom danu.

Dakle: mesto **mora** da popuni vežba za gornje telo. Dan je „za noge" po tome šta ga
određuje, a ne po tome da nijedna druga vežba ne sme da uđe.

## Šta je ipak ispravljeno

Ostaje jedna stvar koja jeste bila pogrešna, i to samo u `Legs Specialization`:

**`Straight-Arm Pulldown` je bila jedina izolacija za leđa u celom šablonu — i stajala je na
danu za noge.** Vežbač koji traži rad za leđa gleda „Upper A" i „Upper B", gde je nema.

Preseljena je na **Upper A**, i to **ispred ruku**: sastav treninga uzima izolacije redom, pa
bi sa poslednjeg mesta do srednjeg nivoa nikada ne bi ni stigla — ista zamka koju je runda
13 već našla kod zadnjeg deltoida u Upper/Lower.

Mesto na „Legs C" uzela je **`Dumbbell Curl`**. Ruke na danu za noge su uobičajen način da se
iskoristi vreme koje nogama više ne treba, i ne takmiče se ni sa čim na tom danu.

`Upper/Lower x3` ostaje kakav je: `Face Pull` na „Lower C" je isti kompromis, a u tom šablonu
nema nijedne neiskorišćene izolacije za gornje telo kojom bi se zamenio. Razlog je sada
zapisan uz sam dan, da sledeći čitalac ne pomisli da je slučajan.

## Šta je provereno

- Svih **613** testova prolazi. Merodavni su `EveryDay_GivesEveryLevelAFullSession`,
  `TemplatesOfThreeDaysOrMore_ReachMevAtTheReferenceLevel` i
  `EveryTemplate_StaysUnderMrvForEveryMuscle` — prva dva su i uhvatila da izbacivanje ne
  prolazi, treći da zamena vežbom za noge ne prolazi.
- Leđa u `Legs Specialization` i dalje stižu MEV: bez `Straight-Arm Pulldown` test javlja
  **8 uz MEV 10**, pa je premeštena vežba ta koja ih drži iznad granice — sada sa dana na
  kome je vežbač i traži.

## Zaključak za rad

Nalaz E3 je **pola tačan**. Placement jeste bio pogrešan u jednom šablonu i tamo je
ispravljen; u drugom je bio izbor koji volumenske granice nameću, a jedini pravi propust bio
je to što taj izbor nigde nije bio obrazložen.
